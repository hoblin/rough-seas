using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RoughSeas.Shipyard;

public static class Waterside
{
    public static bool IsWater(IBlockAccessor accessor, BlockPos at) =>
        accessor.GetBlock(at, BlockLayersAccess.Fluid) is { } fluid && fluid.IsLiquid() && fluid.LiquidCode?.Contains("water") == true;
}
