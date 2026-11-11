using System;
using RoughSeas.SeaAndWeather;

namespace RoughSeas.Storms;

/// <summary>
/// The waves of the player's region and its eight neighbours, each cell a share of the highest. Read as the shader reads
/// <c>seaHeightMap</c> (linear, clamped at the edge); the other end is <c>assets/roughseas/shaderincludes/seasurface.vsh</c>.
/// Each update is published whole, for the engine's ambient scan reads it off the main thread.
/// </summary>
public sealed class SurfaceField
{
    private const int RegionsAcross = 3;
    private const int Levels = 255;

    private volatile Grid grid = new(0, 0, 0, 0, [], 0, 0, 0);

    public double OriginX => grid.OriginX;

    public double OriginZ => grid.OriginZ;

    public double SizeBlocks => grid.Across * grid.CellBlocks;

    public double Height => grid.Height;

    public double HighestX => grid.HighestX;

    public double HighestZ => grid.HighestZ;

    public int Across => grid.Across;

    public void Update(ISeaSource source, double x, double z, int regionSize, int cellBlocks)
    {
        var across = RegionsAcross * regionSize / cellBlocks;
        var originX = (Math.Floor(x / regionSize) - 1) * regionSize;
        var originZ = (Math.Floor(z / regionSize) - 1) * regionSize;
        double CentreX(int cell) => originX + (cell % across + 0.5) * cellBlocks;
        double CentreZ(int cell) => originZ + (cell / across + 0.5) * cellBlocks;

        var heights = new double[across * across];
        var highest = 0;
        for (var i = 0; i < heights.Length; i++)
        {
            heights[i] = source.Sample(CentreX(i), CentreZ(i)).WaveHeight;
            if (heights[i] > heights[highest]) highest = i;
        }
        var factors = new byte[heights.Length];
        for (var i = 0; i < heights.Length; i++)
        {
            factors[i] = (byte)Math.Round(heights[highest] > 0 ? heights[i] / heights[highest] * Levels : 0);
        }
        grid = new Grid(originX, originZ, cellBlocks, across, factors, heights[highest], CentreX(highest), CentreZ(highest));
    }

    public double Factor(double x, double z)
    {
        var now = grid;
        if (now.Across == 0) return 0;
        var u = Math.Clamp((x - now.OriginX) / now.CellBlocks - 0.5, 0, now.Across - 1);
        var v = Math.Clamp((z - now.OriginZ) / now.CellBlocks - 0.5, 0, now.Across - 1);
        var (left, top) = ((int)Math.Floor(u), (int)Math.Floor(v));
        var (right, bottom) = (Math.Min(left + 1, now.Across - 1), Math.Min(top + 1, now.Across - 1));
        var (tu, tv) = (u - left, v - top);
        var upper = now.At(left, top) + (now.At(right, top) - now.At(left, top)) * tu;
        var lower = now.At(left, bottom) + (now.At(right, bottom) - now.At(left, bottom)) * tu;
        return upper + (lower - upper) * tv;
    }

    public int[] Pixels()
    {
        var factors = grid.Factors;
        var pixels = new int[factors.Length];
        for (var i = 0; i < pixels.Length; i++) pixels[i] = factors[i] | unchecked((int)0xff000000);
        return pixels;
    }

    private sealed record Grid(double OriginX, double OriginZ, int CellBlocks, int Across, byte[] Factors, double Height, double HighestX, double HighestZ)
    {
        public double At(int column, int row) => (double)Factors[row * Across + column] / Levels;
    }
}
