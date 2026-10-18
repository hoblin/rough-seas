using System;
using RoughSeas.SeaAndWeather;

namespace RoughSeas.Storms;

/// <summary>
/// The waves around the player, each cell a share of the highest, read as the shader reads <c>seaHeightMap</c> (linear,
/// clamped at the edge); the other end is <c>assets/roughseas/shaderincludes/seasurface.vsh</c>.
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

    public double Period { get; private set; }

    public int Across => across;

    public void Update(ISeaSource source, double x, double z, int regionSize, int cellBlocks)
    {
        this.cellBlocks = cellBlocks;
        across = RegionsAcross * regionSize / cellBlocks;
        if (factors.Length != across * across) factors = new byte[across * across];
        OriginX = (Math.Floor(x / regionSize) - 1) * regionSize;
        OriginZ = (Math.Floor(z / regionSize) - 1) * regionSize;

        var heights = new double[across * across];
        var (highest, period) = (0.0, 0.0);
        for (var i = 0; i < heights.Length; i++)
        {
            var sample = source.Sample(OriginX + (i % across + 0.5) * cellBlocks, OriginZ + (i / across + 0.5) * cellBlocks);
            heights[i] = sample.WaveHeight;
            if (sample.WaveHeight > highest) (highest, period) = (sample.WaveHeight, sample.WavePeriod);
        }
        (Height, Period) = (highest, period);
        for (var i = 0; i < heights.Length; i++) factors[i] = (byte)Math.Round(highest > 0 ? heights[i] / highest * Levels : 0);
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

    private double At(int column, int row) => factors[row * across + column] / (double)Levels;
}
