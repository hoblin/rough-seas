using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RoughSeas.Shipyard;

public static class Waterside
{
    public static bool IsWater(IBlockAccessor accessor, BlockPos at) =>
        accessor.GetBlock(at, BlockLayersAccess.Fluid) is { } fluid && fluid.IsLiquid() && fluid.LiquidCode?.Contains("water") == true;

    /// <summary>Whether water lies beside the block, level with it or a block lower.</summary>
    public static bool Beside(IBlockAccessor accessor, BlockPos at) =>
        BlockFacing.HORIZONTALS.Any(side => IsWater(accessor, at.AddCopy(side)) || IsWater(accessor, at.AddCopy(side).DownCopy()));
}
