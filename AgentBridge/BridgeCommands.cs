using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RoughSeas.SeaAndWeather;
using RoughSeas.Shipyard;
using RoughSeas.Vessels;
using RoughSeas.Water;
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
    private static readonly string[] Places = HullShape.Places.Select(place => place.Code()).ToArray();

    private static readonly (double Angle, double Reach)[] StormProbes =
        Enumerable.Range(0, 12).SelectMany(i => new[] { 0.25, 0.45, 0.65, 0.85, 1.0 }, (i, reach) => (i * Math.PI / 6, reach)).ToArray();

    private const int HoverAboveSea = 1;
    private const double NearbyGridBlocks = 5000;
    private const int WaveProbes = 12;

    private ICoreServerAPI sapi = null!;
    private DriftSwitch? drift;
    private CurrentOverride? current;
    private DriftSync? sync;
    private ChunkSettle settle = null!;
    private int suspendMs;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
        settle = new ChunkSettle(api, Mod.Logger);
        var benchDefaults = Environment.GetEnvironmentVariable("AGENT_BRIDGE_BENCH_DEFAULTS") != "off";
        if (api.ModLoader.IsModEnabled("roughseas"))
        {
            drift = new DriftSwitch { Drifting = !benchDefaults };
            current = new CurrentOverride();
            sync = api.ModLoader.GetModSystem<DriftSync>();
            api.Event.PlayerJoin += player => sync.Send(drift.Drifting, drift.WavesOnly, current.Current, player);
        }
        if (benchDefaults) TurnOffTemporalEffects(api.World.Config);
        api.Event.ServerSuspend += HoldSuspension;
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
                .WithDescription("Fly the caller to the nearest open sea within 1500 blocks, with ocean all round for a clearing of blocks (default 128), hovering just above the surface")
                .RequiresPlayer()
                .WithArgs(parsers.OptionalIntRange("clear", 0, 512, 128))
                .HandleWith(args => Sea((IServerPlayer)args.Caller.Player, (int)args[0]))
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
            .BeginSubCommand("climate")
                .WithDescription("Over the next year, how many hours a storm of a type covers each open-sea point of a grid around the caller, and in which month most: the points it visits most, first")
                .RequiresPlayer()
                .WithArgs(parsers.OptionalWord("type"), parsers.OptionalDouble("range", 20000), parsers.OptionalDouble("step", 5000), parsers.OptionalDouble("presence", 0.7), parsers.OptionalDouble("years", 1))
                .HandleWith(args => Climate((IServerPlayer)args.Caller.Player, (string?)args[0], (double)args[1], (double)args[2], (double)args[3], (double)args[4]))
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
            .BeginSubCommand("beaches")
                .WithDescription("List the places in the loaded world within this range where dry ground lies flush with the sea, LENGTH blocks along the water and DEPTH back from it: where a vessel is laid by the water. Each with its ground, rainfall and temperature, the wettest first")
                .RequiresPlayer()
                .WithArgs(parsers.OptionalIntRange("range", 16, 512, 256), parsers.OptionalIntRange("length", 1, 32, 7), parsers.OptionalIntRange("depth", 1, 32, 4))
                .HandleWith(args => Beaches(args.Caller.Player.Entity.Pos.AsBlockPos, (int)args[0], (int)args[1], (int)args[2]))
            .EndSubCommand()
            .BeginSubCommand("shores")
                .WithDescription("List the shores in the loaded world within this range whose ground at the water's edge, at the sea's level or up to two blocks over it, matches a wildcard, LENGTH blocks along open salt water: each with how high its ground stands over the water at each of the 8 blocks inland, and how far the open water runs out from it")
                .RequiresPlayer()
                .WithArgs(parsers.Word("ground wildcard"), parsers.OptionalIntRange("range", 16, 512, 256), parsers.OptionalIntRange("length", 1, 32, 5))
                .HandleWith(args => Shores(args.Caller.Player.Entity.Pos.AsBlockPos, (string)args[0], (int)args[1], (int)args[2]))
            .EndSubCommand()
            .BeginSubCommand("cliffs")
                .WithDescription("List the natural faces in the loaded world within this range that rise at least MINRISE blocks straight out of open salt water, LENGTH blocks along it: each with its ground, how high it stands at each of the 8 blocks inland, and how far the open water runs out from it")
                .RequiresPlayer()
                .WithArgs(parsers.IntRange("minrise", 3, 200), parsers.OptionalIntRange("range", 16, 512, 256), parsers.OptionalIntRange("length", 1, 32, 3))
                .HandleWith(args => Shores(args.Caller.Player.Entity.Pos.AsBlockPos, "*", (int)args[1], (int)args[2], (int)args[0], int.MaxValue))
            .EndSubCommand()
            .BeginSubCommand("weed")
                .WithDescription("Clear the plants and loose stones off the ground within this many blocks of the caller, as a scythe and a rake would: soil, rock, water and what floats on it stay")
                .RequiresPlayer()
                .WithArgs(parsers.IntRange("radius", 1, 32))
                .HandleWith(args => Weed(args.Caller.Player.Entity.Pos.AsBlockPos, (int)args[0]))
            .EndSubCommand()
            .BeginSubCommand("drift")
                .WithDescription("Whether vessels drift with the current and the waves, steered or not (waves: the waves alone); off on every bench start, so they hold still")
                .WithArgs(parsers.OptionalWordRange("state", "on", "off", "waves"))
                .HandleWith(args => Drift(args[0] as string))
            .EndSubCommand()
            .BeginSubCommand("current")
                .WithDescription("Set one current everywhere in place of the sea's, SPEED m/s toward a yaw in degrees as the camera's (0 toward +Z, 90 toward +X), as a river carries a vessel; 'off' gives the sea's back")
                .WithArgs(parsers.Word("speed or off"), parsers.OptionalDouble("yaw", 0))
                .HandleWith(args => Current((string)args[0], (double)args[1]))
            .EndSubCommand()
            .BeginSubCommand("set")
                .WithDescription("Set an entity's watched attribute, tree/key for one inside a tree: int, float, bool or string by the value's form, a double with a trailing d (0.05d), while a double or float stays what it is")
                .WithArgs(parsers.Long("id"), parsers.Word("key"), parsers.Word("value"))
                .HandleWith(args => Set(sapi.World.GetEntityById((long)args[0]), (string)args[1], (string)args[2]))
            .EndSubCommand()
            .BeginSubCommand("ride")
                .WithDescription("Spawn a boat (or any seatable entity) at the caller, at the surface over water, and seat the caller in it")
                .RequiresPlayer()
                .WithArgs(parsers.Word("entity code"))
                .HandleWith(args => Ride(args.Caller.Player, (string)args[0]))
            .EndSubCommand()
            .BeginSubCommand("spawn")
                .WithDescription("Spawn an entity at a position facing a yaw in degrees and reply with its id; a vessel over water lies on the surface. With a key and value, that watched attribute is set before it enters the world, as `set` sets it: a vessel spawned at a stage of her build, as the game spawns her")
                .WithArgs(parsers.Word("entity code"), parsers.WorldPosition("position"), parsers.OptionalDouble("yaw", 0), parsers.OptionalWord("key"), parsers.OptionalWord("value"))
                .HandleWith(args => Spawn((string)args[0], (Vec3d)args[1], (double)args[2], args[3] as string, args[4] as string))
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
            .BeginSubCommand("dress")
                .WithDescription("Put a piece of clothing on the caller, in the outfit slot that takes it, replacing what was there")
                .RequiresPlayer()
                .WithArgs(parsers.Word("item code"))
                .HandleWith(args => Dress(args.Caller.Player, (string)args[0]))
            .EndSubCommand()
            .BeginSubCommand("hold")
                .WithDescription("Put an item into the caller's active hotbar slot, held in the hand")
                .RequiresPlayer()
                .WithArgs(parsers.Word("item code"))
                .HandleWith(args => Hold(args.Caller.Player, (string)args[0]))
            .EndSubCommand()
            .BeginSubCommand("craft")
                .WithDescription("Lay stacks in the caller's crafting grid, slot by slot from the top left, and take what it makes, as a click on the output does: CODE, CODE*N for a stack of N, hand for the held stack, _ for an empty slot. Ingredients the recipe gives back go to the inventory, as in play")
                .RequiresPlayer()
                .WithArgs(parsers.All("stacks"))
                .HandleWith(args => Craft(args.Caller.Player, ((string)args[0]).Split(' ', StringSplitOptions.RemoveEmptyEntries)))
            .EndSubCommand()
            .BeginSubCommand("pot")
                .WithDescription("Hold a cooked pot of something cooked in it, as the firepit leaves it: CONTENT, PORTIONS of it a serving, SERVINGS (glueportion-pitch-hot 100 1 is a pot of hot pitch)")
                .RequiresPlayer()
                .WithArgs(parsers.Word("content code"), parsers.Int("portions a serving"), parsers.Double("servings"), parsers.OptionalWord("pot code"))
                .HandleWith(args => Pot(args.Caller.Player, (string)args[0], (int)args[1], (double)args[2], (string?)args[3] ?? "dirtyclaypot-brown-cooked"))
            .EndSubCommand()
            .BeginSubCommand("drop")
                .WithDescription("Drop an item on the ground at a position, at rest, as a dropped stack lies")
                .WithArgs(parsers.Word("item code"), parsers.WorldPosition("position"))
                .HandleWith(args => Drop((string)args[0], (Vec3d)args[1]))
            .EndSubCommand()
            .BeginSubCommand("pack")
                .WithDescription("Fill every container fitted to an entity's accessory slots to the brim, each of its slots a full stack of CODE")
                .WithArgs(parsers.Long("id"), parsers.Word("item code"))
                .HandleWith(args => Pack(sapi.World.GetEntityById((long)args[0]), (string)args[1]))
            .EndSubCommand()
            .BeginSubCommand("tie")
                .WithDescription("Tie a rope-tieable entity to a block with a vanilla rope, as a player with rope in hand would; X Y Z absolute; METRES sets its length as the mouse wheel does (1.5 to 10), else it is as long as the gap")
                .WithArgs(parsers.Long("id"), parsers.Int("x"), parsers.Int("y"), parsers.Int("z"), parsers.OptionalDouble("metres", 0))
                .HandleWith(args => Tie(args.Caller, sapi.World.GetEntityById((long)args[0]), new BlockPos((int)args[1], (int)args[2], (int)args[3]), (double)args[4]))
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
                .HandleWith(args => Dismount(args.Caller.Player.Entity)
                    ? TextCommandResult.Success("Dismounted")
                    : TextCommandResult.Error("Not seated, or the seat would not let go"))
            .EndSubCommand()
            .BeginSubCommand("strike")
                .WithDescription("Strike a vessel's hull at a place (its port side by default) with an impact of this many joules; the hull rolls against it as it does for any impact, and an outrigger's float takes it on its side")
                .WithArgs(parsers.Long("id"), parsers.Double("joules"), parsers.OptionalWordRange("place", Places))
                .HandleWith(args => Strike(sapi.World.GetEntityById((long)args[0]), (double)args[1], args[2] as string ?? "portside"))
            .EndSubCommand()
            .BeginSubCommand("lag")
                .WithDescription("Stall the server's main thread for this many milliseconds on each of its next ticks, as a lag spike or a heavy mod does, world time running on")
                .WithArgs(parsers.IntRange("ms", 1, 60000), parsers.OptionalIntRange("ticks", 1, 1000, 1))
                .HandleWith(args => Lag((int)args[0], (int)args[1]))
            .EndSubCommand()
            .BeginSubCommand("suspend")
                .WithDescription("Autosave and stay suspended this many milliseconds longer, world time paused, as a heavy autosave does")
                .WithArgs(parsers.IntRange("ms", 1, 60000))
                .HandleWith(args => Suspend((int)args[0], args.Caller))
            .EndSubCommand()
            .BeginSubCommand("blast")
                .WithDescription("Set off an explosion that harms entities but no blocks, this many blocks from an entity")
                .WithArgs(parsers.Long("id"), parsers.Int("injureRadius"), parsers.OptionalDouble("distance"))
                .HandleWith(args => Blast(sapi.World.GetEntityById((long)args[0]), (int)args[1], (double)args[2]))
            .EndSubCommand()
            .BeginSubCommand("interact")
                .WithDescription("Attack or interact with an entity using the held item, as the server does when the player clicks it; on a vessel, at a place of its hull when one is given")
                .RequiresPlayer()
                .WithArgs(parsers.Long("id"), parsers.WordRange("mode", "attack", "interact"), parsers.OptionalWordRange("modifier", "none", "ctrl", "shift"), parsers.OptionalWord("box"), parsers.OptionalWordRange("place", Places))
                .HandleWith(args => Interact(args.Caller.Player, sapi.World.GetEntityById((long)args[0]), (string)args[1], args[2] as string, args[3] as string, args[4] as string))
            .EndSubCommand();
    }

    public override void Dispose()
    {
        drift?.Dispose();
        current?.Dispose();
    }

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
        if (state != null)
        {
            drift.Drifting = state != "off";
            drift.WavesOnly = state == "waves";
            sync?.Send(drift.Drifting, drift.WavesOnly, current?.Current);
        }
        if (drift.WavesOnly) return TextCommandResult.Success("Vessels drift with the waves alone, not the current");
        return TextCommandResult.Success(drift.Drifting
            ? "Vessels drift with the current and the waves, steered or not"
            : "Vessels hold still; /bridge drift on lets them drift");
    }

    private TextCommandResult Current(string speed, double yaw)
    {
        if (current == null) return TextCommandResult.Error("Rough Seas is not loaded; there is no current to set");
        if (speed == "off")
        {
            current.Clear();
            sync?.Send(drift?.Drifting ?? true, drift?.WavesOnly == true, null);
            return TextCommandResult.Success("The sea's own currents flow again");
        }
        if (!double.TryParse(speed, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var metresPerSecond))
            return TextCommandResult.Error("Give the current's speed in m/s, or off");

        current.Set(metresPerSecond, yaw);
        sync?.Send(drift?.Drifting ?? true, drift?.WavesOnly == true, current.Current);
        return TextCommandResult.Success($"A current of {metresPerSecond:F1} m/s flows toward yaw {yaw:F0}° everywhere; vessels ride it once /bridge drift is on");
    }

    private TextCommandResult Lag(int ms, int ticks)
    {
        long listener = 0;
        int left = ticks;
        listener = sapi.Event.RegisterGameTickListener(_ =>
        {
            Thread.Sleep(ms);
            if (--left > 0) return;
            sapi.Event.UnregisterGameTickListener(listener);
            Mod.Logger.Debug("[bridge] lagged the server {0} ms on each of {1} ticks", ms, ticks);
        }, 0);
        return TextCommandResult.Success($"The server stalls for {ms} ms on each of its next {ticks} ticks");
    }

    private TextCommandResult Suspend(int ms, Caller caller)
    {
        suspendMs = ms;
        sapi.ChatCommands.ExecuteUnparsed("/autosavenow", new TextCommandCallingArgs { Caller = caller });
        return TextCommandResult.Success($"The server autosaves and stays suspended {ms} ms longer");
    }

    private EnumSuspendState HoldSuspension()
    {
        if (suspendMs == 0) return EnumSuspendState.Ready;
        Thread.Sleep(suspendMs);
        Mod.Logger.Debug("[bridge] held the suspension for {0} ms", suspendMs);
        suspendMs = 0;
        return EnumSuspendState.Ready;
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
            var when = sea.Clock.Hours + ahead;
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

    private TextCommandResult Climate(IServerPlayer player, string? type, double range, double step, double presence, double years)
    {
        if (type is "any") type = null;
        var sea = sapi.ModLoader.GetModSystem<SeaWeatherSystem>().Model;
        var calendar = sapi.World.Calendar;
        var year = calendar.DaysPerYear * calendar.HoursPerDay;
        var pos = player.Entity.Pos;
        var reach = (int)(range / step);
        var (x, z, now) = (pos.X, pos.Z, sea.Clock.Hours);
        Task.Run(() =>
        {
            var reply = Count(sea, sapi.ModLoader.GetModSystem<SeaWeatherSystem>().Config.Storms, type, x, z, now, year, years, reach, step, presence,
                pointZ => calendar.OnGetLatitude?.Invoke(pointZ) >= 0);
            sapi.Event.EnqueueMainThreadTask(() => player.SendMessage(GlobalConstants.GeneralChatGroup, reply, EnumChatType.CommandSuccess), "bridge climate");
        });
        return TextCommandResult.Success($"Counting {type ?? "storm"} hours over the next {years} years on {(2 * reach + 1) * (2 * reach + 1)} points; the answer comes in chat");
    }

    private static string Count(SeaModel sea, StormTypeConfig[] types, string? type, double x, double z, double now, double year, double years, int reach, double step, double presence,
        System.Func<double, bool> northern)
    {
        const int shown = 8;
        const double gale = 17.2;
        var points = Enumerable.Range(-reach, 2 * reach + 1)
            .SelectMany(dx => Enumerable.Range(-reach, 2 * reach + 1).Select(dz => (X: x + dx * step, Z: z + dz * step)))
            .Where(point => sea.IsSea(point.X, point.Z))
            .Select(point =>
            {
                var months = new int[12];
                var galeMonths = new int[12];
                var strongest = 0.0;
                var passed = new System.Collections.Generic.HashSet<Storm>();
                var shift = northern(point.Z) ? 0 : 6;
                for (var hour = 0.0; hour < year * years; hour++)
                {
                    var when = now + hour;
                    var month = (int)(when / year % 1 * 12);
                    var season = (month + shift) % 12;
                    var wind = sea.Weather.Wind(point.X, point.Z, when);
                    strongest = Math.Max(strongest, wind.Speed);
                    if (wind.Speed >= gale) galeMonths[season]++;
                    var here = sea.Weather.Storms.Near(point.X, point.Z, when)
                        .Where(storm => type == null || storm.Code == type)
                        .Select(storm => (Storm: storm, storm.InfluenceAt(point.X, point.Z, when).Presence))
                        .Where(storm => storm.Presence > 0)
                        .ToArray();
                    foreach (var storm in here) passed.Add(storm.Storm);
                    if (here.Length > 0 && here.Max(storm => storm.Presence) >= presence) months[month]++;
                }
                return (point.X, point.Z, Hours: months.Sum(), Month: Array.IndexOf(months, months.Max()) + 1, Peak: months.Max(),
                    GaleHours: galeMonths.Sum() / years, Winter: (galeMonths[11] + galeMonths[0] + galeMonths[1]) / (years * year / 4), Summer: (galeMonths[5] + galeMonths[6] + galeMonths[7]) / (years * year / 4), Strongest: strongest, Passed: passed.Count / years, PeakMps: passed.Count > 0 ? passed.Max(storm => storm.PeakMps) : 0);
            })
            .OrderByDescending(point => point.Hours)
            .ToArray();
        if (points.Length == 0) return "No open sea on the grid";
        var mean = string.Format(CultureInfo.InvariantCulture, "Mean over {0} points: wind at force 8+ {1:P1} of the year, {2:P1} of winter, {3:P1} of summer; {4:F1} storms passed a year",
            points.Length, points.Average(point => point.GaleHours) / year, points.Average(point => point.Winter), points.Average(point => point.Summer), points.Average(point => point.Passed));
        return mean + "\n" + string.Join("\n", points.Take(shown).Select(point =>
        {
            var (celsius, rainfall) = sea.Weather.Climate.At(point.X, point.Z);
            var yearStart = Math.Floor(now / year) * year;
            var winterGradient = sea.Weather.Storms.Gradient(point.X, point.Z, yearStart + year * 0.5 / 12) ?? double.NaN;
            var summerGradient = sea.Weather.Storms.Gradient(point.X, point.Z, yearStart + year * 6.5 / 12) ?? double.NaN;
            var odds = string.Join("; ", types.Where(kind => type == null || kind.Code == type).Select(kind => string.Format(CultureInfo.InvariantCulture,
                "{0}: temperature {1:F2} x rain {2:F2} x months {3:F2} avg",
                kind.Code, kind.Temperature?.Weight(celsius) ?? 1, Math.Pow(rainfall, kind.RainfallExponent), kind.MonthlyWeight.Average())));
            return string.Format(CultureInfo.InvariantCulture,
                "{0:F0} {1:F0}, {2:F1} °C, rain {3:F2}, gradient {15:F1} °C/1000 km in January and {16:F1} in July ({4}): {5} passed, peaks to {6:F0} m/s, here at most {7:F1} m/s; {8} h at force 8+ ({9:P1} of the year, {10:P1} of winter, {11:P1} of summer); {12} h at {13:P0} presence, most in month {14}",
                point.X, point.Z, celsius, rainfall, odds, point.Passed, point.PeakMps, point.Strongest, point.GaleHours, point.GaleHours / year, point.Winter, point.Summer, point.Hours, presence, point.Month, winterGradient, summerGradient);
        }));
    }

    private TextCommandResult Storm(IServerPlayer player, double range, double waves)
    {
        var sea = sapi.ModLoader.GetModSystem<SeaWeatherSystem>().Model;
        var pos = player.Entity.Pos;
        var probed = sea.StormsWithin(pos.X, pos.Z, range)
            .SelectMany(storm =>
            {
                var (x, z) = storm.CentreAt(sea.Clock.Hours);
                return StormProbes.Select(probe => (x: x + Math.Cos(probe.Angle) * probe.Reach * storm.Radius, z: z + Math.Sin(probe.Angle) * probe.Reach * storm.Radius));
            })
            .Where(point => sea.IsSea(point.x, point.z))
            .OrderByDescending(point => sea.StormsAt(point.x, point.z, sea.Clock.Hours).Speed)
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
                var (x, z) = storm.CentreAt(sea.Clock.Hours);
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
            var centre = sapi.World.DefaultSpawnPosition;
            return TextCommandResult.Success(string.Format(CultureInfo.InvariantCulture, "Beaufort {0}, waves {1:F1} m at {2:F0} {3:F0}; '/tp {2:F0} {4} {3:F0}' sails there with the vessel",
                force, chosen.Value.state.WaveHeight, target.X - centre.X, target.Z - centre.Z, target.Y));
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
        var now = sea.Clock.Hours;
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

    private TextCommandResult Sea(IServerPlayer player, int clear)
    {
        const int range = 1500;
        const int step = 16;
        var origin = player.Entity.Pos.AsBlockPos;
        var reach = clear / step;
        var around = Enumerable.Range(-reach, 2 * reach + 1)
            .SelectMany(i => Enumerable.Range(-reach, 2 * reach + 1), (i, j) => (x: i * step, z: j * step))
            .Where(offset => offset.x * offset.x + offset.z * offset.z <= clear * clear)
            .ToArray();

        bool Clear(int x, int z) => around.All(offset => OpenSea(x + offset.x, z + offset.z));

        var sea = Enumerable.Range(-range / step, 2 * range / step + 1)
            .SelectMany(i => Enumerable.Range(-range / step, 2 * range / step + 1), (i, j) => (x: origin.X + i * step, z: origin.Z + j * step))
            .Where(point => OpenSea(point.x, point.z))
            .OrderBy(point => (point.x - origin.X) * (long)(point.x - origin.X) + (point.z - origin.Z) * (long)(point.z - origin.Z))
            .Where(point => Clear(point.x, point.z))
            .Select(point => ((int x, int z)?)point)
            .FirstOrDefault();
        if (sea == null) return TextCommandResult.Error($"No open sea {clear} blocks clear in the generated regions within {range} blocks");

        var flown = Fly(player, new Vec3d(sea.Value.x + 0.5, sapi.World.SeaLevel + HoverAboveSea, sea.Value.z + 0.5));
        return TextCommandResult.Success($"{flown.StatusMessage}, over open sea {clear} blocks clear");
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
                && OpenWater.At(accessor, new BlockPos(x, ground, z, 0)).Id == 0;
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

    private TextCommandResult Beaches(BlockPos origin, int range, int length, int depth)
    {
        const int shown = 12;
        var accessor = sapi.World.BlockAccessor;
        var surface = sapi.World.SeaLevel - 1;
        var centre = sapi.World.DefaultSpawnPosition;

        int Ground(int x, int z) => accessor.GetTerrainMapheightAt(new BlockPos(x, 0, z, 0));
        bool Loaded(int x, int z) => accessor.GetChunkAtBlockPos(new BlockPos(x, surface, z, 0)) != null;
        bool Flush(int x, int z) => Loaded(x, z) && Ground(x, z) == surface
            && OpenWater.At(accessor, new BlockPos(x, surface + 1, z, 0)).Id == 0;
        bool Water(int x, int z) => Loaded(x, z) && Ground(x, z) < surface
            && OpenWater.At(accessor, new BlockPos(x, surface, z, 0)).IsLiquid();

        (int dx, int dz, string name)[] seaward = [(0, 1, "south"), (0, -1, "north"), (1, 0, "east"), (-1, 0, "west")];

        string? Facing(int x, int z) => seaward.FirstOrDefault(sea =>
        {
            var (alongX, alongZ) = (sea.dz, sea.dx);
            return Enumerable.Range(0, length).All(i => Enumerable.Range(0, depth).All(j =>
                       Flush(x + i * alongX - j * sea.dx, z + i * alongZ - j * sea.dz)))
                   && Enumerable.Range(0, length).All(i => Water(x + i * alongX + sea.dx, z + i * alongZ + sea.dz));
        }).name;

        var found = new List<(int x, int z, string sea)>();
        for (var i = -range; i <= range; i += 2)
        for (var j = -range; j <= range; j += 2)
        {
            var (x, z) = (origin.X + i, origin.Z + j);
            if (found.Any(other => Math.Abs(other.x - x) < 2 * length && Math.Abs(other.z - z) < 2 * length)) continue;
            if (Facing(x, z) is { } sea) found.Add((x, z, sea));
        }
        if (found.Count == 0) return TextCommandResult.Error($"No ground flush with the sea, {length} along and {depth} deep, within {range} blocks of loaded world");

        var lines = found
            .Select(spot =>
            {
                var at = new BlockPos(spot.x, surface, spot.z, 0);
                var climate = accessor.GetClimateAt(at, EnumGetClimateMode.WorldGenValues);
                var water = OpenWater.At(accessor, at.AddCopy(BlockFacing.FromCode(spot.sea))).Code;
                return (spot, rain: climate?.WorldgenRainfall ?? 0, climate?.Temperature, ground: accessor.GetBlock(at).Code, water);
            })
            .OrderByDescending(beach => beach.rain)
            .Take(shown)
            .Select(beach => $"{beach.spot.x - centre.X:0} {surface + 1} {beach.spot.z - centre.Z:0}: {beach.ground}, {beach.water} to the {beach.spot.sea}, rainfall {beach.rain:0.00}, {beach.Temperature:0} °C");
        return TextCommandResult.Success($"{found.Count} beaches, the wettest first:\n{string.Join("\n", lines)}");
    }

    private TextCommandResult Shores(BlockPos origin, string wildcard, int range, int length, int lowest = 0, int highest = 2)
    {
        const int shown = 16;
        const int inland = 8;
        const int farthest = 64;
        var pattern = new AssetLocation(wildcard);
        var accessor = sapi.World.BlockAccessor;
        var surface = sapi.World.SeaLevel - 1;
        var centre = sapi.World.DefaultSpawnPosition;

        bool Loaded(int x, int z) => accessor.GetChunkAtBlockPos(new BlockPos(x, surface, z, 0)) != null;
        int Ground(int x, int z) => accessor.GetTerrainMapheightAt(new BlockPos(x, 0, z, 0));
        bool Sea(int x, int z) => Loaded(x, z) && Ground(x, z) < surface
            && OpenWater.At(accessor, new BlockPos(x, surface, z, 0)).Code?.Path.StartsWith("saltwater") == true;
        bool Edge(int x, int z) => Loaded(x, z) && Ground(x, z) - surface >= lowest && Ground(x, z) - surface <= highest
            && accessor.GetBlock(new BlockPos(x, Ground(x, z), z, 0)).WildCardMatch(pattern);

        (int dx, int dz, string name)[] seaward = [(0, 1, "south"), (0, -1, "north"), (1, 0, "east"), (-1, 0, "west")];

        var found = new List<(int x, int z, (int dx, int dz, string name) sea)>();
        for (var i = -range; i <= range; i += 2)
        for (var j = -range; j <= range; j += 2)
        {
            var (x, z) = (origin.X + i, origin.Z + j);
            if (found.Any(other => Math.Abs(other.x - x) < 2 * length && Math.Abs(other.z - z) < 2 * length)) continue;
            var facing = seaward.FirstOrDefault(sea => Enumerable.Range(0, length)
                .All(k => Edge(x + k * sea.dz, z + k * sea.dx) && Sea(x + k * sea.dz + sea.dx, z + k * sea.dx + sea.dz)));
            if (facing.name != null) found.Add((x, z, facing));
        }
        if (found.Count == 0) return TextCommandResult.Error($"No {wildcard} at the edge of the open sea, {length} along, within {range} blocks of loaded world");

        var lines = found
            .Select(spot =>
            {
                var (dx, dz, name) = spot.sea;
                var rise = Enumerable.Range(0, inland).Select(k => Ground(spot.x - k * dx, spot.z - k * dz) - surface);
                var open = Enumerable.Range(1, farthest).TakeWhile(k => Sea(spot.x + k * dx, spot.z + k * dz)).Count();
                var same = Enumerable.Range(0, inland).Count(k =>
                    accessor.GetBlock(new BlockPos(spot.x - k * dx, Ground(spot.x - k * dx, spot.z - k * dz), spot.z - k * dz, 0)).WildCardMatch(pattern));
                return (spot, rise: string.Join(" ", rise), open, same, ground: accessor.GetBlock(new BlockPos(spot.x, Ground(spot.x, spot.z), spot.z, 0)).Code);
            })
            .OrderByDescending(shore => shore.open)
            .ThenByDescending(shore => shore.same)
            .Take(shown)
            .Select(shore => $"{shore.spot.x - centre.X:0} {surface + 1} {shore.spot.z - centre.Z:0}: {shore.ground}, sea to the {shore.spot.sea.name} open for {shore.open}{(shore.open == farthest ? "+" : "")} blocks; blocks over the sea inland: {shore.rise}, {shore.same} of them {pattern.Path}");
        return TextCommandResult.Success($"{found.Count} shores, the most open first:\n{string.Join("\n", lines)}");
    }

    private TextCommandResult Weed(BlockPos origin, int radius)
    {
        var accessor = sapi.World.GetBlockAccessorBulkUpdate(true, true);
        var cleared = 0;
        for (var x = -radius; x <= radius; x++)
        for (var z = -radius; z <= radius; z++)
        for (var y = -radius; y <= radius; y++)
        {
            var pos = origin.AddCopy(x, y, z);
            var block = accessor.GetBlock(pos);
            if (block.BlockMaterial != EnumBlockMaterial.Plant && block.Code?.Path.StartsWith("loose") != true) continue;
            if (OpenWater.At(accessor, pos.DownCopy()).IsLiquid()) continue;

            accessor.SetBlock(0, pos);
            cleared++;
        }
        accessor.Commit();
        return TextCommandResult.Success($"Cleared {cleared} plants and stones within {radius} blocks");
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

    private static bool Dismount(EntityAgent rider)
    {
        if (rider.MountedOn is not { } seat) return false;
        seat.DoTeleportOnUnmount = false;
        bool left = rider.TryUnmount();
        seat.DoTeleportOnUnmount = true;
        return left;
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
        var centre = sapi.World.DefaultSpawnPosition;
        return TextCommandResult.Success($"Hovering at {position.X - centre.X:0.#} {position.Y:0.#} {position.Z - centre.Z:0.#} (flight {flight})");
    }

    private static TextCommandResult Set(Entity? entity, string key, string value)
    {
        if (entity == null) return TextCommandResult.Error("No such entity");

        var path = key.Split('/');
        ITreeAttribute attributes = entity.WatchedAttributes;
        foreach (var tree in path[..^1]) attributes = attributes.GetOrAddTreeAttribute(tree);

        var leaf = path[^1];
        var asDouble = value.EndsWith('d') && double.TryParse(value[..^1], CultureInfo.InvariantCulture, out _);
        if (asDouble) value = value[..^1];
        var number = double.TryParse(value, CultureInfo.InvariantCulture, out var parsed) ? parsed : (double?)null;
        switch (attributes[leaf])
        {
            case DoubleAttribute when number is { } d: attributes.SetDouble(leaf, d); break;
            case FloatAttribute when number is { } f: attributes.SetFloat(leaf, (float)f); break;
            default:
                if (number is { } exact && asDouble) attributes.SetDouble(leaf, exact);
                else if (int.TryParse(value, CultureInfo.InvariantCulture, out var whole)) attributes.SetInt(leaf, whole);
                else if (number is { } single) attributes.SetFloat(leaf, (float)single);
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

        return Mount(player, Spawn(type, player.Entity.Pos.XYZ, player.Entity.Pos.Yaw, afloat: true));
    }

    private TextCommandResult Spawn(string code, Vec3d position, double yaw, string? key, string? value)
    {
        var type = sapi.World.GetEntityType(new AssetLocation(code));
        if (type == null) return TextCommandResult.Error($"No entity type {code}");
        if (key != null && value == null) return TextCommandResult.Error($"No value to set {key} to");

        var entity = Spawn(type, position, (float)(yaw * GameMath.DEG2RAD), afloat: false, key == null ? null : entity => Set(entity, key, value!));
        return TextCommandResult.Success($"Spawned {entity.Code} {entity.EntityId}");
    }

    private Entity Spawn(EntityProperties type, Vec3d position, float yaw, bool afloat, Action<Entity>? prepare = null)
    {
        var entity = sapi.World.ClassRegistry.CreateEntity(type);
        var y = afloat || entity is EntityBoat ? Surface(position) : position.Y;
        entity.Pos.SetPos(position.X, y, position.Z);
        entity.Pos.Yaw = yaw;
        prepare?.Invoke(entity);
        sapi.World.SpawnEntity(entity);
        return entity;
    }

    private double Surface(Vec3d position)
    {
        var accessor = sapi.World.BlockAccessor;
        var below = position.AsBlockPos;
        while (below.Y > 0 && accessor.GetBlock(below, BlockLayersAccess.FluidOrSolid).Id == 0) below.Y--;
        return OpenWater.At(accessor, below).IsLiquid() ? below.Y : position.Y;
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

    private TextCommandResult Dress(IPlayer player, string code)
    {
        var stack = Stack(code);
        if (stack == null) return TextCommandResult.Error($"No item or block {code}");

        var slot = player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName).FirstOrDefault(slot => slot.CanHold(new DummySlot(stack)));
        if (slot == null) return TextCommandResult.Error($"No outfit slot takes {code}");

        slot.Itemstack = stack;
        slot.MarkDirty();
        return TextCommandResult.Success($"Dressed in {code}");
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

    private TextCommandResult Craft(IPlayer player, string[] stacks)
    {
        var grid = player.InventoryManager.GetOwnInventory(GlobalConstants.craftingInvClassName);
        var size = grid.Count - 1;
        if (stacks.Length > size) return TextCommandResult.Error($"The grid holds {size} stacks");
        if (Enumerable.Range(0, size).Any(i => !grid[i].Empty)) return TextCommandResult.Error("The crafting grid is not empty: take its stacks out first");

        var hand = player.InventoryManager.ActiveHotbarSlot;
        if (stacks.FirstOrDefault(code => code is not ("_" or "hand") && Stack(code.Split('*')[0]) == null) is { } unknown) return TextCommandResult.Error($"No item or block {unknown}");
        if (stacks.FirstOrDefault(code => code.Split('*') is [_, var count] && !(int.TryParse(count, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > 0)) is { } miscounted) return TextCommandResult.Error($"Not a count of items: {miscounted}");
        if (stacks.Contains("hand") && hand.Empty) return TextCommandResult.Error("The hand is empty");

        for (int i = 0; i < stacks.Length; i++)
        {
            if (stacks[i] == "_") continue;

            ItemStack stack;
            if (stacks[i] == "hand")
            {
                stack = hand.TakeOutWhole();
                hand.MarkDirty();
            }
            else
            {
                var parts = stacks[i].Split('*');
                stack = Stack(parts[0])!;
                if (parts.Length > 1) stack.StackSize = int.Parse(parts[1], CultureInfo.InvariantCulture);
            }
            grid[i].Itemstack = stack;
            grid[i].MarkDirty();
        }

        var output = grid[size];
        var crafted = new DummySlot();
        var before = Carried(player);
        if (!output.Empty)
        {
            var op = new ItemStackMoveOperation(sapi.World, EnumMouseButton.Left, 0, EnumMergePriority.AutoMerge, output.StackSize) { ActingPlayer = player };
            output.TryPutInto(crafted, ref op);
        }
        var givenBack = Carried(player).Select(pair => (code: pair.Key, count: pair.Value - before.GetValueOrDefault(pair.Key))).Where(pair => pair.count > 0).Select(pair => $"{pair.count}x{pair.code}").ToList();
        var left = Enumerable.Range(0, size).Select(i => grid[i]).Where(slot => !slot.Empty).Select(slot => Describe(slot.Itemstack)).ToList();
        foreach (var slot in Enumerable.Range(0, size).Select(i => grid[i]).Where(slot => !slot.Empty))
        {
            var rest = slot.TakeOutWhole();
            if (!player.InventoryManager.TryGiveItemstack(rest, true)) sapi.World.SpawnItemEntity(rest, player.Entity.Pos.XYZ);
            slot.MarkDirty();
        }
        if (crafted.Empty) return TextCommandResult.Error($"Nothing crafted; the grid held {string.Join(", ", left)}");

        var made = Describe(crafted.Itemstack);
        if (!player.InventoryManager.TryGiveItemstack(crafted.Itemstack, true)) sapi.World.SpawnItemEntity(crafted.Itemstack, player.Entity.Pos.XYZ);
        return TextCommandResult.Success($"Crafted {made}; given back: {(givenBack.Count == 0 ? "nothing" : string.Join(", ", givenBack))}; left in the grid: {(left.Count == 0 ? "nothing" : string.Join(", ", left))}");
    }

    private static string Describe(ItemStack stack) => $"{stack.StackSize}x{stack.Collectible.Code} {stack.Attributes.ToJsonToken()}";

    private static Dictionary<AssetLocation, int> Carried(IPlayer player) =>
        new[] { GlobalConstants.hotBarInvClassName, GlobalConstants.backpackInvClassName }
            .SelectMany(name => player.InventoryManager.GetOwnInventory(name) ?? Enumerable.Empty<ItemSlot>())
            .Where(slot => !slot.Empty)
            .GroupBy(slot => slot.Itemstack.Collectible.Code)
            .ToDictionary(group => group.Key, group => group.Sum(slot => slot.StackSize));

    private TextCommandResult Pot(IPlayer player, string content, int portions, double servings, string pot)
    {
        if (Stack(pot) is not { Collectible: IBlockMealContainer meal } held) return TextCommandResult.Error($"No cooked pot {pot}");
        if (Stack(content) is not { } cooked) return TextCommandResult.Error($"No item or block {content}");

        cooked.StackSize = portions;
        meal.SetContents(null, held, [cooked], (float)servings);
        var slot = player.InventoryManager.ActiveHotbarSlot;
        slot.Itemstack = held;
        slot.MarkDirty();
        return TextCommandResult.Success($"Holding {pot} with {servings} servings of {portions} {content}");
    }

    private TextCommandResult Drop(string code, Vec3d position)
    {
        var stack = Stack(code);
        if (stack == null) return TextCommandResult.Error($"No item or block {code}");

        var dropped = sapi.World.SpawnItemEntity(stack, position, new Vec3d());
        return TextCommandResult.Success($"Dropped {code}: entity {dropped.EntityId}");
    }

    private TextCommandResult Pack(Entity? entity, string code)
    {
        if (entity?.GetBehavior<EntityBehaviorRideableAccessories>() is not { } accessories) return TextCommandResult.Error("No entity with accessories");
        if (Stack(code) is not { } stack) return TextCommandResult.Error($"No item or block {code}");

        var packed = new List<string>();
        for (int i = 0; i < accessories.Inventory.Count; i++)
        {
            var bagStack = accessories.Inventory[i].Itemstack;
            if (bagStack?.Collectible.GetCollectibleInterface<IHeldBag>() is not { } bag) continue;

            var slots = bag.GetOrCreateSlots(bagStack, accessories.Inventory, i, sapi.World);
            foreach (var slot in slots)
            {
                slot.Itemstack = stack.Clone();
                slot.Itemstack.StackSize = stack.Collectible.MaxStackSize;
                bag.Store(bagStack, slot);
            }
            accessories.Inventory[i].MarkDirty();
            packed.Add($"{bagStack.Collectible.Code} in slot {i}: {slots.Count} stacks");
        }
        accessories.storeInv();
        return TextCommandResult.Success(packed.Count == 0 ? "No container to pack" : string.Join("; ", packed));
    }

    private ItemStack? Stack(string code)
    {
        var location = new AssetLocation(code);
        CollectibleObject? collectible = sapi.World.GetItem(location) ?? (CollectibleObject?)sapi.World.GetBlock(location);
        return collectible == null ? null : new ItemStack(collectible);
    }

    private static TextCommandResult Strike(Entity? entity, double joules, string place)
    {
        var hull = entity?.GetBehavior<EntityBehaviorVesselIntegrity>();
        if (hull == null) return TextCommandResult.Error("Not a vessel");

        hull.Impact(joules, Enum.Parse<HullPlace>(place, ignoreCase: true));
        return TextCommandResult.Success(HullState(entity!));
    }

    private static string HullState(Entity entity)
    {
        var hull = entity.GetBehavior<EntityBehaviorVesselIntegrity>();
        if (hull == null) return $"{entity.Code} {entity.EntityId}: alive {entity.Alive}";

        var afloat = hull.Afloat;
        var cracks = hull.Model is CrackingHull cracking
            ? ", cracks " + string.Join(" ", HullShape.Places.Select(place => $"{place.Code()} {cracking.Crack(place):F2}"))
            : "";
        var lashing = entity.GetBehavior<EntityBehaviorFloatLashing>() is { } floated ? $", float lashing {floated.Condition:F2}" : "";
        return $"{entity.Code} {entity.EntityId}: alive {entity.Alive}, integrity {hull.Integrity:F2}{cracks}{lashing}, " +
            $"{hull.Aboard.Total:F0} kg aboard, draft {afloat.DraftMetres:F3} m, freeboard {afloat.FreeboardMetres:F3} m, sunk {afloat.Sunk}";
    }

    private TextCommandResult Tie(Caller caller, Entity? entity, BlockPos block, double metres)
    {
        var tieable = entity?.GetBehavior<EntityBehaviorRopeTieable>();
        if (tieable == null) return TextCommandResult.Error("Not a rope-tieable entity");

        var cloths = sapi.ModLoader.GetModSystem<ClothManager>();
        var rope = ClothSystem.CreateRope(sapi, cloths, entity!.Pos.XYZ, block.ToVec3d().Add(0.5, 0.5, 0.5), null);
        if (metres > 0)
        {
            if (metres < 1.5 || metres > 10) return TextCommandResult.Error("A vanilla rope is 1.5 to 10 m long, as vanilla counts its points");

            var points = 0;
            rope.WalkPoints(_ => points++);
            var change = (int)Math.Round(metres * rope.Resolution) - points;
            // vanilla's ChangeRopeLength adds or removes (int)(len * Resolution) + 1 points, and bounds the length by its points
            if (change != 0) rope.ChangeRopeLength(Math.Sign(change) * (Math.Abs(change) - 1 + 0.01) / rope.Resolution);
        }
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
        return TextCommandResult.Success(HullState(entity));
    }

    private static TextCommandResult Interact(IPlayer player, Entity? entity, string mode, string? modifier, string? box, string? place)
    {
        if (entity == null) return TextCommandResult.Error("No such entity");

        var boxes = entity.GetBehavior<EntityBehaviorSelectionBoxes>()?.selectionBoxes ?? [];
        var boxIndex = box == null ? 0 : Array.FindIndex(boxes, candidate => candidate.AttachPoint.Code == box) + 1;
        if (box != null && boxIndex == 0) return TextCommandResult.Error($"No box {box}; boxes: {string.Join(", ", boxes.Select(candidate => candidate.AttachPoint.Code))}");

        var controls = player.Entity.Controls;
        controls.CtrlKey = modifier == "ctrl";
        controls.ShiftKey = modifier == "shift";
        var interactMode = mode == "attack" ? EnumInteractMode.Attack : EnumInteractMode.Interact;
        var hull = entity.GetBehavior<EntityBehaviorVesselIntegrity>();
        var hit = place != null && hull != null
            ? hull.PointAt(Enum.Parse<HullPlace>(place, ignoreCase: true))
            : new Vec3d(0, entity.SelectionBox.Y2 / 2, 0);
        player.Entity.EntitySelection = new EntitySelection { Entity = entity, Position = entity.Pos.XYZ, SelectionBoxIndex = boxIndex, HitPosition = hit };
        if (interactMode == EnumInteractMode.Attack || hull?.TryPickUp(player.Entity) != true)
        {
            entity.OnInteract(player.Entity, player.InventoryManager.ActiveHotbarSlot, hit, interactMode);
        }
        controls.CtrlKey = controls.ShiftKey = false;
        return TextCommandResult.Success(HullState(entity));
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
            ground = new
            {
                solid = accessor.GetBlock(blockPos.DownCopy(), BlockLayersAccess.Solid)?.Code?.ToString(),
                fluid = accessor.GetBlock(blockPos.DownCopy(), BlockLayersAccess.Fluid)?.Code?.ToString()
            },
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
            $"\"watched\":{Strict(entity.WatchedAttributes)},\"attributes\":{Strict(entity.Attributes)}}}");
    }

    private static string Strict(ITreeAttribute tree) =>
        new JObject(tree.Select(pair => new JProperty(pair.Key, Strict(pair.Value.ToJsonToken())))).ToString(Newtonsoft.Json.Formatting.None);

    private static JToken Strict(string relaxedJson)
    {
        try
        {
            return JToken.Parse(relaxedJson);
        }
        catch (Newtonsoft.Json.JsonReaderException)
        {
            return relaxedJson;
        }
    }
}
