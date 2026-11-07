using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RoughSeas.Shipyard;

/// <summary>Laths lying on the ground: Shift with an empty hand takes the nearest back, and breaking it drops them all.</summary>
public class BlockLathBed : Block
{
    public const string ClassName = "roughseas:BlockLathBed";

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (!byPlayer.Entity.Controls.ShiftKey || !byPlayer.InventoryManager.ActiveHotbarSlot.Empty) return base.OnBlockInteractStart(world, byPlayer, blockSel);
        if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is not BlockEntityLathBed bed) return false;
        if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use)) return false;
        if (world.Side != EnumAppSide.Server) return true;

        var hit = blockSel.Position.ToVec3d().Add(blockSel.HitPosition);
        if (bed.TakeNearest(hit.X, hit.Z) is { } lath && !byPlayer.InventoryManager.TryGiveItemstack(lath, true))
        {
            world.SpawnItemEntity(lath, hit);
        }
        if (bed.Empty) world.BlockAccessor.SetBlock(0, blockSel.Position);
        return true;
    }

    public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1) =>
        world.BlockAccessor.GetBlockEntity(pos) is BlockEntityLathBed bed ? bed.Stacks.Select(stack => stack.Clone()).ToArray() : [];
}
