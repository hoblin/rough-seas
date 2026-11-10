using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RoughSeas.SeaAndWeather;

/// <summary>
/// The one answer to "open water here?": a position holding a full solid block is land whatever its fluid layer holds,
/// since the water under the shore (<see cref="Shore.Groundwater"/>) only carries the drawn surface. A partial block
/// (a post, a slab) keeps the water around it.
/// </summary>
public static class OpenWater
{
    public static Block At(IBlockAccessor blocks, BlockPos pos)
    {
        var fluid = blocks.GetBlock(pos, BlockLayersAccess.Fluid);
        return fluid.Id != 0 && Land(blocks, pos) ? blocks.GetBlock(0) : fluid;
    }

    public static Block At(IBlockAccessor blocks, int x, int y, int z)
    {
        var fluid = blocks.GetBlockRaw(x, y, z, BlockLayersAccess.Fluid);
        return fluid.Id != 0 && Land(blocks, new BlockPos(x, y, z)) ? blocks.GetBlock(0) : fluid;
    }

    /// <summary><see cref="IBlockAccessor.GetBlock(BlockPos, int)"/> with the fluid layer read as <see cref="At(IBlockAccessor, BlockPos)"/>: what a transpiler puts in place of vanilla's read.</summary>
    public static Block Layer(IBlockAccessor blocks, BlockPos pos, int layer) =>
        layer == BlockLayersAccess.Fluid ? At(blocks, pos) : blocks.GetBlock(pos, layer);

    /// <summary><see cref="IBlockAccessor.GetBlockRaw"/> with the fluid layer read as <see cref="At(IBlockAccessor, int, int, int)"/>.</summary>
    public static Block LayerRaw(IBlockAccessor blocks, int x, int y, int z, int layer) =>
        layer == BlockLayersAccess.Fluid ? At(blocks, x, y, z) : blocks.GetBlockRaw(x, y, z, layer);

    public static bool Land(IBlockAccessor blocks, BlockPos pos) =>
        blocks.GetBlock(pos, BlockLayersAccess.Solid).DisplacesLiquids(blocks, pos);
}
