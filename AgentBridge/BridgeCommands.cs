using System.Globalization;
using System.Linq;
using System.Text.Json;
using RoughSeas.Vessels;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace AgentBridge;

public class BridgeCommands : ModSystem
{
    private ICoreServerAPI sapi = null!;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
        var parsers = api.ChatCommands.Parsers;
        api.ChatCommands.Create("bridge")
            .WithDescription("Agent bridge: server-side state as JSON")
            .RequiresPrivilege(Privilege.controlserver)
            .BeginSubCommand("here")
                .WithDescription("Sea, weather and climate at the caller's position")
                .HandleWith(args => Reply(Here(args.Caller.Pos)))
            .EndSubCommand()
            .BeginSubCommand("entity")
                .WithDescription("An entity's server-side attributes")
                .WithArgs(parsers.Long("id"))
                .HandleWith(args => Entity((long)args[0]))
            .EndSubCommand()
            .BeginSubCommand("sea")
                .WithDescription("Teleport the caller into the nearest open sea within 1500 blocks, in the water at sea level")
                .HandleWith(args => Sea(args.Caller.Player))
            .EndSubCommand()
            .BeginSubCommand("set")
                .WithDescription("Set an entity's watched attribute: int, float, bool or string by the value's form")
                .WithArgs(parsers.Long("id"), parsers.Word("key"), parsers.Word("value"))
                .HandleWith(args => Set(sapi.World.GetEntityById((long)args[0]), (string)args[1], (string)args[2]))
            .EndSubCommand()
            .BeginSubCommand("ride")
                .WithDescription("Spawn a boat (or any seatable entity) at the caller, at the surface over water, and seat the caller in it")
                .WithArgs(parsers.Word("entity code"))
                .HandleWith(args => Ride(args.Caller.Player, (string)args[0]))
            .EndSubCommand()
            .BeginSubCommand("mount")
                .WithDescription("Seat the caller in an entity, a steering seat first")
                .WithArgs(parsers.Long("id"))
                .HandleWith(args => Mount(args.Caller.Player, sapi.World.GetEntityById((long)args[0])))
            .EndSubCommand()
            .BeginSubCommand("dismount")
                .WithDescription("Take the caller off whatever they ride, as Shift does")
                .HandleWith(args => args.Caller.Player.Entity.MountedOn != null && args.Caller.Player.Entity.TryUnmount()
                    ? TextCommandResult.Success("Dismounted")
                    : TextCommandResult.Error("Not mounted, or the seat would not let go"))
            .EndSubCommand()
            .BeginSubCommand("wear")
                .WithDescription("Put an item into the caller's first empty backpack slot, where it is worn on the back")
                .WithArgs(parsers.Word("item code"))
                .HandleWith(args => Wear(args.Caller.Player, (string)args[0]))
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
                .WithArgs(parsers.Long("id"), parsers.WordRange("mode", "attack", "interact"), parsers.OptionalWordRange("modifier", "ctrl", "shift"))
                .HandleWith(args => Interact(args.Caller.Player, sapi.World.GetEntityById((long)args[0]), (string)args[1], args[2] as string))
            .EndSubCommand();
    }

    private TextCommandResult Sea(IPlayer player)
    {
        const int range = 1500;
        const int step = 16;
        var accessor = sapi.World.BlockAccessor;
        var regionSize = accessor.RegionSize;
        var origin = player.Entity.Pos.AsBlockPos;

        var sea = Enumerable.Range(-range / step, 2 * range / step + 1)
            .SelectMany(i => Enumerable.Range(-range / step, 2 * range / step + 1), (i, j) => (x: origin.X + i * step, z: origin.Z + j * step))
            .Where(point => accessor.GetMapRegion(point.x / regionSize, point.z / regionSize)?.OceanMap is { Data.Length: > 0 } map
                && map.GetUnpaddedIntLerpedForBlockPos(point.x, point.z, regionSize) >= 250)
            .OrderBy(point => (point.x - origin.X) * (long)(point.x - origin.X) + (point.z - origin.Z) * (long)(point.z - origin.Z))
            .Select(point => ((int x, int z)?)point)
            .FirstOrDefault();
        if (sea == null) return TextCommandResult.Error($"No open sea in the generated regions within {range} blocks");

        player.Entity.TeleportTo(new Vec3d(sea.Value.x + 0.5, sapi.World.SeaLevel - 0.5, sea.Value.z + 0.5));
        return TextCommandResult.Success($"Teleported to open sea at {sea.Value.x - accessor.MapSizeX / 2} {sapi.World.SeaLevel} {sea.Value.z - accessor.MapSizeZ / 2}");
    }

    private static TextCommandResult Set(Entity? entity, string key, string value)
    {
        if (entity == null) return TextCommandResult.Error("No such entity");

        var attributes = entity.WatchedAttributes;
        if (int.TryParse(value, CultureInfo.InvariantCulture, out var number)) attributes.SetInt(key, number);
        else if (float.TryParse(value, CultureInfo.InvariantCulture, out var fraction)) attributes.SetFloat(key, fraction);
        else if (bool.TryParse(value, out var flag)) attributes.SetBool(key, flag);
        else attributes.SetString(key, value);
        return TextCommandResult.Success($"{entity.Code} {entity.EntityId}: {key} = {attributes[key].ToJsonToken()}");
    }

    private TextCommandResult Ride(IPlayer player, string code)
    {
        var type = sapi.World.GetEntityType(new AssetLocation(code));
        if (type == null) return TextCommandResult.Error($"No entity type {code}");

        var boat = sapi.World.ClassRegistry.CreateEntity(type);
        var pos = player.Entity.Pos;
        var surface = sapi.World.BlockAccessor.GetBlock(pos.AsBlockPos, BlockLayersAccess.Fluid).IsLiquid()
            ? sapi.World.SeaLevel - 1
            : pos.Y;
        boat.Pos.SetPos(pos.X, surface, pos.Z);
        boat.Pos.Yaw = pos.Yaw;
        sapi.World.SpawnEntity(boat);
        return Mount(player, boat);
    }

    private static TextCommandResult Mount(IPlayer player, Entity? target)
    {
        var seats = target?.GetInterface<IMountable>()?.Seats;
        if (seats == null) return TextCommandResult.Error("Not a seatable entity");

        var seat = seats.Where(seat => seat.Passenger == null).OrderByDescending(seat => seat.CanControl).FirstOrDefault();
        if (seat == null || !player.Entity.TryMount(seat)) return TextCommandResult.Error("No free seat");

        return TextCommandResult.Success($"Seated in {target!.Code} {target.EntityId}, seat {seat.SeatId}");
    }

    private TextCommandResult Wear(IPlayer player, string code)
    {
        var item = sapi.World.GetItem(new AssetLocation(code));
        if (item == null) return TextCommandResult.Error($"No item {code}");

        var slot = player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName).FirstOrDefault(slot => slot is ItemSlotBackpack && slot.Empty);
        if (slot == null) return TextCommandResult.Error("No empty backpack slot");

        var stack = new ItemStack(item);
        if (!slot.CanHold(new DummySlot(stack))) return TextCommandResult.Error($"{code} does not go in a backpack slot");

        slot.Itemstack = stack;
        slot.MarkDirty();
        return TextCommandResult.Success($"Wearing {code}");
    }

    private static TextCommandResult Strike(Entity? entity, double joules)
    {
        var hull = entity?.GetBehavior<EntityBehaviorVesselIntegrity>();
        if (hull == null) return TextCommandResult.Error("Not a vessel");

        hull.Strike(joules);
        return TextCommandResult.Success($"{entity!.Code} {entity.EntityId}: alive {entity.Alive}, integrity {hull.Integrity}");
    }

    private TextCommandResult Blast(Entity? entity, int injureRadius, double distance)
    {
        if (entity == null) return TextCommandResult.Error("No such entity");

        sapi.World.CreateExplosion(entity.Pos.XYZ.Add(distance, 1, 0).AsBlockPos, EnumBlastType.EntityBlast, 0, injureRadius);
        var hull = entity.GetBehavior<EntityBehaviorVesselIntegrity>();
        return TextCommandResult.Success($"{entity.Code} {entity.EntityId}: alive {entity.Alive}" + (hull == null ? "" : $", integrity {hull.Integrity}"));
    }

    private static TextCommandResult Interact(IPlayer player, Entity? entity, string mode, string? modifier)
    {
        if (entity == null) return TextCommandResult.Error("No such entity");

        var controls = player.Entity.Controls;
        controls.CtrlKey = modifier == "ctrl";
        controls.ShiftKey = modifier == "shift";
        var interactMode = mode == "attack" ? EnumInteractMode.Attack : EnumInteractMode.Interact;
        if (interactMode == EnumInteractMode.Attack || entity.GetBehavior<EntityBehaviorVesselIntegrity>()?.TryPickUp(player.Entity) != true)
        {
            entity.OnInteract(player.Entity, player.InventoryManager.ActiveHotbarSlot, new Vec3d(0, entity.SelectionBox.Y2 / 2, 0), interactMode);
        }
        controls.CtrlKey = controls.ShiftKey = false;
        var hull = entity.GetBehavior<EntityBehaviorVesselIntegrity>();
        return TextCommandResult.Success($"{entity.Code} {entity.EntityId}: alive {entity.Alive}" + (hull == null ? "" : $", integrity {hull.Integrity}"));
    }

    private static TextCommandResult Reply(object value) => TextCommandResult.Success(JsonSerializer.Serialize(value));

    private object Here(Vec3d pos)
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

    private TextCommandResult Entity(long id)
    {
        var entity = sapi.World.GetEntityById(id);
        if (entity == null) return TextCommandResult.Error($"No entity {id}");

        return TextCommandResult.Success(
            $"{{\"code\":\"{entity.Code}\",\"alive\":{entity.Alive.ToString().ToLower()}," +
            $"\"pos\":{JsonSerializer.Serialize(new { entity.Pos.X, entity.Pos.Y, entity.Pos.Z, entity.Pos.Yaw })}," +
            $"\"behaviors\":{JsonSerializer.Serialize(entity.SidedProperties.Behaviors.Select(behavior => behavior.PropertyName()))}," +
            $"\"watched\":{entity.WatchedAttributes.ToJsonToken()},\"attributes\":{entity.Attributes.ToJsonToken()}}}");
    }
}
