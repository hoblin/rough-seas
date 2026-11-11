using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RoughSeas.Shore;

public class BlockBehaviorGroundwater(Block block) : BlockBehavior(block)
{
    public const string Code = "roughseas:groundwater";

    public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos, ref EnumHandling handling)
    {
        if (world.Side == EnumAppSide.Server) ShoreModSystem.Water?.Stirred(pos);
    }
}
