using System;
using System.Linq;
using RoughSeas.Vessels;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace RoughSeas.Shipyard;

/// <summary>
/// A finished hull that a boom laid across her joins to a float row of logs lying alongside: she becomes the vessel
/// she is rigged into, keeping her hull's condition and the water in her.
/// </summary>
public class EntityBehaviorRiggable(Entity entity) : EntityBehavior(entity), IWorkedHull
{
    public const string Code = "roughseas:riggable";

    private string vessel = "";
    private AssetLocation poles = null!;
    private double seconds;

    public override string PropertyName() => Code;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        vessel = attributes["vessel"].AsString("");
        poles = new AssetLocation(attributes["poles"].AsString(""));
        seconds = attributes["seconds"].AsDouble();
    }

    public (double Seconds, string? Refusal)? WorkWith(ItemSlot slot)
    {
        if (slot.Itemstack?.Collectible is not { } pole || !WildcardUtil.Match(poles, pole.Code)) return null;
        if (entity.GetBehavior<EntityBehaviorBuild>() is { Finished: false }) return (seconds, "shipyard-rig-unfinished");

        return (seconds, Rig() is { } rig && Float(rig, null) != null ? null : "shipyard-float-row");
    }

    public void Lay(EntityAgent byEntity, ItemSlot slot)
    {
        var world = entity.World;
        var shipyard = world.Api.ModLoader.GetModSystem<ShipyardModSystem>();
        if (WorkWith(slot) is not { Refusal: null } || Successor() is not { } type || Rig() is not { } rig
            || Float(rig, byEntity) is not var (row, endForEnd) || slot.Itemstack?.Collectible.Variant["wood"] is not { } poleWood) return;

        var rigged = world.ClassRegistry.CreateEntity(type);
        rigged.Pos.SetFrom(entity.Pos);
        if (endForEnd) rigged.Pos.Yaw = GameMath.Mod(entity.Pos.Yaw + GameMath.PI, GameMath.TWOPI);
        foreach (var state in entity.SidedProperties.Behaviors.OfType<IHandedOver>()) state.HandOver(rigged, endForEnd);
        EntityBehaviorRigging.Begin(rigged, rig, poleWood, row.Wood.Code);

        foreach (var log in row.Blocks) world.BlockAccessor.SetBlock(0, log);
        if ((byEntity as EntityPlayer)?.Player?.WorldData.CurrentGameMode != EnumGameMode.Creative) slot.TakeOut(1);
        slot.MarkDirty();
        foreach (var seat in entity.GetBehavior<EntityBehaviorSeatable>()?.Seats ?? []) (seat.Passenger as EntityAgent)?.TryUnmount();
        entity.GetBehavior<EntityBehaviorRideableAccessories>()?.Inventory.DropAll(entity.Pos.XYZ);

        world.SpawnEntity(rigged);
        entity.Die(EnumDespawnReason.Removed);
        world.PlaySoundAt(new AssetLocation("game:sounds/player/build"), rigged, null, true, 16);
        shipyard.Log(rigged, "rigged by {0} from {1} {2}{3}: a boom of {4} across her joins a float of {5} {6} logs", byEntity.GetName(),
            entity.Code, entity.EntityId, endForEnd ? ", turned end for end" : "", poleWood, row.Length, row.Wood.Code);
    }

    public override WorldInteraction[]? GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled) =>
        entity.GetBehavior<EntityBehaviorBuild>() is { Finished: false } ? null : ObjectCacheUtil.GetOrCreate(world.Api, $"{Code}/help/{poles}", () => new[]
        {
            new WorldInteraction
            {
                ActionLangCode = "roughseas:shipyard-rig",
                MouseButton = EnumMouseButton.Right,
                Itemstacks = [.. world.SearchItems(poles).Select(pole => new ItemStack(pole))]
            }
        });

    private EntityProperties? Successor() =>
        Wood.Of(entity.Properties.Variant) is { } wood ? entity.World.GetEntityType(new AssetLocation($"{vessel}-{wood.Code}")) : null;

    private RiggingConfig? Rig() => Successor() is { } type ? RiggingConfig.Of(type) : null;

    private (LogRow Row, bool EndForEnd)? Float(RiggingConfig rig, EntityAgent? byEntity)
    {
        var accessor = entity.World.BlockAccessor;
        var (alongX, alongZ) = HullShape.ToWorld(1, 0, entity.Pos.Yaw);
        var axis = Math.Abs(alongX) > Math.Abs(alongZ) ? EnumAxis.X : EnumAxis.Z;
        foreach (var side in new[] { 1, -1 })
        {
            var (x, z) = HullShape.ToWorld(0, side * rig.FloatMetres, entity.Pos.Yaw);
            var at = new BlockPos((int)Math.Floor(entity.Pos.X + x), (int)Math.Floor(entity.Pos.Y + 0.5), (int)Math.Floor(entity.Pos.Z + z));
            var row = LogRow.Run(at, pos => LyingLog.Of(accessor.GetBlock(pos)));
            if (row?.Length == rig.FloatLogs && row.Along == axis && (byEntity == null || LandClaims.MayBreak(byEntity, row.Blocks))) return (row, side < 0);
        }
        return null;
    }
}
