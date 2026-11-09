using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RoughSeas.SeaAndWeather;

public static class OpenWater
{
    public static Block At(IBlockAccessor blocks, BlockPos pos) =>
        Land(blocks, pos) ? blocks.GetBlock(0) : blocks.GetBlock(pos, BlockLayersAccess.Fluid);

    public static Block At(IBlockAccessor blocks, int x, int y, int z)
    {
        var fluid = blocks.GetBlockRaw(x, y, z, BlockLayersAccess.Fluid);
        return fluid.Id != 0 && Land(blocks, new BlockPos(x, y, z)) ? blocks.GetBlock(0) : fluid;
    }

    public static Block Layer(IBlockAccessor blocks, BlockPos pos, int layer) =>
        layer == BlockLayersAccess.Fluid ? At(blocks, pos) : blocks.GetBlock(pos, layer);

    public static bool Land(IBlockAccessor blocks, BlockPos pos) =>
        blocks.GetBlock(pos, BlockLayersAccess.Solid).DisplacesLiquids(blocks, pos);
}
