using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Shipyard;

/// <summary>
/// On vanilla's <c>game:multiblock</c> (patches/shipyard-groundstorage.json): a filler beside a ground pile passes the
/// changes around it on to the pile, which <see cref="BlockBehaviorPileFootprint"/> weighs its ground by.
/// </summary>
public class BlockBehaviorPileFiller(Block block) : BlockBehavior(block)
{
    public const string Code = "roughseas:pilefiller";

    public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos, ref EnumHandling handling)
    {
        if (world.Side != EnumAppSide.Server || block is not BlockMultiblock filler) return;

        var pile = pos.AddCopy(filler.OffsetInv);
        if (world.BlockAccessor.GetBlockEntity(pile) is BlockEntityGroundStorage) world.BlockAccessor.GetBlock(pile).OnNeighbourBlockChange(world, pile, neibpos);
    }
}
