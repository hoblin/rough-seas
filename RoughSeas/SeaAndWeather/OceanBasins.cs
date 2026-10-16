using System;
using System.Collections.Concurrent;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;

namespace RoughSeas.SeaAndWeather;

/// <summary>
/// Open water west and east of any position, loaded or not: GenMaps' ocean map rebuilt from the world config, so its
/// settings must follow GenMaps.initWorldGen. Of the land vanilla forces at spawn and story locations, only the
/// spawn's centre is here.
/// </summary>
public class OceanBasins
{
    private static readonly int PixelSize = TerraGenConfig.oceanMapScale;
    private const int StripPixels = 256;
    private const int BlurPadding = 2;
    private const int CacheLimit = 4096;

    private readonly MapLayerBase oceans;
    private readonly int reachPixels;
    private readonly int landBelow;
    private readonly ConcurrentDictionary<(int StripX, int Row), Basin[]> strips = new(Environment.ProcessorCount, 0);

    public OceanBasins(long seed, ITreeAttribute worldConfig, int mapSizeX, int mapSizeZ, CurrentsConfig config)
    {
        oceans = GenMaps.GetOceanMapGen(
            seed + 1873,
            worldConfig.GetString("landcover", "1").ToFloat(1),
            TerraGenConfig.oceanMapScale,
            worldConfig.GetString("oceanscale", "1").ToFloat(1),
            [new XZ(mapSizeX / 2 / PixelSize, mapSizeZ / 2 / PixelSize)],
            false);
        reachPixels = config.BasinReachBlocks / PixelSize;
        landBelow = (int)(config.LandBelowOceanity * 255);
    }

    public Basin At(double x, double z)
    {
        var pixelX = (int)Math.Floor(x / PixelSize);
        var stripX = (int)Math.Floor((double)pixelX / StripPixels);
        var key = (stripX, (int)Math.Floor(z / PixelSize));
        if (!strips.TryGetValue(key, out var strip))
        {
            if (strips.Count > CacheLimit) strips.Clear();
            strip = strips.GetOrAdd(key, static (key, basins) => basins.Strip(key.StripX, key.Row), this);
        }
        return strip[pixelX - stripX * StripPixels];
    }

    private Basin[] Strip(int stripX, int row)
    {
        var margin = reachPixels + BlurPadding;
        var width = StripPixels + 2 * margin;
        var rows = 2 * BlurPadding + 1;
        int[] map;
        lock (oceans)
        {
            map = oceans.GenLayer(stripX * StripPixels - margin, row - BlurPadding, width, rows);
        }
        var line = BlurPadding * width;
        bool Sea(int i) => map[line + i] >= landBelow;

        var westLand = new int[width];
        var eastLand = new int[width];
        for (var i = 0; i < width; i++) westLand[i] = Sea(i) ? (i > 0 ? westLand[i - 1] : -1) : i;
        for (var i = width - 1; i >= 0; i--) eastLand[i] = Sea(i) ? (i < width - 1 ? eastLand[i + 1] : width) : i;

        var strip = new Basin[StripPixels];
        for (var j = 0; j < StripPixels; j++)
        {
            var i = margin + j;
            strip[j] = Sea(i)
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
}
