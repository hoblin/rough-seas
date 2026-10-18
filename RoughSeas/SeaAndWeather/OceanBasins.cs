using System;
using System.Collections.Concurrent;

namespace RoughSeas.SeaAndWeather;

public class OceanBasins
{
    private static readonly int PixelSize = WorldOceanMap.PixelSize;
    private const int StripPixels = 256;
    private const int CacheLimit = 4096;

    private readonly IOceanMap ocean;
    private readonly OceanCalibration calibration;
    private readonly int reachPixels;
    private readonly ConcurrentDictionary<(int StripX, int Row, int SeaFrom), Basin[]> strips = new(Environment.ProcessorCount, 0);

    public OceanBasins(IOceanMap ocean, OceanCalibration calibration, CurrentsConfig config)
    {
        this.ocean = ocean;
        this.calibration = calibration;
        reachPixels = config.BasinReachBlocks / PixelSize;
    }

    public bool IsSea(double x, double z) => calibration.IsSea(ocean.At(Pixel(x), Pixel(z)));

    public Basin At(double x, double z)
    {
        var pixelX = Pixel(x);
        var stripX = (int)Math.Floor((double)pixelX / StripPixels);
        var key = (stripX, Pixel(z), calibration.SeaFrom);
        if (!strips.TryGetValue(key, out var strip))
        {
            if (strips.Count > CacheLimit) strips.Clear();
            strip = strips.GetOrAdd(key, static (key, basins) => basins.Strip(key.StripX, key.Row), this);
        }
        return strip[pixelX - stripX * StripPixels];
    }

    private static int Pixel(double blocks) => (int)Math.Floor(blocks / PixelSize);

    private Basin[] Strip(int stripX, int row)
    {
        var start = stripX * StripPixels - reachPixels;
        var width = StripPixels + 2 * reachPixels;
        var sea = new bool[width];
        for (var i = 0; i < width; i++) sea[i] = calibration.IsSea(ocean.At(start + i, row));

        var westLand = new int[width];
        var eastLand = new int[width];
        for (var i = 0; i < width; i++) westLand[i] = sea[i] ? (i > 0 ? westLand[i - 1] : -1) : i;
        for (var i = width - 1; i >= 0; i--) eastLand[i] = sea[i] ? (i < width - 1 ? eastLand[i + 1] : width) : i;

        var strip = new Basin[StripPixels];
        for (var j = 0; j < StripPixels; j++)
        {
            var i = reachPixels + j;
            strip[j] = sea[i]
                ? new Basin(Reach(i - westLand[i]), Reach(eastLand[i] - i))
                : default;
        }
        return strip;
    }

    private double Reach(int pixels) => (Math.Min(pixels, reachPixels) - 0.5) * PixelSize;
}

public readonly record struct Basin(double WestBlocks, double EastBlocks)
{
    public bool IsSea => WestBlocks > 0;

    public double Width => WestBlocks + EastBlocks;

    public double FromWest => WestBlocks / Width;

    public double ShoreBlocks => Math.Min(WestBlocks, EastBlocks);
}
