using System;
using System.Collections.Concurrent;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace RoughSeas.SeaAndWeather;

/// <summary>
/// The water a wind blows over: a straight line upwind to the shore, over the built world while it is built and over the
/// calibrated ocean map beyond it. Its length is in real metres, by the world's own horizontal compression (the real
/// pole-to-equator distance over the world's <c>polarEquatorDistance</c>); depths stay one block to the metre.
/// </summary>
public class WaterBody(ICoreServerAPI sapi, OceanBasins basins, WavesConfig config, Func<double> nowHours)
{
    private const int ChunkSize = GlobalConstants.ChunkSize;
    private const int CacheLimit = 65536;

    private readonly ConcurrentDictionary<(int X, int Z, int Direction), (double Hours, (bool, int?) Start, Reach Reach)> reaches = new(Environment.ProcessorCount, 0);
    private readonly double scale = config.RealPolarEquatorMetres / sapi.World.Config.GetString("polarEquatorDistance", "50000").ToInt(50000);

    public Reach Upwind(double x, double z, double towardRadians)
    {
        var direction = (int)Math.Round(towardRadians / GameMath.TWOPI * config.FetchDirections) % config.FetchDirections;
        if (direction < 0) direction += config.FetchDirections;
        var key = ((int)Math.Floor(x / config.FetchStepBlocks), (int)Math.Floor(z / config.FetchStepBlocks), direction);
        var now = nowHours();
        var start = Surface((int)x, (int)z, new BlockPos(Dimensions.NormalWorld));
        if (reaches.TryGetValue(key, out var cached) && cached.Start == start && now - cached.Hours < config.FetchCacheHours) return cached.Reach;
        if (reaches.Count > CacheLimit) reaches.Clear();
        var reach = Walk(x, z, direction * GameMath.TWOPI / config.FetchDirections);
        reaches[key] = (now, start, reach);
        return reach;
    }

    public void Forget() => reaches.Clear();

    public string Describe(double x, double z)
    {
        var probe = new BlockPos(Dimensions.NormalWorld);
        var (built, surface) = Surface((int)x, (int)z, probe);
        if (!built) return "not built here, so the ocean map answers";
        return surface is { } level
            ? $"water here at {level}, {Depth((int)x, (int)z, level, probe):0} m deep"
            : "built here, and no water at its surface";
    }

    private Reach Walk(double x, double z, double towardRadians)
    {
        var (towardX, towardZ) = Compass.Toward(towardRadians);
        var probe = new BlockPos(Dimensions.NormalWorld);
        var (built, surface) = Surface((int)x, (int)z, probe);
        var startDepth = built ? surface is { } level ? Depth((int)x, (int)z, level, probe) ?? 0 : 0 : OpenSea(x, z, config.OpenSeaDepthMetres);
        if (startDepth <= 0) return default;

        var water = surface ?? sapi.World.SeaLevel - 1;
        double fetch = 0, waterArea = 0, builtArea = 0, builtFetch = 0;
        while (fetch < config.MaxFetchBlocks)
        {
            var step = built ? config.FetchStepBlocks : config.OpenSeaStepBlocks;
            var atX = x - towardX * (fetch + step);
            var atZ = z - towardZ * (fetch + step);
            var measured = Depth((int)atX, (int)atZ, water, probe);
            built = measured != null;
            var depth = measured ?? OpenSea(atX, atZ, builtFetch > 0 ? builtArea / builtFetch : config.OpenSeaDepthMetres);
            if (depth <= 0) break;
            fetch += step;
            waterArea += depth * step;
            if (built) (builtArea, builtFetch) = (builtArea + depth * step, builtFetch + step);
        }
        return new Reach(fetch * scale, fetch > 0 ? waterArea / fetch : startDepth);
    }

    private (bool Built, int? Level) Surface(int x, int z, BlockPos probe)
    {
        if (x < 0 || z < 0) return (false, null);
        var chunk = sapi.WorldManager.GetMapChunk(x / ChunkSize, z / ChunkSize);
        if (chunk == null) return (false, null);
        var y = chunk.RainHeightMap[z % ChunkSize * ChunkSize + x % ChunkSize];
        probe.Set(x, y, z);
        if (sapi.World.BlockAccessor.GetChunkAtBlockPos(probe) == null) return (false, null);
        return (true, Liquid(probe) ? y : null);
    }

    private double? Depth(int x, int z, int surface, BlockPos probe)
    {
        if (x < 0 || z < 0) return null;
        var chunk = sapi.WorldManager.GetMapChunk(x / ChunkSize, z / ChunkSize);
        probe.Set(x, surface, z);
        if (chunk == null || sapi.World.BlockAccessor.GetChunkAtBlockPos(probe) == null) return null;
        var index = z % ChunkSize * ChunkSize + x % ChunkSize;
        if (chunk.RainHeightMap[index] != surface) return 0;
        if (!Liquid(probe)) return 0;
        var seabed = chunk.WorldGenTerrainHeightMap[index];
        return seabed < surface && Liquid(probe.Set(x, seabed + 1, z)) ? surface - seabed : Sounded(probe.Set(x, surface, z));
    }

    private bool Liquid(BlockPos at) => sapi.World.BlockAccessor.GetBlock(at, BlockLayersAccess.Fluid).IsLiquid();

    private int Sounded(BlockPos surface)
    {
        var depth = 1;
        while (depth < config.SoundingBlocks && Liquid(surface.Down())) depth++;
        return depth;
    }

    private double OpenSea(double x, double z, double depth) => basins.IsSea(x, z) ? depth : 0;
}

public readonly record struct Reach(double FetchMetres, double MeanDepthMetres);
