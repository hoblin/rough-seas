using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Newtonsoft.Json.Linq;
using RoughSeas.SeaAndWeather;
using RoughSeas.Vessels;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace AgentBridge;

public class BridgeCommands : ModSystem
{
    private static readonly (double Angle, double Reach)[] StormProbes =
        Enumerable.Range(0, 12).SelectMany(i => new[] { 0.25, 0.45, 0.65, 0.85, 1.0 }, (i, reach) => (i * Math.PI / 6, reach)).ToArray();

    private const int HoverAboveSea = 1;
    private const double NearbyGridBlocks = 5000;
    private const int WaveProbes = 12;

    private ICoreServerAPI sapi = null!;
    private DriftSwitch? drift;
    private ChunkSettle settle = null!;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
        settle = new ChunkSettle(api, Mod.Logger);
        var benchDefaults = Environment.GetEnvironmentVariable("AGENT_BRIDGE_BENCH_DEFAULTS") != "off";
        if (api.ModLoader.IsModEnabled("roughseas")) drift = new DriftSwitch { Drifting = !benchDefaults };
        if (benchDefaults) TurnOffTemporalEffects(api.World.Config);
        var parsers = api.ChatCommands.Parsers;
        api.ChatCommands.Create("bridge")
            .WithDescription("Agent bridge: server-side state as JSON")
            .RequiresPrivilege(Privilege.controlserver)
            .BeginSubCommand("here")
                .WithDescription("Sea, weather and climate at the caller's position")
                .RequiresPlayer()
                .HandleWith(args => Reply(args.Caller, JsonSerializer.Serialize(Here(args.Caller.Pos, args.Caller.Entity?.Teleporting == true))))
            .EndSubCommand()
            .BeginSubCommand("entity")
                .WithDescription("An entity's server-side attributes")
                .WithArgs(parsers.Long("id"))
                .HandleWith(args => Entity(args.Caller, (long)args[0]))
            .EndSubCommand()
            .BeginSubCommand("sea")
                .WithDescription("Fly the caller to the nearest open sea within 1500 blocks, hovering just above the surface")
                .RequiresPlayer()
                .HandleWith(args => Sea((IServerPlayer)args.Caller.Player))
            .EndSubCommand()
            .BeginSubCommand("shore")
                .WithDescription("Fly the caller to the nearest dry, open ground beside the sea within 256 blocks, hovering just above it")
                .RequiresPlayer()
                .HandleWith(args => Shore((IServerPlayer)args.Caller.Player))
            .EndSubCommand()
            .BeginSubCommand("await")
                .WithDescription("Move time on until a storm of the model, of a type and warmth when given, covers the caller where they stand: no flight, no new chunks")
                .RequiresPlayer()
                .WithArgs(parsers.OptionalWord("type"), parsers.OptionalWord("warmth"), parsers.OptionalDouble("presence", 0.7), parsers.OptionalDouble("hours", 0))
                .HandleWith(args => Await((IServerPlayer)args.Caller.Player, (string?)args[0], (string?)args[1], (double)args[2], (double)args[3]))
            .EndSubCommand()
            .BeginSubCommand("storm")
                .WithDescription("Fly the caller to where the storms within range raise the highest waves over open sea, or the waves nearest a height, hovering just above the surface")
                .RequiresPlayer()
                .WithArgs(parsers.OptionalDouble("range", 60000), parsers.OptionalDouble("waves", 0))
                .HandleWith(args => Storm((IServerPlayer)args.Caller.Player, (double)args[0], (double)args[1]))
            .EndSubCommand()
            .BeginSubCommand("force")
                .WithDescription("Fly the caller to the roughest of the nearest open seas where the wind blows at a Beaufort force; seated, reply with the /tp that takes the vessel along")
                .RequiresPlayer()
                .WithArgs(parsers.IntRange("force", 0, 12), parsers.OptionalDouble("range", 30000))
                .HandleWith(args => Force((IServerPlayer)args.Caller.Player, (int)args[0], (double)args[1]))
            .EndSubCommand()
            .BeginSubCommand("approach")
                .WithDescription("Fly the caller over open sea a distance outside the rim of the strongest storm in range, of a type when given, and say the yaw that faces its centre")
                .RequiresPlayer()
                .WithArgs(parsers.OptionalDouble("distance", 500), parsers.OptionalWord("type"))
                .HandleWith(args => Approach((IServerPlayer)args.Caller.Player, (double)args[0], (string?)args[1]))
            .EndSubCommand()
            .BeginSubCommand("find")
                .WithDescription("Fly the caller over the nearest block at the surface whose code matches a wildcard, within range, in the loaded world")
                .RequiresPlayer()
                .WithArgs(parsers.Word("code wildcard"), parsers.OptionalIntRange("range", 1, 512, 256))
                .HandleWith(args => Find((IServerPlayer)args.Caller.Player, (string)args[0], (int)args[1]))
            .EndSubCommand()
            .BeginSubCommand("drift")
                .WithDescription("Whether unridden vessels drift with the current and the wind; off on every bench start, so they hold still")
                .WithArgs(parsers.OptionalWordRange("state", "on", "off"))
                .HandleWith(args => Drift(args[0] as string))
            .EndSubCommand()
            .BeginSubCommand("set")
                .WithDescription("Set an entity's watched attribute, tree/key for one inside a tree: int, float, bool or string by the value's form, while a double or float stays what it is")
                .WithArgs(parsers.Long("id"), parsers.Word("key"), parsers.Word("value"))
                .HandleWith(args => Set(sapi.World.GetEntityById((long)args[0]), (string)args[1], (string)args[2]))
            .EndSubCommand()
            .BeginSubCommand("ride")
                .WithDescription("Spawn a boat (or any seatable entity) at the caller, at the surface over water, and seat the caller in it")
                .RequiresPlayer()
                .WithArgs(parsers.Word("entity code"))
                .HandleWith(args => Ride(args.Caller.Player, (string)args[0]))
            .EndSubCommand()
            .BeginSubCommand("mount")
                .WithDescription("Seat the caller in an entity, a steering seat first, or in its seat of this index")
                .RequiresPlayer()
                .WithArgs(parsers.Long("id"), parsers.OptionalInt("seat", -1))
                .HandleWith(args => Mount(args.Caller.Player, sapi.World.GetEntityById((long)args[0]), (int)args[1]))
            .EndSubCommand()
            .BeginSubCommand("wear")
                .WithDescription("Put an item into the caller's first empty backpack slot, where it is worn on the back")
                .RequiresPlayer()
                .WithArgs(parsers.Word("item code"))
                .HandleWith(args => Wear(args.Caller.Player, (string)args[0]))
            .EndSubCommand()
            .BeginSubCommand("hold")
                .WithDescription("Put an item into the caller's active hotbar slot, held in the hand")
                .RequiresPlayer()
                .WithArgs(parsers.Word("item code"))
                .HandleWith(args => Hold(args.Caller.Player, (string)args[0]))
            .EndSubCommand()
            .BeginSubCommand("drop")
                .WithDescription("Drop an item on the ground at a position, at rest, as a dropped stack lies")
                .WithArgs(parsers.Word("item code"), parsers.WorldPosition("position"))
                .HandleWith(args => Drop((string)args[0], (Vec3d)args[1]))
            .EndSubCommand()
            .BeginSubCommand("tie")
                .WithDescription("Tie a rope-tieable entity to a block with a vanilla rope, as a player with rope in hand would; X Y Z absolute")
                .WithArgs(parsers.Long("id"), parsers.Int("x"), parsers.Int("y"), parsers.Int("z"))
                .HandleWith(args => Tie(args.Caller, sapi.World.GetEntityById((long)args[0]), new BlockPos((int)args[1], (int)args[2], (int)args[3])))
            .EndSubCommand()
            .BeginSubCommand("ropes")
                .WithDescription("Where each rope tied to an entity is pinned at both ends: a block position, an entity id, or loose")
                .WithArgs(parsers.Long("id"))
                .HandleWith(args => Ropes(args.Caller, (long)args[0]))
            .EndSubCommand()
            .BeginSubCommand("fly")
                .WithDescription("Hover the caller at a position in creative free flight: no post, no sinking; X Y Z absolute or ~ relative")
                .RequiresPlayer()
                .WithArgs(parsers.WorldPosition("position"))
                .HandleWith(args => Fly((IServerPlayer)args.Caller.Player, (Vec3d)args[0]))
            .EndSubCommand()
            .BeginSubCommand("dismount")
                .WithDescription("Get the caller out of whatever they are seated in, leaving it where it is")
                .RequiresPlayer()
                .HandleWith(args => args.Caller.Player.Entity.TryUnmount()
                    ? TextCommandResult.Success("Dismounted")
                    : TextCommandResult.Error("Not seated, or the seat would not let go"))
            .EndSubCommand()
            .BeginSubCommand("strike")
                .WithDescription("Strike a vessel's hull with an impact of this many joules; the hull rolls against it as it does for any impact")
                .WithArgs(parsers.Long("id"), parsers.Double("joules"))
                .HandleWith(args => Strike(sapi.World.GetEntityById((long)args[0]), (double)args[1]))
            .EndSubCommand()
            .BeginSubCommand("blast")
                .WithDescription("Set off an explosion that harms entities but no blocks, this many blocks from an entity")
                .WithArgs(parsers.Long("id"), parsers.Int("injureRadius"), parsers.OptionalDouble("distance"))
                .HandleWith(args => Blast(sapi.World.GetEntityById((long)args[0]), (int)args[1], (double)args[2]))
            .EndSubCommand()
            .BeginSubCommand("interact")
                .WithDescription("Attack or interact with an entity using the held item, as the server does when the player clicks it")
                .RequiresPlayer()
                .WithArgs(parsers.Long("id"), parsers.WordRange("mode", "attack", "interact"), parsers.OptionalWordRange("modifier", "none", "ctrl", "shift"), parsers.OptionalWord("box"))
                .HandleWith(args => Interact(args.Caller.Player, sapi.World.GetEntityById((long)args[0]), (string)args[1], args[2] as string, args[3] as string))
            .EndSubCommand();
    }

    public override void Dispose() => drift?.Dispose();

    private void TurnOffTemporalEffects(ITreeAttribute config)
    {
        config.SetString("temporalStorms", "off");
        config.SetBool("temporalStability", false);
        config.SetString("temporalRifts", "off");
        Mod.Logger.Notification("Temporal storms, stability and rifts are off for this world");
    }

    private TextCommandResult Drift(string? state)
    {
        if (drift == null) return TextCommandResult.Error("Rough Seas is not loaded; nothing drifts by our current");
        if (state != null) drift.Drifting = state == "on";
        return TextCommandResult.Success(drift.Drifting
            ? "Unridden vessels drift with the current and the wind"
            : "Unridden vessels hold still; /bridge drift on lets them drift");
    }

    private TextCommandResult Await(IServerPlayer player, string? type, string? warmth, double presence, double hours)
    {
        const double step = 0.25;
        const double warm = 12;
        const double cold = 0;
        if (type is "any") type = null;
        if (warmth is not (null or "any" or "warm" or "cold")) return TextCommandResult.Error("Warmth is warm (thunder from 12 °C), cold (snow, below 0 °C) or any");

        var sea = sapi.ModLoader.GetModSystem<SeaWeatherSystem>().Model;
        var calendar = sapi.World.Calendar;
        if (hours <= 0) hours = calendar.DaysPerYear * calendar.HoursPerDay;
        var pos = player.Entity.Pos;
        var at = pos.AsBlockPos;
        var (strongest, strongestAhead, strongestCode) = (0.0, 0.0, "storm");
        for (var ahead = step; ahead <= hours; ahead += step)
        {
            var when = sea.NowHours + ahead;
            var storms = sea.StormsAt(pos.X, pos.Z, when);
            if (storms.Storm == null || type != null && storms.Storm.Code != type) continue;
            if (storms.Presence > strongest) (strongest, strongestAhead, strongestCode) = (storms.Presence, ahead, storms.Storm.Code);
            if (storms.Presence < presence) continue;
            var celsius = sapi.World.BlockAccessor.GetClimateAt(at, EnumGetClimateMode.ForSuppliedDateValues, when / calendar.HoursPerDay)?.Temperature ?? double.NaN;
            if (warmth == "warm" && !(celsius >= warm) || warmth == "cold" && !(celsius < cold)) continue;

            calendar.Add((float)ahead);
            return TextCommandResult.Success(string.Format(CultureInfo.InvariantCulture,
                "A {0} covers you at {1:P0} presence: time moved on {2:F2} h to hour {3:F2}, {4:F1} °C", storms.Storm.Code, storms.Presence, ahead, when, celsius));
        }
        return TextCommandResult.Error(string.Format(CultureInfo.InvariantCulture,
            "No {0}{1} reaches {2:P0} presence here within {3} h; the strongest is a {4} at {5:P0}, {6:F2} h ahead",
            type ?? "storm", warmth is null or "any" ? "" : ", " + warmth + ",", presence, hours, strongestCode, strongest, strongestAhead));
    }

    private TextCommandResult Storm(IServerPlayer player, double range, double waves)
    {
        var sea = sapi.ModLoader.GetModSystem<SeaWeatherSystem>().Model;
        var pos = player.Entity.Pos;
        var probed = sea.StormsWithin(pos.X, pos.Z, range)
            .SelectMany(storm =>
            {
                var (x, z) = storm.CentreAt(sea.NowHours);
                return StormProbes.Select(probe => (x: x + Math.Cos(probe.Angle) * probe.Reach * storm.Radius, z: z + Math.Sin(probe.Angle) * probe.Reach * storm.Radius));
            })
            .Where(point => sea.IsSea(point.x, point.z))
            .OrderByDescending(point => sea.StormsAt(point.x, point.z, sea.NowHours).Speed)
            .Take(WaveProbes)
            .Select(point => (point.x, point.z, waves: sea.At(point.x, point.z).WaveHeight))
            .Where(point => point.waves > 0)
            .ToArray();
        var chosen = waves > 0
            ? probed.OrderBy(point => Math.Abs(point.waves - waves)).FirstOrDefault()
            : probed.OrderByDescending(point => point.waves).FirstOrDefault();
        if (chosen.waves <= 0) return TextCommandResult.Error($"No storm over open sea within {range} blocks; /seas storms lists them");

        var flown = Fly(player, new Vec3d((int)chosen.x + 0.5, sapi.World.SeaLevel + HoverAboveSea, (int)chosen.z + 0.5));
        return TextCommandResult.Success($"{flown.StatusMessage}, over the storm's {(waves > 0 ? "waves nearest " + waves.ToString("F1", CultureInfo.InvariantCulture) + " m" : "highest waves")}, {chosen.waves:F1} m");
    }

    private TextCommandResult Force(IServerPlayer player, int force, double range)
    {
        const double step = 500;
        var weather = sapi.ModLoader.GetModSystem<SeaWeatherSystem>();
        var sea = weather.Model;
        var beaufort = new BeaufortScale(weather.Config.Beaufort);
        var pos = player.Entity.Pos;
        var steps = (int)(Math.Min(range, NearbyGridBlocks) / step);
        var chosen = Enumerable.Range(-steps, 2 * steps + 1)
            .SelectMany(_ => Enumerable.Range(-steps, 2 * steps + 1), (i, j) => (x: pos.X + i * step, z: pos.Z + j * step))
            .Concat(sea.StormsWithin(pos.X, pos.Z, range).SelectMany(storm =>
            {
                var (x, z) = storm.CentreAt(sea.NowHours);
                return StormProbes.Select(probe => (x: x + Math.Cos(probe.Angle) * probe.Reach * storm.Radius, z: z + Math.Sin(probe.Angle) * probe.Reach * storm.Radius));
            }))
            .Where(point => sea.IsSea(point.x, point.z))
            .Where(point => beaufort.ForceAt(Speed(sea.Wind(point.x, point.z))).Force == force)
            .OrderBy(point => Math.Abs(point.x - pos.X) + Math.Abs(point.z - pos.Z))
            .Take(WaveProbes)
            .Select(point => (point.x, point.z, state: sea.At(point.x, point.z)))
            .Where(point => point.state.WaveHeight > 0)
            .OrderByDescending(point => point.state.WaveHeight)
            .Select(point => ((double x, double z, SeaState state)?)point)
            .FirstOrDefault();
        if (chosen == null) return TextCommandResult.Error($"No open sea at Beaufort {force} within {NearbyGridBlocks} blocks, nor in a storm within {range}");

        var target = new Vec3d((int)chosen.Value.x + 0.5, sapi.World.SeaLevel + HoverAboveSea, (int)chosen.Value.z + 0.5);
        if (player.Entity.MountedOn != null)
        {
            var accessor = sapi.World.BlockAccessor;
            return TextCommandResult.Success(string.Format(CultureInfo.InvariantCulture, "Beaufort {0}, waves {1:F1} m at {2:F0} {3:F0}; '/tp {2:F0} {4} {3:F0}' sails there with the vessel",
                force, chosen.Value.state.WaveHeight, target.X - accessor.MapSizeX / 2, target.Z - accessor.MapSizeZ / 2, target.Y));
        }
        var flown = Fly(player, target);
        return TextCommandResult.Success(string.Format(CultureInfo.InvariantCulture, "{0}: Beaufort {1}, waves {2:F1} m", flown.StatusMessage, force, chosen.Value.state.WaveHeight));
    }

    private static double Speed((double X, double Z) wind) => Math.Sqrt(wind.X * wind.X + wind.Z * wind.Z);

    private TextCommandResult Approach(IServerPlayer player, double distance, string? type)
    {
        const double range = 60000;
        var sea = sapi.ModLoader.GetModSystem<SeaWeatherSystem>().Model;
        var pos = player.Entity.Pos;
        var now = sea.NowHours;
        var chosen = sea.StormsWithin(pos.X, pos.Z, range)
            .Where(storm => type == null || storm.Code == type)
            .Select(storm => (storm, centre: storm.CentreAt(now)))
            .Where(entry => sea.IsSea(entry.centre.X, entry.centre.Z))
            .SelectMany(_ => Enumerable.Range(0, 12), (entry, i) =>
            {
                var reach = entry.storm.Radius + distance;
                return (entry.storm, entry.centre, x: entry.centre.X + Math.Cos(i * Math.PI / 6) * reach, z: entry.centre.Z + Math.Sin(i * Math.PI / 6) * reach);
            })
            .Where(point => sea.IsSea(point.x, point.z))
            .OrderByDescending(point => point.storm.Strength(now))
            .ThenBy(point => Math.Abs(point.x - pos.X) + Math.Abs(point.z - pos.Z))
            .FirstOrDefault();
        if (chosen.storm == null) return TextCommandResult.Error($"No {type ?? "storm"} over open sea within {range} blocks; /seas storms lists them");

        var flown = Fly(player, new Vec3d((int)chosen.x + 0.5, sapi.World.SeaLevel + HoverAboveSea, (int)chosen.z + 0.5));
        var yaw = Math.Atan2(chosen.centre.X - chosen.x, chosen.centre.Z - chosen.z);
        return TextCommandResult.Success(string.Format(CultureInfo.InvariantCulture,
            "{0}, {1:F0} blocks outside the rim of a {2} at {3:P0} strength; yaw {4:F2} faces its centre",
            flown.StatusMessage, distance, chosen.storm.Code, chosen.storm.Strength(now), yaw));
    }

    private bool OpenSea(int x, int z)
    {
        var accessor = sapi.World.BlockAccessor;
        var regionSize = accessor.RegionSize;
        return accessor.GetMapRegion(x / regionSize, z / regionSize)?.OceanMap is { Data.Length: > 0 } map
            && map.GetUnpaddedIntLerpedForBlockPos(x, z, regionSize) >= 250;
    }

    private TextCommandResult Sea(IServerPlayer player)
    {
        const int range = 1500;
        const int step = 16;
        var origin = player.Entity.Pos.AsBlockPos;

        var sea = Enumerable.Range(-range / step, 2 * range / step + 1)
            .SelectMany(i => Enumerable.Range(-range / step, 2 * range / step + 1), (i, j) => (x: origin.X + i * step, z: origin.Z + j * step))
            .Where(point => OpenSea(point.x, point.z))
            .OrderBy(point => (point.x - origin.X) * (long)(point.x - origin.X) + (point.z - origin.Z) * (long)(point.z - origin.Z))
            .Select(point => ((int x, int z)?)point)
            .FirstOrDefault();
        if (sea == null) return TextCommandResult.Error($"No open sea in the generated regions within {range} blocks");

        var flown = Fly(player, new Vec3d(sea.Value.x + 0.5, sapi.World.SeaLevel + HoverAboveSea, sea.Value.z + 0.5));
        return TextCommandResult.Success($"{flown.StatusMessage}, over open sea");
    }

    private TextCommandResult Shore(IServerPlayer player)
    {
        const int range = 256;
        const int step = 2;
        const int clearing = 3;
        var accessor = sapi.World.BlockAccessor;
        var seaLevel = sapi.World.SeaLevel;
        var origin = player.Entity.Pos.AsBlockPos;

        int Ground(int x, int z) => accessor.GetTerrainMapheightAt(new BlockPos(x, 0, z, 0));
        bool Dry(int x, int z)
        {
            var ground = Ground(x, z);
            return ground >= seaLevel && ground <= seaLevel + 2
                && accessor.GetBlock(new BlockPos(x, ground + 1, z, 0)).CollisionBoxes is null or { Length: 0 }
                && accessor.GetBlock(new BlockPos(x, ground, z, 0), BlockLayersAccess.Fluid).Id == 0;
        }
        bool Open(int x, int z) => Enumerable.Range(-clearing, 2 * clearing + 1)
            .All(i => Enumerable.Range(-clearing, 2 * clearing + 1).All(j => Dry(x + i, z + j) && Math.Abs(Ground(x + i, z + j) - Ground(x, z)) <= 1));
        bool BesideWater(int x, int z) => Enumerable.Range(-12, 25)
            .Any(i => Sea(Ground(x + i, z)) || Sea(Ground(x, z + i)));
        bool Sea(int ground) => ground is > 0 && ground < seaLevel - 1;

        var shore = Enumerable.Range(-range / step, 2 * range / step + 1)
            .SelectMany(i => Enumerable.Range(-range / step, 2 * range / step + 1), (i, j) => (x: origin.X + i * step, z: origin.Z + j * step))
            .OrderBy(point => (point.x - origin.X) * (long)(point.x - origin.X) + (point.z - origin.Z) * (long)(point.z - origin.Z))
            .Where(point => Dry(point.x, point.z) && BesideWater(point.x, point.z) && Open(point.x, point.z))
            .Select(point => ((int x, int z)?)point)
            .FirstOrDefault();
        if (shore == null) return Coast(player);

        var flown = Fly(player, new Vec3d(shore.Value.x + 0.5, Ground(shore.Value.x, shore.Value.z) + 1 + HoverAboveSea, shore.Value.z + 0.5));
        return TextCommandResult.Success($"{flown.StatusMessage}, over the shore");
    }

    private TextCommandResult Find(IServerPlayer player, string wildcard, int range)
    {
        var pattern = new AssetLocation(wildcard);
        var accessor = sapi.World.BlockAccessor;
        var origin = player.Entity.Pos.AsBlockPos;

        BlockPos? Match(int x, int z)
        {
            var ground = accessor.GetTerrainMapheightAt(new BlockPos(x, 0, z, 0));
            return Enumerable.Range(ground, 3)
                .Select(y => new BlockPos(x, y, z, 0))
                .FirstOrDefault(pos => accessor.GetBlock(pos).WildCardMatch(pattern));
        }

        var found = Enumerable.Range(-range, 2 * range + 1)
            .SelectMany(i => Enumerable.Range(-range, 2 * range + 1), (i, j) => (x: origin.X + i, z: origin.Z + j))
            .OrderBy(point => (point.x - origin.X) * (long)(point.x - origin.X) + (point.z - origin.Z) * (long)(point.z - origin.Z))
            .Select(point => Match(point.x, point.z))
            .FirstOrDefault(pos => pos != null);
        if (found == null) return TextCommandResult.Error($"No {wildcard} at the surface within {range} blocks of loaded world");

        var flown = Fly(player, new Vec3d(found.X + 0.5, found.Y + 3, found.Z + 0.5));
        return TextCommandResult.Success($"{flown.StatusMessage}, 3 blocks over {accessor.GetBlock(found).Code}");
    }

    private TextCommandResult Coast(IServerPlayer player)
    {
        const int range = 3000;
        const int step = 32;
        var accessor = sapi.World.BlockAccessor;
        var regionSize = accessor.RegionSize;
        var origin = player.Entity.Pos.AsBlockPos;

        int? Oceanity(int x, int z) => accessor.GetMapRegion(x / regionSize, z / regionSize)?.OceanMap is { Data.Length: > 0 } map
            ? map.GetUnpaddedIntLerpedForBlockPos(x, z, regionSize)
            : null;

        var coast = Enumerable.Range(-range / step, 2 * range / step + 1)
            .SelectMany(i => Enumerable.Range(-range / step, 2 * range / step + 1), (i, j) => (x: origin.X + i * step, z: origin.Z + j * step))
            .Where(point => Oceanity(point.x, point.z) is > 0 and < 60)
            .OrderBy(point => (point.x - origin.X) * (long)(point.x - origin.X) + (point.z - origin.Z) * (long)(point.z - origin.Z))
            .Select(point => ((int x, int z)?)point)
            .FirstOrDefault();
        if (coast == null) return TextCommandResult.Error($"No coast in the generated regions within {range} blocks");

        var flown = Fly(player, new Vec3d(coast.Value.x + 0.5, sapi.World.SeaLevel + 20, coast.Value.z + 0.5));
        return TextCommandResult.Success($"{flown.StatusMessage}, near the coast: no open beach is loaded yet, run /bridge shore again once the land has loaded");
    }

    private TextCommandResult Fly(IServerPlayer player, Vec3d position)
    {
        player.Entity.TryUnmount();
        player.WorldData.CurrentGameMode = EnumGameMode.Creative;
        player.WorldData.FreeMove = true;
        player.BroadcastPlayerData();
        player.Entity.TeleportTo(position);
        var accessor = sapi.World.BlockAccessor;
        var flight = settle.Watch(position);
        return TextCommandResult.Success($"Hovering at {position.X - accessor.MapSizeX / 2:0.#} {position.Y:0.#} {position.Z - accessor.MapSizeZ / 2:0.#} (flight {flight})");
    }

    private static TextCommandResult Set(Entity? entity, string key, string value)
    {
        if (entity == null) return TextCommandResult.Error("No such entity");

        var path = key.Split('/');
        ITreeAttribute attributes = entity.WatchedAttributes;
        foreach (var tree in path[..^1])
        {
            if (attributes.GetTreeAttribute(tree) is not { } inner) return TextCommandResult.Error($"No tree {tree} in {key}");
            attributes = inner;
        }

        var leaf = path[^1];
        var number = double.TryParse(value, CultureInfo.InvariantCulture, out var parsed) ? parsed : (double?)null;
        switch (attributes[leaf])
        {
            case DoubleAttribute when number is { } d: attributes.SetDouble(leaf, d); break;
            case FloatAttribute when number is { } f: attributes.SetFloat(leaf, (float)f); break;
            default:
                if (int.TryParse(value, CultureInfo.InvariantCulture, out var whole)) attributes.SetInt(leaf, whole);
                else if (number is { } fraction) attributes.SetFloat(leaf, (float)fraction);
                else if (bool.TryParse(value, out var flag)) attributes.SetBool(leaf, flag);
                else attributes.SetString(leaf, value);
                break;
        }
        entity.WatchedAttributes.MarkPathDirty(path[0]);
        return TextCommandResult.Success($"{entity.Code} {entity.EntityId}: {key} = {attributes[leaf].ToJsonToken()}");
    }

    private TextCommandResult Ride(IPlayer player, string code)
    {
        var type = sapi.World.GetEntityType(new AssetLocation(code));
        if (type == null) return TextCommandResult.Error($"No entity type {code}");

        if (player.Entity.Teleporting) return TextCommandResult.Error("Still flying there; ride again in a second");

        var boat = sapi.World.ClassRegistry.CreateEntity(type);
        var pos = player.Entity.Pos;
        var accessor = sapi.World.BlockAccessor;
        var below = pos.AsBlockPos;
        while (below.Y > 0 && accessor.GetBlock(below, BlockLayersAccess.FluidOrSolid).Id == 0) below.Y--;
        var surface = accessor.GetBlock(below, BlockLayersAccess.Fluid).IsLiquid()
            ? sapi.World.SeaLevel - 1
            : pos.Y;
        boat.Pos.SetPos(pos.X, surface, pos.Z);
        boat.Pos.Yaw = pos.Yaw;
        sapi.World.SpawnEntity(boat);
        return Mount(player, boat);
    }

    private static TextCommandResult Mount(IPlayer player, Entity? target, int index = -1)
    {
        var seats = target?.GetInterface<IMountable>()?.Seats;
        if (seats == null) return TextCommandResult.Error("Not a seatable entity");

        var seat = index >= 0
            ? seats.ElementAtOrDefault(index)
            : seats.Where(seat => seat.Passenger == null).OrderByDescending(seat => seat.CanControl).FirstOrDefault();
        if (seat == null || !player.Entity.TryMount(seat)) return TextCommandResult.Error("No free seat");

        return TextCommandResult.Success($"Seated in {target!.Code} {target.EntityId}, seat {seat.SeatId}");
    }

    private TextCommandResult Wear(IPlayer player, string code)
    {
        var stack = Stack(code);
        if (stack == null) return TextCommandResult.Error($"No item or block {code}");

        var slot = player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName).FirstOrDefault(slot => slot is ItemSlotBackpack && slot.Empty);
        if (slot == null) return TextCommandResult.Error("No empty backpack slot");

        if (!slot.CanHold(new DummySlot(stack))) return TextCommandResult.Error($"{code} does not go in a backpack slot");

        slot.Itemstack = stack;
        slot.MarkDirty();
        return TextCommandResult.Success($"Wearing {code}");
    }

    private TextCommandResult Hold(IPlayer player, string code)
    {
        var stack = Stack(code);
        if (stack == null) return TextCommandResult.Error($"No item or block {code}");

        var slot = player.InventoryManager.ActiveHotbarSlot;
        slot.Itemstack = stack;
        slot.MarkDirty();
        return TextCommandResult.Success($"Holding {code}");
    }

    private TextCommandResult Drop(string code, Vec3d position)
    {
        var stack = Stack(code);
        if (stack == null) return TextCommandResult.Error($"No item or block {code}");

        var dropped = sapi.World.SpawnItemEntity(stack, position, new Vec3d());
        return TextCommandResult.Success($"Dropped {code}: entity {dropped.EntityId}");
    }

    private ItemStack? Stack(string code)
    {
        var location = new AssetLocation(code);
        CollectibleObject? collectible = sapi.World.GetItem(location) ?? (CollectibleObject?)sapi.World.GetBlock(location);
        return collectible == null ? null : new ItemStack(collectible);
    }

    private static TextCommandResult Strike(Entity? entity, double joules)
    {
        var hull = entity?.GetBehavior<EntityBehaviorVesselIntegrity>();
        if (hull == null) return TextCommandResult.Error("Not a vessel");

        hull.Strike(joules);
        return TextCommandResult.Success($"{entity!.Code} {entity.EntityId}: alive {entity.Alive}, integrity {hull.Integrity}");
    }

    private TextCommandResult Tie(Caller caller, Entity? entity, BlockPos block)
    {
        var tieable = entity?.GetBehavior<EntityBehaviorRopeTieable>();
        if (tieable == null) return TextCommandResult.Error("Not a rope-tieable entity");

        var cloths = sapi.ModLoader.GetModSystem<ClothManager>();
        var rope = ClothSystem.CreateRope(sapi, cloths, entity!.Pos.XYZ, block.ToVec3d().Add(0.5, 0.5, 0.5), null);
        rope.LastPoint.PinTo(block, new Vec3f(0.5f, 0.5f, 0.5f));
        cloths.RegisterCloth(rope);
        tieable.Attach(rope, rope.FirstPoint);
        return Ropes(caller, entity.EntityId);
    }

    private TextCommandResult Ropes(Caller caller, long id)
    {
        var cloths = sapi.ModLoader.GetModSystem<ClothManager>();
        var ropes = sapi.World.GetEntityById(id)?.GetBehavior<EntityBehaviorRopeTieable>()?.ClothIds?.value ?? [];
        return Reply(caller, JsonSerializer.Serialize(ropes.Select(cloths.GetClothSystem).Where(rope => rope != null).Select(rope => new
        {
            rope!.ClothId,
            ends = rope.Ends.Select(end => end.PinnedToBlockPos is { } block
                ? $"block {block.X} {block.Y} {block.Z}"
                : end.PinnedToEntity is { } entity ? $"entity {entity.EntityId} {entity.Code}" : "loose")
        })));
    }

    private TextCommandResult Blast(Entity? entity, int injureRadius, double distance)
    {
        if (entity == null) return TextCommandResult.Error("No such entity");

        sapi.World.CreateExplosion(entity.Pos.XYZ.Add(distance, 1, 0).AsBlockPos, EnumBlastType.EntityBlast, 0, injureRadius);
        var hull = entity.GetBehavior<EntityBehaviorVesselIntegrity>();
        return TextCommandResult.Success($"{entity.Code} {entity.EntityId}: alive {entity.Alive}" + (hull == null ? "" : $", integrity {hull.Integrity}"));
    }

    private static TextCommandResult Interact(IPlayer player, Entity? entity, string mode, string? modifier, string? box)
    {
        if (entity == null) return TextCommandResult.Error("No such entity");

        var boxes = entity.GetBehavior<EntityBehaviorSelectionBoxes>()?.selectionBoxes ?? [];
        var boxIndex = box == null ? 0 : Array.FindIndex(boxes, candidate => candidate.AttachPoint.Code == box) + 1;
        if (box != null && boxIndex == 0) return TextCommandResult.Error($"No box {box}; boxes: {string.Join(", ", boxes.Select(candidate => candidate.AttachPoint.Code))}");

        var controls = player.Entity.Controls;
        controls.CtrlKey = modifier == "ctrl";
        controls.ShiftKey = modifier == "shift";
        var interactMode = mode == "attack" ? EnumInteractMode.Attack : EnumInteractMode.Interact;
        player.Entity.EntitySelection = new EntitySelection { Entity = entity, Position = entity.Pos.XYZ, SelectionBoxIndex = boxIndex };
        if (interactMode == EnumInteractMode.Attack || entity.GetBehavior<EntityBehaviorVesselIntegrity>()?.TryPickUp(player.Entity) != true)
        {
            entity.OnInteract(player.Entity, player.InventoryManager.ActiveHotbarSlot, new Vec3d(0, entity.SelectionBox.Y2 / 2, 0), interactMode);
        }
        controls.CtrlKey = controls.ShiftKey = false;
        var hull = entity.GetBehavior<EntityBehaviorVesselIntegrity>();
        return TextCommandResult.Success($"{entity.Code} {entity.EntityId}: alive {entity.Alive}" + (hull == null ? "" : $", integrity {hull.Integrity}"));
    }

    // A command run with the console's privileges, /executeas included, replies into the server log, which reads it as a format string.
    private static TextCommandResult Reply(Caller caller, string json) =>
        TextCommandResult.Success(caller.CallerPrivileges?.Contains("*") == true ? json.Replace("{", "{{").Replace("}", "}}") : json);

    private object Here(Vec3d pos, bool teleporting)
    {
        var accessor = sapi.World.BlockAccessor;
        var blockPos = pos.AsBlockPos;
        var regionSize = accessor.RegionSize;
        var region = accessor.GetMapRegion(blockPos.X / regionSize, blockPos.Z / regionSize);
        var climate = accessor.GetClimateAt(blockPos, EnumGetClimateMode.NowValues);
        var weather = sapi.ModLoader.GetModSystem<WeatherSystemServer>();
        var wind = accessor.GetWindSpeedAt(pos);
        var sim = weather.getOrCreateWeatherSimForRegion(blockPos.X / regionSize, blockPos.Z / regionSize);

        return new
        {
            teleporting,
            y = Math.Round(pos.Y, 2),
            seaLevel = sapi.World.SeaLevel,
            oceanity = region?.OceanMap?.GetUnpaddedIntLerpedForBlockPos(blockPos.X, blockPos.Z, regionSize),
            feet = accessor.GetBlock(blockPos, BlockLayersAccess.Fluid)?.Code?.ToString(),
            head = accessor.GetBlock(blockPos.UpCopy(2), BlockLayersAccess.Fluid)?.Code?.ToString(),
            seabed = accessor.GetTerrainMapheightAt(blockPos),
            season = sapi.World.Calendar.GetSeason(blockPos).ToString(),
            temperature = climate?.Temperature,
            rainfall = climate?.Rainfall,
            wind = new { wind.X, wind.Y, wind.Z },
            precipitation = weather.GetPrecipitation(pos),
            windPattern = sim?.CurWindPattern?.config.Code,
            windStrength = sim?.CurWindPattern?.Strength,
            weatherPattern = sim?.NewWePattern?.config.Code,
            weatherEvent = sim?.CurWeatherEvent?.config.Code
        };
    }

    private TextCommandResult Entity(Caller caller, long id)
    {
        var entity = sapi.World.GetEntityById(id);
        if (entity == null) return TextCommandResult.Error($"No entity {id}");

        return Reply(caller,
            $"{{\"code\":\"{entity.Code}\",\"alive\":{entity.Alive.ToString().ToLower()}," +
            $"\"pos\":{JsonSerializer.Serialize(new { entity.Pos.X, entity.Pos.Y, entity.Pos.Z, entity.Pos.Yaw })}," +
            $"\"behaviors\":{JsonSerializer.Serialize(entity.SidedProperties.Behaviors.Select(behavior => behavior.PropertyName()))}," +
            $"\"watched\":{Strict(entity.WatchedAttributes.ToJsonToken())},\"attributes\":{Strict(entity.Attributes.ToJsonToken())}}}");
    }

    private static string Strict(string relaxedJson) => JToken.Parse(relaxedJson).ToString(Newtonsoft.Json.Formatting.None);
}
