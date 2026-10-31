using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace RoughSeas.Shipyard;

/// <summary>
/// Bundles lying side by side where they were laid. The first binding of rope around the pair, by the water, makes
/// them the vessel they are bundles of, where they lie.
/// </summary>
public class BlockBundles : Block
{
    public const string ClassName = "roughseas:BlockBundles";

    private const string CountVariant = "count";
    private const string AxisVariant = "axis";

    private readonly HeldWork work = new();
    private AssetLocation vessel = null!;

    private bool Pair => Variant[CountVariant] == "two";

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        vessel = new AssetLocation(Attributes["vessel"].AsString());
    }

    public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
    {
        var along = BlockFacing.HorizontalFromYaw(byPlayer.Entity.Pos.Yaw).Axis;
        world.BlockAccessor.SetBlock(world.GetBlock(CodeWithVariants([CountVariant, AxisVariant], ["one", AxisOf(along)])).Id, blockSel.Position);
        return true;
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        var slot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (!Pair && slot.Itemstack?.Block is BlockBundles bundle && bundle.FirstCodePart() == FirstCodePart())
        {
            if (world.Side == EnumAppSide.Server) LayBeside(world, byPlayer, slot, blockSel.Position);
            return true;
        }
        if (!Pair || Binding(world) is not { } binding || !binding.Laying.Takes(slot)) return base.OnBlockInteractStart(world, byPlayer, blockSel);

        var shipyard = world.Api.ModLoader.GetModSystem<ShipyardModSystem>();
        if (!ByWater(world, blockSel.Position))
        {
            shipyard.Log("bundles at {0} {1} {2} lie away from the water: not bound", blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z);
            (world.Api as ICoreClientAPI)?.TriggerIngameError(this, "shipyard-bundles-by-water", Lang.Get("roughseas:shipyard-bundles-by-water"));
            return false;
        }
        if (!LandClaims.MayBreak(byPlayer.Entity, [blockSel.Position]))
        {
            shipyard.Log("bundles at {0} {1} {2} lie on a claim {3} may not break: not bound", blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer.PlayerName);
            return false;
        }

        work.Start(byPlayer.Entity);
        return true;
    }

    public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel) =>
        Binding(world) is { } binding && work.Step(secondsUsed, binding.Stage.Seconds);

    public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (Binding(world) is not { } binding || !work.Stop(byPlayer.Entity, secondsUsed, binding.Stage.Seconds) || world.Side != EnumAppSide.Server) return;

        var slot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (Pair && binding.Laying.Takes(slot) && ByWater(world, blockSel.Position) && LandClaims.MayBreak(byPlayer.Entity, [blockSel.Position]))
        {
            Bind(world, byPlayer, slot, blockSel.Position, binding);
        }
    }

    public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
    {
        work.Stop(byPlayer.Entity);
        return true;
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer) =>
        ObjectCacheUtil.GetOrCreate(world.Api, $"{ClassName}/help/{Code}", () => new[]
        {
            Pair
                ? new WorldInteraction
                {
                    ActionLangCode = "roughseas:shipyard-bind-bundles",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = Binding(world)?.Laying.Shown(world).ToArray() ?? []
                }
                : new WorldInteraction
                {
                    ActionLangCode = "roughseas:shipyard-lay-bundle",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = [new ItemStack(world.GetBlock(CodeWithVariants([CountVariant, AxisVariant], ["one", "we"])))]
                }
        }).Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));

    private static string AxisOf(EnumAxis axis) => axis == EnumAxis.X ? "we" : "ns";

    private static bool ByWater(IWorldAccessor world, BlockPos pos) =>
        BlockFacing.HORIZONTALS.Any(side => Water(world, pos.AddCopy(side)) || Water(world, pos.AddCopy(side).DownCopy()));

    private static bool Water(IWorldAccessor world, BlockPos pos) =>
        world.BlockAccessor.GetBlock(pos, BlockLayersAccess.Fluid) is { } fluid && fluid.IsLiquid() && fluid.LiquidCode?.Contains("water") == true;

    private (FramingStage Stage, Laying Laying)? Binding(IWorldAccessor world)
    {
        var config = world.GetEntityType(vessel) is { } type ? FramingConfig.Of(type) : null;
        return config?.Stages.FirstOrDefault() is { } first ? (first, Laying.For(first, config)) : null;
    }

    private void LayBeside(IWorldAccessor world, IPlayer byPlayer, ItemSlot slot, BlockPos pos)
    {
        world.BlockAccessor.SetBlock(world.GetBlock(CodeWithVariant(CountVariant, "two")).Id, pos);
        if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative) slot.TakeOut(1);
        slot.MarkDirty();
        world.PlaySoundAt(Sounds.Place, pos, 0, byPlayer);
    }

    private void Bind(IWorldAccessor world, IPlayer byPlayer, ItemSlot slot, BlockPos pos, (FramingStage Stage, Laying Laying) binding)
    {
        var shipyard = world.Api.ModLoader.GetModSystem<ShipyardModSystem>();
        if (world.GetEntityType(vessel) is not { } type)
        {
            shipyard.Log("no {0} to bind bundles into", vessel);
            return;
        }

        world.BlockAccessor.SetBlock(0, pos);
        var boat = world.ClassRegistry.CreateEntity(type);
        boat.Pos.SetPos(pos.ToVec3d().Add(0.5, 0, 0.5));
        boat.Pos.Yaw = Variant[AxisVariant] == "we" ? GameMath.PIHALF : 0;
        EntityBehaviorFraming.Begin(boat, binding.Stage.Group);
        world.SpawnEntity(boat);
        if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative) binding.Laying.Spend(slot, byPlayer.Entity);
        slot.MarkDirty();
        world.PlaySoundAt(new AssetLocation("game:sounds/player/build"), boat, null, true, 16);
        shipyard.Log(boat, "bound by {0} from the bundles lying at {1} {2} {3}", byPlayer.PlayerName, pos.X, pos.Y, pos.Z);
    }
}
