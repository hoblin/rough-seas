using System;
using Vintagestory.API.Common;

namespace RoughSeas.Storms;

/// <summary>
/// The water a point is in as the sea is drawn: the still water's surface in the point's column, where vanilla draws
/// its water mesh (7/8 of the top block for still water), plus the swell drawn there. The drawn sea rises over the
/// block water in a crest and sinks below it in a trough, so a point a crest covers is under water in an air block,
/// and a point a trough bares is out of it in a water block. On a client alone, as the drawn sea is. Positions are
/// absolute block coordinates.
/// </summary>
public sealed class DrawnWater(IDrawnSea sea)
{
    /// <summary>
    /// The height of the drawn surface over the water column at a point, or null where the point stays as the blocks
    /// have it: no swell is drawn there, no water lies under the point within the swell's reach there, or a solid block
    /// stands between the point and the water. The point may lie above the surface returned: between crests, or in the
    /// top water block above the level its water is drawn at.
    /// </summary>
    public double? SurfaceAt(IBlockAccessor blocks, double x, double y, double z) =>
        SurfaceAt(new BlockColumn(blocks, (int)Math.Floor(x), (int)Math.Floor(z)), x, y, z);

    internal double? SurfaceAt<TColumn>(TColumn column, double x, double y, double z) where TColumn : IWaterColumn =>
        sea.SwellAt(x, z) ? WaterAt(column, x, y, z)?.Height : null;

    /// <summary>
    /// The water a body at a point is in: the drawn surface over its column as <see cref="SurfaceAt(IBlockAccessor, double, double, double)"/>
    /// finds it, or the still water's surface where no swell is drawn. Null where no water lies under the point within
    /// the swell's reach there, or a solid block stands between.
    /// </summary>
    public WaterSurface? WaterAt(IBlockAccessor blocks, double x, double y, double z) =>
        WaterAt(new BlockColumn(blocks, (int)Math.Floor(x), (int)Math.Floor(z)), x, y, z);

    internal WaterSurface? WaterAt<TColumn>(TColumn column, double x, double y, double z) where TColumn : IWaterColumn
    {
        var swell = sea.SwellAt(x, z) ? sea.HeightAt(x, z) : 0;
        return TopOf(column, y, swell) is { } top ? new WaterSurface(top + column.Level(top) + swell, swell, column.Salt(top)) : null;
    }

    private static int? TopOf<TColumn>(TColumn column, double y, double swell) where TColumn : IWaterColumn
    {
        var at = (int)Math.Floor(y);
        if (column.Water(at))
        {
            // in the block water: up to its top block, as vanilla counts the eye's depth
            while (column.Water(at + 1)) at++;
            return at;
        }

        // above it: down as far as a crest here could reach the point, and a block more
        var lowest = (int)Math.Floor(y - 1 - Math.Max(swell, 0));
        for (var below = at; below >= lowest; below--)
        {
            // a solid block first: a waterlogged deck keeps the sea out as a dry one does
            if (column.Solid(below)) return null;
            if (column.Water(below)) return below;
        }
        return null;
    }
}

/// <summary>The surface over a water column: its height, the swell drawn in it, and whether the water is the sea's.</summary>
public readonly record struct WaterSurface(double Height, double Swell, bool Salt);

/// <summary>One column of blocks, read for the water in it.</summary>
internal interface IWaterColumn
{
    /// <summary>Whether the block at <paramref name="y"/> holds water, by vanilla's codes for the water a body is in.</summary>
    bool Water(int y);

    /// <summary>How high the water in the block at <paramref name="y"/> stands above the block's bottom, in blocks: where vanilla draws its surface.</summary>
    double Level(int y);

    /// <summary>Whether a block at <paramref name="y"/> keeps the sea out: a floor, a deck or a dock between a point and the water.</summary>
    bool Solid(int y);

    /// <summary>Whether the water in the block at <paramref name="y"/> is the sea's, denser than a lake's or a river's.</summary>
    bool Salt(int y);
}

internal readonly struct BlockColumn(IBlockAccessor blocks, int x, int z) : IWaterColumn
{
    // vanilla measures a liquid in eighths of a block: its mesh (ChunkTesselator.waterLevels), its selection box and
    // the swimming stroke (PModulePlayerInLiquid.HandleSwimming) all stand at LiquidLevel / 8, so still water at 7/8
    private const double LevelsPerBlock = 8;

    public bool Water(int y) => Fluid(y).LiquidCode is "water" or "seawater" or "saltwater";

    public double Level(int y) => Fluid(y).LiquidLevel / LevelsPerBlock;

    public bool Salt(int y) => Fluid(y).LiquidCode is "seawater" or "saltwater";

    public bool Solid(int y) => blocks.GetBlockRaw(x, y, z, BlockLayersAccess.Solid).CollisionBoxes is { Length: > 0 };

    private Block Fluid(int y) => blocks.GetBlockRaw(x, y, z, BlockLayersAccess.Fluid);
}
