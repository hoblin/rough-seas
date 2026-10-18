using System;
using System.Collections.Concurrent;
using Vintagestory.ServerMods;

namespace RoughSeas.SeaAndWeather;

public class GeneratedOcean(Func<MapLayerBase?> generator) : IOceanMap
{
    private const int TilePixels = 16;
    private const int Padding = 5;
    private const int CacheLimit = 2048;

    private readonly ConcurrentDictionary<(int X, int Z), int[]> tiles = new(Environment.ProcessorCount, 0);
    private MapLayerBase? layer;

    public int At(int pixelX, int pixelZ)
    {
        if ((layer ??= generator()) == null) return 0;
        var tileX = (int)Math.Floor((double)pixelX / TilePixels);
        var tileZ = (int)Math.Floor((double)pixelZ / TilePixels);
        var x = pixelX - tileX * TilePixels;
        var z = pixelZ - tileZ * TilePixels;
        if (!tiles.TryGetValue((tileX, tileZ), out var tile))
        {
            if (tiles.Count > CacheLimit) tiles.Clear();
            tile = tiles.GetOrAdd((tileX, tileZ), static (key, ocean) => ocean.Generate(key.X, key.Z), this);
        }
        return tile[(z + Padding) * (TilePixels + 2 * Padding) + x + Padding];
    }

    private int[] Generate(int tileX, int tileZ)
    {
        var size = TilePixels + 2 * Padding;
        lock (layer!)
        {
            return layer.GenLayer(tileX * TilePixels - Padding, tileZ * TilePixels - Padding, size, size);
        }
    }
}
