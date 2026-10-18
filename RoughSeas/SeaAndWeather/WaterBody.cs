using System;
using System.Collections.Concurrent;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace RoughSeas.SeaAndWeather;

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
        var startDepth = built ? surface is { } level ? Depth((int)x, (int)z, level, probe) ?? 0 : 0 : OpenSea(x, z);
        if (startDepth <= 0) return default;

        double fetch = 0, depthSum = startDepth;
        var samples = 1;
        while (fetch < config.MaxFetchBlocks)
        {
            var step = built ? config.FetchStepBlocks : config.OpenSeaStepBlocks;
            var atX = x - towardX * (fetch + step);
            var atZ = z - towardZ * (fetch + step);
            var measured = built ? Depth((int)atX, (int)atZ, surface!.Value, probe) : null;
            built = measured != null;
            var depth = measured ?? OpenSea(atX, atZ);
            if (depth <= 0) break;
            fetch += step;
            depthSum += depth;
            samples++;
        }
        return new Reach(fetch * scale, depthSum / samples);
    }

    private (bool Built, int? Level) Surface(int x, int z, BlockPos probe)
    {
        var chunk = sapi.WorldManager.GetMapChunk(x / ChunkSize, z / ChunkSize);
        if (chunk == null) return (false, null);
        var y = chunk.RainHeightMap[z % ChunkSize * ChunkSize + x % ChunkSize];
        probe.Set(x, y, z);
        if (sapi.World.BlockAccessor.GetChunkAtBlockPos(probe) == null) return (false, null);
        return (true, sapi.World.BlockAccessor.GetBlock(probe, BlockLayersAccess.Fluid).IsLiquid() ? y : null);
    }

    private double? Depth(int x, int z, int surface, BlockPos probe)
    {
        var chunk = sapi.WorldManager.GetMapChunk(x / ChunkSize, z / ChunkSize);
        probe.Set(x, surface, z);
        if (chunk == null || sapi.World.BlockAccessor.GetChunkAtBlockPos(probe) == null) return null;
        var index = z % ChunkSize * ChunkSize + x % ChunkSize;
        if (chunk.RainHeightMap[index] != surface) return 0;
        if (!sapi.World.BlockAccessor.GetBlock(probe, BlockLayersAccess.Fluid).IsLiquid()) return 0;
        var seabed = chunk.WorldGenTerrainHeightMap[index];
        return seabed < surface ? surface - seabed : Sounded(probe);
    }

    private int Sounded(BlockPos surface)
    {
        var depth = 1;
        while (depth < config.SoundingBlocks && sapi.World.BlockAccessor.GetBlock(surface.Down(), BlockLayersAccess.Fluid).IsLiquid()) depth++;
        return depth;
    }

    private double OpenSea(double x, double z) => basins.IsSea(x, z) ? config.OpenSeaDepthMetres : 0;
}

public readonly record struct Reach(double FetchMetres, double MeanDepthMetres);
