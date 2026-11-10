using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RoughSeas.Water;

/// <summary>Water standing in a column of blocks: where its surface lies in its top block, and how deep it goes.</summary>
public static class WaterColumn
{
    public const int StillEighths = 7;

    public const double StillLevel = StillEighths / 8.0;

    private const double LevelsPerBlock = 8;

    private static readonly string[] SeaFamilies = ["water", "saltwater"];

    /// <summary>Whether this fluid is water a shore holds: the sea's or a lake's, and not another liquid.</summary>
    public static bool Carries(Block fluid) => fluid.IsLiquid() && SeaFamilies.Contains(fluid.FirstCodePart());

    /// <summary>The depth of the water from <paramref name="top"/> down, no further than <paramref name="maxBlocks"/>, and the first position under it. Moves <paramref name="top"/>.</summary>
    public static (double Depth, BlockPos Bottom) Sound(IBlockAccessor blocks, BlockPos top, int maxBlocks)
    {
        var depth = 0.0;
        var surface = !OpenWater.At(blocks, top.UpCopy()).IsLiquid();
        while (depth < maxBlocks && OpenWater.At(blocks, top) is { } fluid && fluid.IsLiquid())
        {
            depth += surface ? fluid.LiquidLevel / LevelsPerBlock : 1;
            surface = false;
            top.Down();
        }
        return (depth, top);
    }
}
