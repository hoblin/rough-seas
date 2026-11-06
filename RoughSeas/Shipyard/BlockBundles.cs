using System.Linq;
using RoughSeas.Vessels;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace RoughSeas.Shipyard;

/// <summary>
/// A bundle, or two side by side, lying where they were laid. The first binding of rope around the pair makes them
/// the vessel they are bundles of, where they lie.
/// </summary>
public class BlockBundles : Block, IWorkedBlock
{
    public const string ClassName = "roughseas:BlockBundles";

    private const string AxisVariant = "axis";

    private AssetLocation vessel = null!;
    private (FramingStage Stage, Laying Laying)? binding;

    private bool Pair => Variant[Reeds.Starboard] != Reeds.None;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        vessel = new AssetLocation($"{Attributes["vessel"].AsString()}-{Variant[Reeds.Port]}-{Variant[Reeds.Starboard]}");
    }

    public override string GetHeldItemName(ItemStack itemStack) => Reeds.NameOf(Code, Variant, "block") ?? base.GetHeldItemName(itemStack);

    public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos) => Reeds.NameOf(Code, Variant, "block") ?? base.GetPlacedBlockName(world, pos);

    public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
    {
        var along = BlockFacing.HorizontalFromYaw(byPlayer.Entity.Pos.Yaw).Axis;
        world.BlockAccessor.SetBlock(world.GetBlock(CodeWithVariant(AxisVariant, AxisOf(along))).Id, blockSel.Position);
        Shipyard(world).Log("{0} lays a bundle of {1} at {2} along {3}", byPlayer.PlayerName, Variant[Reeds.Port], At(blockSel.Position), along);
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
        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }

    public IWorkedHull? HullAt(IWorldAccessor world, BlockPos pos) => Pair ? new Bound(this, world, pos) : null;

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
                    Itemstacks = world.SearchBlocks(new AssetLocation(Code.Domain, $"{FirstCodePart()}-*-{Reeds.None}-{AxisOf(EnumAxis.X)}")).Select(bundle => new ItemStack(bundle)).ToArray()
                }
        }).Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));

    private static string AxisOf(EnumAxis axis) => axis == EnumAxis.X ? "we" : "ns";

    private static ShipyardModSystem Shipyard(IWorldAccessor world) => world.Api.ModLoader.GetModSystem<ShipyardModSystem>();

    private static string At(BlockPos pos) => $"{pos.X} {pos.Y} {pos.Z}";

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
        var plant = slot.Itemstack!.Block.Variant[Reeds.Port];
        world.BlockAccessor.SetBlock(world.GetBlock(CodeWithVariant(Reeds.Starboard, plant)).Id, pos);
        if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative) slot.TakeOut(1);
        slot.MarkDirty();
        world.PlaySoundAt(Sounds.Place, pos, 0);
        Shipyard(world).Log("{0} lays a second bundle, of {1}, beside the first at {2}", byPlayer.PlayerName, plant, At(pos));
    }

    private void Bind(IWorldAccessor world, EntityAgent byEntity, ItemSlot slot, BlockPos pos, (FramingStage Stage, Laying Laying) job)
    {
        if (world.GetEntityType(vessel) is not { } type)
        {
            Shipyard(world).Log("no {0} to bind bundles into", vessel);
            return;
        }

        world.BlockAccessor.SetBlock(0, pos);
        var boat = world.ClassRegistry.CreateEntity(type);
        boat.Pos.SetPos(pos.ToVec3d().Add(0.5, 0, 0.5));
        // At this yaw her port bundle lies where the block drew its port one; the shape's mirror symmetry hides a wrong turn.
        boat.Pos.Yaw = Variant[AxisVariant] == AxisOf(EnumAxis.X) ? 3 * GameMath.PIHALF : 0;
        EntityBehaviorFraming.Begin(boat, job.Stage.Group);
        world.SpawnEntity(boat);
        if ((byEntity as EntityPlayer)?.Player?.WorldData.CurrentGameMode != EnumGameMode.Creative) job.Laying.Spend(slot, byEntity);
        slot.MarkDirty();
        world.PlaySoundAt(new AssetLocation("game:sounds/player/build"), boat, null, true, 16);
        Shipyard(world).Log(boat, "bound by {0} from the bundles lying at {1}", byEntity.GetName(), At(pos));
    }

    private sealed class Bound(BlockBundles bundles, IWorldAccessor world, BlockPos pos) : IWorkedHull
    {
        public HullWork? WorkWith(ItemSlot slot) =>
            bundles.Binding(world) is { } job && job.Laying.Takes(slot)
                ? new(job.Stage.Seconds, null, job.Laying.ShowsBar)
                : null;

        public void Lay(EntityAgent byEntity, ItemSlot slot)
        {
            if (WorkWith(slot) is not { Refusal: null }) return;
            if (!LandClaims.MayBreak(byEntity, [pos]))
            {
                Shipyard(world).Log("bundles at {0} not bound: {1} may not break blocks on this claim", At(pos), byEntity.GetName());
                return;
            }

            bundles.Bind(world, byEntity, slot, pos, bundles.Binding(world)!.Value);
        }
    }
}
