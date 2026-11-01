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

    private EntityProperties? successor;
    private RiggingConfig? rig;

    public override string PropertyName() => Code;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        var vessel = attributes["vessel"].AsString("");
        successor = Wood.Of(properties.Variant) is { } wood ? entity.World.GetEntityType(new AssetLocation($"{vessel}-{wood.Code}")) : null;
        rig = successor == null ? null : RiggingConfig.Of(successor);
        rig?.Measure(successor?.Client?.Shape?.Base is { } shape ? FramingConfig.ShapeAt(entity.Api, shape) : null);
    }

    /// <summary>
    /// The row of <paramref name="logs"/> lying along a hull at <paramref name="x"/>, <paramref name="y"/>,
    /// <paramref name="z"/> turned to <paramref name="yaw"/>, its middle log the block <paramref name="offMetres"/> off her
    /// middle on either side, where the rigged model draws her float; end for end when it lies on her port side.
    /// </summary>
    public static (LogRow Row, bool EndForEnd)? FloatBeside(double x, double y, double z, double yaw, int logs, double offMetres, System.Func<BlockPos, LyingLog?> logAt)
    {
        var (alongX, alongZ) = HullShape.ToWorld(1, 0, yaw);
        var axis = Math.Abs(alongX) > Math.Abs(alongZ) ? EnumAxis.X : EnumAxis.Z;
        foreach (var side in new[] { 1, -1 })
        {
            var (dx, dz) = HullShape.ToWorld(0, side * offMetres, yaw);
            var beside = new BlockPos((int)Math.Floor(x + dx), (int)Math.Floor(y + 0.5), (int)Math.Floor(z + dz));
            var row = LogRow.Run(beside, logAt, logs);
            if (row?.Length == logs && row.Along == axis && row.Centre.Equals(beside)) return (row, side < 0);
        }
        return null;
    }

    public static (double X, double Z) OntoRow(double x, double z, double yaw, double offMetres, BlockPos centre)
    {
        var (floatX, floatZ) = HullShape.ToWorld(0, offMetres, yaw);
        return (centre.X + 0.5 - x - floatX, centre.Z + 0.5 - z - floatZ);
    }

    public (double Seconds, string? Refusal)? WorkWith(ItemSlot slot)
    {
        if (rig == null || slot.Itemstack?.Collectible is not { } pole || !WildcardUtil.Match(new AssetLocation(rig.Poles), pole.Code)) return null;

        var seconds = rig.Stages[0].Seconds;
        if (entity.GetBehavior<EntityBehaviorBuild>() is { Finished: false }) return (seconds, "shipyard-rig-unfinished");

        if (Laying.ShorterThan(slot, rig.Stages[0].Metres.FirstOrDefault())) return (seconds, "shipyard-pole-short");

        return (seconds, Float(rig) != null ? null : "shipyard-float-row");
    }

    public void Lay(EntityAgent byEntity, ItemSlot slot)
    {
        var world = entity.World;
        var shipyard = world.Api.ModLoader.GetModSystem<ShipyardModSystem>();
        if (WorkWith(slot) is not { Refusal: null } || successor == null || rig == null || Float(rig) is not var (row, endForEnd)
            || !LandClaims.MayBreak(byEntity, row.Blocks) || slot.Itemstack?.Collectible.Variant["wood"] is not { } poleWood) return;

        var rigged = world.ClassRegistry.CreateEntity(successor);
        rigged.Pos.SetFrom(entity.Pos);
        if (endForEnd) rigged.Pos.Yaw = GameMath.Mod(entity.Pos.Yaw + GameMath.PI, GameMath.TWOPI);
        var (dx, dz) = OntoRow(rigged.Pos.X, rigged.Pos.Z, rigged.Pos.Yaw, rig.FloatOffMetres, row.Centre);
        rigged.Pos.X += dx;
        rigged.Pos.Z += dz;
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
        shipyard.Log(rigged, "rigged by {0} from {1} {2}{3}: a boom of {4} across her joins a float of {5} {6} logs, her hull moved {7:F2} m onto them",
            byEntity.GetName(), entity.Code, entity.EntityId, endForEnd ? ", turned end for end" : "", poleWood, row.Length, row.Wood.Code, Math.Sqrt(dx * dx + dz * dz));
    }

    public override WorldInteraction[]? GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled) =>
        rig == null || entity.GetBehavior<EntityBehaviorBuild>() is { Finished: false } ? null : ObjectCacheUtil.GetOrCreate(world.Api, $"{Code}/help/{rig.Poles}", () => new[]
        {
            new WorldInteraction
            {
                ActionLangCode = "roughseas:shipyard-rig",
                MouseButton = EnumMouseButton.Right,
                Itemstacks = [.. world.SearchItems(new AssetLocation(rig.Poles)).Select(pole => new ItemStack(pole))]
            }
        });

    private (LogRow Row, bool EndForEnd)? Float(RiggingConfig rig)
    {
        var accessor = entity.World.BlockAccessor;
        return FloatBeside(entity.Pos.X, entity.Pos.Y, entity.Pos.Z, entity.Pos.Yaw, rig.FloatLogs, rig.FloatOffMetres, pos => LyingLog.Of(accessor.GetBlock(pos)));
    }
}
