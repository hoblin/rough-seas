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

    private readonly ConcurrentDictionary<(int X, int Z, int Direction), (double Hours, Reach Reach)> reaches = new(Environment.ProcessorCount, 0);
    private readonly double scale = config.RealPolarEquatorMetres / sapi.World.Config.GetString("polarEquatorDistance", "50000").ToInt(50000);

    public Reach Upwind(double x, double z, double towardRadians)
    {
        var direction = (int)Math.Round(towardRadians / GameMath.TWOPI * config.FetchDirections) % config.FetchDirections;
        if (direction < 0) direction += config.FetchDirections;
        var key = ((int)Math.Floor(x / config.FetchStepBlocks), (int)Math.Floor(z / config.FetchStepBlocks), direction);
        var now = nowHours();
        if (reaches.TryGetValue(key, out var cached) && now - cached.Hours < config.FetchCacheHours) return cached.Reach;
        if (reaches.Count > CacheLimit) reaches.Clear();
        var reach = Walk(x, z, direction * GameMath.TWOPI / config.FetchDirections);
        reaches[key] = (now, reach);
        return reach;
    }

    private Reach Walk(double x, double z, double towardRadians)
    {
        var (towardX, towardZ) = Compass.Toward(towardRadians);
        var probe = new BlockPos(Dimensions.NormalWorld);
        var surface = Surface((int)x, (int)z, probe);
        var startDepth = surface is { } level ? Depth((int)x, (int)z, level, probe) ?? 0 : OpenSea(x, z);
        if (startDepth <= 0) return default;

        double fetch = 0, depthSum = startDepth;
        var samples = 1;
        var built = surface != null;
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

    private int? Surface(int x, int z, BlockPos probe)
    {
        var chunk = sapi.WorldManager.GetMapChunk(x / ChunkSize, z / ChunkSize);
        if (chunk == null) return null;
        var y = chunk.RainHeightMap[z % ChunkSize * ChunkSize + x % ChunkSize];
        probe.Set(x, y, z);
        if (sapi.World.BlockAccessor.GetChunkAtBlockPos(probe) == null) return null;
        return sapi.World.BlockAccessor.GetBlock(probe, BlockLayersAccess.Fluid).IsLiquid() ? y : null;
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
        return seabed < surface ? surface - seabed : 1;
    }

    private double OpenSea(double x, double z) => basins.IsSea(x, z) ? config.OpenSeaDepthMetres : 0;
}

public readonly record struct Reach(double FetchMetres, double MeanDepthMetres);
