using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace RoughSeas.Shipyard;

/// <summary>
/// A bundle, or two side by side, lying where they were laid. The first binding of rope around the pair, by the
/// water, makes them the vessel they are bundles of, where they lie.
/// </summary>
public class BlockBundles : Block
{
    public const string ClassName = "roughseas:BlockBundles";

    private const string CountVariant = "count";
    private const string AxisVariant = "axis";
    private const string One = "one";
    private const string Two = "two";
    private const string AwayFromWater = "they lie away from the water";
    private const string WaterLangCode = "shipyard-bundles-by-water";

    private readonly HeldWork work = new();
    private AssetLocation vessel = null!;
    private (FramingStage Stage, Laying Laying)? binding;

    private bool Pair => Variant[CountVariant] == Two;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        vessel = new AssetLocation(Attributes["vessel"].AsString());
    }

    public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
    {
        var along = BlockFacing.HorizontalFromYaw(byPlayer.Entity.Pos.Yaw).Axis;
        world.BlockAccessor.SetBlock(world.GetBlock(CodeWithVariants([CountVariant, AxisVariant], [One, AxisOf(along)])).Id, blockSel.Position);
        Shipyard(world).Log("{0} lays a bundle at {1} along {2}", byPlayer.PlayerName, At(blockSel.Position), along);
        return true;
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        var slot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (!Pair && slot.Itemstack?.Block is BlockBundles bundle && bundle.FirstCodePart() == FirstCodePart())
        {
            if (!LandClaims.MayBreak(byPlayer.Entity, [blockSel.Position])) return false;
            if (world.Side == EnumAppSide.Server) LayBeside(world, byPlayer, slot, blockSel.Position);
            return true;
        }
        if (!Pair || Binding(world) is not { } job || !job.Laying.Takes(slot)) return base.OnBlockInteractStart(world, byPlayer, blockSel);
        if (Refusal(world, byPlayer, blockSel.Position) is { } refusal)
        {
            Shipyard(world).Log("bundles at {0} not bound for {1}: {2}", At(blockSel.Position), byPlayer.PlayerName, refusal);
            if (refusal == AwayFromWater) (world.Api as ICoreClientAPI)?.TriggerIngameError(this, WaterLangCode, Lang.Get("roughseas:" + WaterLangCode));
            return false;
        }

        work.Start(byPlayer.Entity);
        return true;
    }

    public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel) =>
        Binding(world) is { } job && work.Step(secondsUsed, job.Stage.Seconds);

    public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (Binding(world) is not { } job || !work.Stop(byPlayer.Entity, secondsUsed, job.Stage.Seconds) || world.Side != EnumAppSide.Server) return;

        var slot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (!job.Laying.Takes(slot)) Shipyard(world).Log("bundles at {0} not bound: {1} let go of the rope", At(blockSel.Position), byPlayer.PlayerName);
        else if (Refusal(world, byPlayer, blockSel.Position) is { } refusal) Shipyard(world).Log("bundles at {0} not bound: {1}", At(blockSel.Position), refusal);
        else Bind(world, byPlayer, slot, blockSel.Position, job);
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
                    Itemstacks = [new ItemStack(world.GetBlock(CodeWithVariants([CountVariant, AxisVariant], [One, AxisOf(EnumAxis.X)])))]
                }
        }).Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));

    private static string AxisOf(EnumAxis axis) => axis == EnumAxis.X ? "we" : "ns";

    private static ShipyardModSystem Shipyard(IWorldAccessor world) => world.Api.ModLoader.GetModSystem<ShipyardModSystem>();

    private static string At(BlockPos pos) => $"{pos.X} {pos.Y} {pos.Z}";

    private static string? Refusal(IWorldAccessor world, IPlayer byPlayer, BlockPos pos) =>
        !Waterside.Beside(world.BlockAccessor, pos) ? AwayFromWater
        : !LandClaims.MayBreak(byPlayer.Entity, [pos]) ? $"{byPlayer.PlayerName} may not break blocks on this claim"
        : null;

    private (FramingStage Stage, Laying Laying)? Binding(IWorldAccessor world)
    {
        if (binding == null && world.GetEntityType(vessel) is { } type && FramingConfig.Of(type) is { Stages: [var first, ..] } config)
        {
            binding = (first, Laying.For(first, config));
        }
        return binding;
    }

    private void LayBeside(IWorldAccessor world, IPlayer byPlayer, ItemSlot slot, BlockPos pos)
    {
        world.BlockAccessor.SetBlock(world.GetBlock(CodeWithVariant(CountVariant, Two)).Id, pos);
        if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative) slot.TakeOut(1);
        slot.MarkDirty();
        world.PlaySoundAt(Sounds.Place, pos, 0);
        Shipyard(world).Log("{0} lays a second bundle beside the first at {1}", byPlayer.PlayerName, At(pos));
    }

    private void Bind(IWorldAccessor world, IPlayer byPlayer, ItemSlot slot, BlockPos pos, (FramingStage Stage, Laying Laying) job)
    {
        if (world.GetEntityType(vessel) is not { } type)
        {
            Shipyard(world).Log("no {0} to bind bundles into", vessel);
            return;
        }

        world.BlockAccessor.SetBlock(0, pos);
        var boat = world.ClassRegistry.CreateEntity(type);
        boat.Pos.SetPos(pos.ToVec3d().Add(0.5, 0, 0.5));
        boat.Pos.Yaw = Variant[AxisVariant] == AxisOf(EnumAxis.X) ? GameMath.PIHALF : 0;
        EntityBehaviorFraming.Begin(boat, job.Stage.Group);
        world.SpawnEntity(boat);
        if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative) job.Laying.Spend(slot, byPlayer.Entity);
        slot.MarkDirty();
        world.PlaySoundAt(new AssetLocation("game:sounds/player/build"), boat, null, true, 16);
        Shipyard(world).Log(boat, "bound by {0} from the bundles lying at {1}", byPlayer.PlayerName, At(pos));
    }
}
