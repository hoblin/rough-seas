using System;
using RoughSeas.SeaAndWeather;

namespace RoughSeas.Storms;

/// <summary>
/// The waves of the player's region and its eight neighbours, each cell a share of the highest. Read as the shader reads
/// <c>seaHeightMap</c> (linear, clamped at the edge); the other end is <c>assets/roughseas/shaderincludes/seasurface.vsh</c>.
/// </summary>
public sealed class SurfaceField
{
    private const int RegionsAcross = 3;
    private const int Levels = 255;

    private int cellBlocks;
    private int across;
    private byte[] factors = [];

    public double OriginX { get; private set; }

    public double OriginZ { get; private set; }

    public double SizeBlocks => across * cellBlocks;

    public double Height { get; private set; }

    public double HighestX { get; private set; }

    public double HighestZ { get; private set; }

    public int Across => across;

    public void Update(ISeaSource source, double x, double z, int regionSize, int cellBlocks)
    {
        this.cellBlocks = cellBlocks;
        across = RegionsAcross * regionSize / cellBlocks;
        if (factors.Length != across * across) factors = new byte[across * across];
        OriginX = (Math.Floor(x / regionSize) - 1) * regionSize;
        OriginZ = (Math.Floor(z / regionSize) - 1) * regionSize;

        var heights = new double[across * across];
        var highest = 0;
        for (var i = 0; i < heights.Length; i++)
        {
            heights[i] = source.Sample(CentreX(i), CentreZ(i)).WaveHeight;
            if (heights[i] > heights[highest]) highest = i;
        }
        (Height, HighestX, HighestZ) = (heights[highest], CentreX(highest), CentreZ(highest));
        for (var i = 0; i < heights.Length; i++)
        {
            factors[i] = (byte)Math.Round(Height > 0 ? heights[i] / Height * Levels : 0);
        }
    }

    public double Factor(double x, double z)
    {
        if (across == 0) return 0;
        var u = Math.Clamp((x - OriginX) / cellBlocks - 0.5, 0, across - 1);
        var v = Math.Clamp((z - OriginZ) / cellBlocks - 0.5, 0, across - 1);
        var (left, top) = ((int)Math.Floor(u), (int)Math.Floor(v));
        var (right, bottom) = (Math.Min(left + 1, across - 1), Math.Min(top + 1, across - 1));
        var (tu, tv) = (u - left, v - top);
        var upper = At(left, top) + (At(right, top) - At(left, top)) * tu;
        var lower = At(left, bottom) + (At(right, bottom) - At(left, bottom)) * tu;
        return upper + (lower - upper) * tv;
    }

    public int[] Pixels()
    {
        var pixels = new int[factors.Length];
        for (var i = 0; i < pixels.Length; i++) pixels[i] = factors[i] | unchecked((int)0xff000000);
        return pixels;
    }

    private double At(int column, int row) => (double)factors[row * across + column] / Levels;

    private double CentreX(int cell) => OriginX + (cell % across + 0.5) * cellBlocks;

    private double CentreZ(int cell) => OriginZ + (cell / across + 0.5) * cellBlocks;
}
