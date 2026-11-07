using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RoughSeas.Storms;

/// <summary>How deep the water stands over the bed, read from the fluid layer's blocks.</summary>
public static class WaterDepth
{
    /// <summary>The water's depth from the block at <paramref name="at"/> down, in metres, up to <paramref name="maxBlocks"/>; the top block counts as high as its water stands.</summary>
    public static double Below(IBlockAccessor blocks, Vec3d at, int maxBlocks)
    {
        var pos = at.AsBlockPos;
        var depth = 0.0;
        var surface = !blocks.GetBlock(pos.UpCopy(), BlockLayersAccess.Fluid).IsLiquid();
        while (depth < maxBlocks && blocks.GetBlock(pos, BlockLayersAccess.Fluid) is { } fluid && fluid.IsLiquid())
        {
            depth += surface ? fluid.LiquidLevel / 8.0 : 1;
            surface = false;
            pos.Down();
        }
        return depth;
    }

    /// <summary>The whole depth of the water a point lies in or just over: it looks up to the column's top block first, or a block down onto it, then down from there.</summary>
    public static double AtColumn(IBlockAccessor blocks, Vec3d at, int maxBlocks)
    {
        var top = at.Clone();
        if (!blocks.GetBlock(top.AsBlockPos, BlockLayersAccess.Fluid).IsLiquid()) top.Y -= 1;
        for (var up = 0; up < maxBlocks && blocks.GetBlock(top.AsBlockPos.UpCopy(), BlockLayersAccess.Fluid).IsLiquid(); up++) top.Y += 1;
        return Below(blocks, top, maxBlocks);
    }
}
