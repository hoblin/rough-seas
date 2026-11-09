using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using RoughSeas.SeaAndWeather;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RoughSeas.Shore;

public sealed class Groundwater
{
    private const int ChunkSize = GlobalConstants.ChunkSize;
    private const int TickMilliseconds = 100;
    private const int StillLevel = 7;

    private static readonly string[] Families = ["water", "saltwater"];
    private static readonly AccessTools.FieldRef<BlockBehaviorFiniteSpreadingLiquid, int> SpreadDelay =
        AccessTools.FieldRefAccess<BlockBehaviorFiniteSpreadingLiquid, int>("spreadDelay");

    private readonly ICoreServerAPI sapi;
    private readonly SeaWeatherSystem sea;
    private readonly ShoreConfig config;
    private readonly Action<string, object[]> log;
    private readonly Band band;
    private readonly Beds beds;
    private readonly Dictionary<(int X, int Z), Dictionary<int, List<ShoreSeed>>> seeds = [];
    private readonly Queue<(int X, int Z)> surveys = new();
    private readonly HashSet<(int X, int Z)> surveying = [];
    private readonly Queue<(int X, int Z)> bands = new();
    private readonly HashSet<(int X, int Z)> banding = [];
    private readonly PriorityQueue<(BlockPos Pos, int Fluid), long> writes = new();
    private readonly Dictionary<int, int> carriers = [];
    private double refreshedHours = double.NaN;

    public Groundwater(ICoreServerAPI sapi, SeaWeatherSystem sea, ShoreConfig config, Action<string, object[]> log)
    {
        (this.sapi, this.sea, this.config, this.log) = (sapi, sea, config, log);
        beds = new Beds(config.Beds);
        band = new Band(new RunUp(config.RunUp), config.Sheet, config.RunUp.SlopeSearchBlocks);
    }

    public TickBudget Budget { get; } = new("shore");

    public static bool Carries(Block fluid) => fluid.IsLiquid() && Families.Contains(fluid.FirstCodePart());

    public void Start()
    {
        Budget.CountTicks(sapi);
        sapi.Event.ChunkColumnLoaded += (coord, _) => Survey(coord.X, coord.Y);
        sapi.Event.ChunkColumnUnloaded += coord => seeds.Remove((coord.X, coord.Z));
        sapi.Event.DidPlaceBlock += (_, _, selection, _) => Changed(selection.Position);
        sapi.Event.DidBreakBlock += (_, _, selection) => Changed(selection.Position);
        sapi.Event.RegisterGameTickListener(_ => Tick(), TickMilliseconds);
    }

    public void Changed(BlockPos pos)
    {
        var (chunkX, chunkZ) = (pos.X / ChunkSize, pos.Z / ChunkSize);
        Survey(chunkX, chunkZ);
        for (var dx = -1; dx <= 1; dx++)
        for (var dz = -1; dz <= 1; dz++) Enqueue(bands, banding, (chunkX + dx, chunkZ + dz));
    }

    public void Stirred(BlockPos pos) => Survey(pos.X / ChunkSize, pos.Z / ChunkSize);

    public string Describe(BlockPos pos)
    {
        var level = pos.Y;
        var ground = new ShoreGround(sapi.World.BlockAccessor, level, beds, config.Band.HighestRiseBlocks);
        var cell = ground.At(pos.X, pos.Z);
        var nearest = SeedsAround(pos.X / ChunkSize, pos.Z / ChunkSize, level)
            .MinBy(seed => (seed.X - pos.X) * (seed.X - pos.X) + (seed.Z - pos.Z) * (seed.Z - pos.Z));
        if (nearest == default) return $"no shore at level {level} within {config.Band.HaloBlocks} blocks; here: {cell.Kind}";
        var seas = Seas(nearest.X, nearest.Z);
        var reaches = string.Join(", ", seas.Select(state => $"{state.Height:F1} m every {state.Period:F1} s runs up {band.RunUpFrom(ground, nearest, state):F2} m"));
        return $"here: {cell.Kind}, {cell.Elevation:F2} m over still water, bed {cell.Bed.Material}; nearest shore {nearest.X - sapi.World.DefaultSpawnPosition.X:0} {nearest.Z - sapi.World.DefaultSpawnPosition.Z:0}: {reaches}";
    }

    private void Survey(int chunkX, int chunkZ) => Enqueue(surveys, surveying, (chunkX, chunkZ));

    private static void Enqueue(Queue<(int X, int Z)> queue, HashSet<(int X, int Z)> queued, (int X, int Z) chunk)
    {
        lock (queue)
        {
            if (queued.Add(chunk)) queue.Enqueue(chunk);
        }
    }

    private static bool Dequeue(Queue<(int X, int Z)> queue, HashSet<(int X, int Z)> queued, out (int X, int Z) chunk)
    {
        lock (queue)
        {
            if (!queue.TryDequeue(out chunk)) return false;
            queued.Remove(chunk);
            return true;
        }
    }

    private void Tick()
    {
        using var _ = Budget.Measure();
        var budget = Stopwatch.StartNew();
        Write();
        Refresh();
        while (budget.ElapsedMilliseconds < config.Band.TickBudgetMilliseconds && Dequeue(surveys, surveying, out var chunk)) Surveyed(chunk);
        while (budget.ElapsedMilliseconds < config.Band.TickBudgetMilliseconds && Dequeue(bands, banding, out var chunk)) Spread(chunk);
    }

    private void Refresh()
    {
        var now = sea.Clock.Hours;
        if (!double.IsNaN(refreshedHours) && now - refreshedHours < config.Band.RefreshMinutes / 60) return;
        refreshedHours = now;
        foreach (var chunk in Shores()) Enqueue(bands, banding, chunk);
    }

    private IEnumerable<(int X, int Z)> Shores()
    {
        var reach = Halo;
        return seeds.Keys
            .SelectMany(chunk => Enumerable.Range(-reach, 2 * reach + 1).SelectMany(dx => Enumerable.Range(-reach, 2 * reach + 1), (dx, dz) => (chunk.X + dx, chunk.Z + dz)))
            .Distinct()
            .Where(chunk => sapi.WorldManager.GetMapChunk(chunk.Item1, chunk.Item2) != null);
    }

    private int Halo => (config.Band.HaloBlocks + ChunkSize - 1) / ChunkSize;

    private void Surveyed((int X, int Z) chunk)
    {
        var map = sapi.WorldManager.GetMapChunk(chunk.X, chunk.Z);
        if (map == null) return;
        var accessor = sapi.World.BlockAccessor;
        var found = new Dictionary<int, List<ShoreSeed>>();
        var probe = new BlockPos(Dimensions.NormalWorld);
        for (var i = 0; i < ChunkSize * ChunkSize; i++)
        {
            var (x, z) = (chunk.X * ChunkSize + i % ChunkSize, chunk.Z * ChunkSize + i / ChunkSize);
            var y = (int)map.RainHeightMap[i];
            probe.Set(x, y, z);
            if (accessor.GetChunkAtBlockPos(probe) == null) continue;
            var fluid = accessor.GetBlock(probe, BlockLayersAccess.Fluid);
            if (!Carries(fluid) || OpenWater.Land(accessor, probe)) continue;
            if (!BlockFacing.HORIZONTALS.Any(side => OpenWater.Land(accessor, probe.Set(x + side.Normali.X, y, z + side.Normali.Z)))) continue;
            if (!found.TryGetValue(y, out var level)) found[y] = level = [];
            level.Add(new ShoreSeed(x, z, Carrier(fluid)));
        }

        var known = seeds.GetValueOrDefault(chunk);
        if (found.Count > 0) seeds[chunk] = found;
        else seeds.Remove(chunk);
        if (Same(known, found)) return;
        log("chunk {0} {1}: shore at {2}", [chunk.X, chunk.Z, found.Count > 0 ? string.Join(", ", found.Select(level => $"{level.Value.Count} cells of level {level.Key}")) : "none"]);
        for (var dx = -Halo; dx <= Halo; dx++)
        for (var dz = -Halo; dz <= Halo; dz++) Enqueue(bands, banding, (chunk.X + dx, chunk.Z + dz));
    }

    private static bool Same(Dictionary<int, List<ShoreSeed>>? known, Dictionary<int, List<ShoreSeed>> found) =>
        known != null && known.Count == found.Count && found.All(level => known.TryGetValue(level.Key, out var seen) && seen.SequenceEqual(level.Value));

    private void Spread((int X, int Z) chunk)
    {
        if (sapi.WorldManager.GetMapChunk(chunk.X, chunk.Z) == null) return;
        var levels = Enumerable.Range(-Halo, 2 * Halo + 1)
            .SelectMany(dx => Enumerable.Range(-Halo, 2 * Halo + 1), (dx, dz) => seeds.GetValueOrDefault((chunk.X + dx, chunk.Z + dz)))
            .Where(found => found != null)
            .SelectMany(found => found!.Keys)
            .ToHashSet();
        foreach (var level in levels.Concat(Held(chunk)).Distinct()) Spread(chunk, level);
    }

    private IEnumerable<int> Held((int X, int Z) chunk)
    {
        var map = sapi.WorldManager.GetMapChunk(chunk.X, chunk.Z);
        if (map == null) return [];
        var accessor = sapi.World.BlockAccessor;
        var probe = new BlockPos(Dimensions.NormalWorld);
        var held = new HashSet<int>();
        foreach (var level in seeds.Values.SelectMany(found => found.Keys).Distinct())
        {
            for (var i = 0; i < ChunkSize * ChunkSize && !held.Contains(level); i++)
            {
                probe.Set(chunk.X * ChunkSize + i % ChunkSize, level, chunk.Z * ChunkSize + i / ChunkSize);
                if (Carries(accessor.GetBlock(probe, BlockLayersAccess.Fluid)) && OpenWater.Land(accessor, probe)) held.Add(level);
            }
        }
        return held;
    }

    private IEnumerable<ShoreSeed> SeedsAround(int chunkX, int chunkZ, int level) =>
        Enumerable.Range(-Halo, 2 * Halo + 1)
            .SelectMany(dx => Enumerable.Range(-Halo, 2 * Halo + 1), (dx, dz) => seeds.GetValueOrDefault((chunkX + dx, chunkZ + dz)))
            .SelectMany(found => found?.GetValueOrDefault(level) ?? []);

    private void Spread((int X, int Z) chunk, int level)
    {
        var started = Stopwatch.StartNew();
        var accessor = sapi.World.BlockAccessor;
        var ground = new ShoreGround(accessor, level, beds, config.Band.HighestRiseBlocks);
        var memo = new Dictionary<(int, int), IReadOnlyList<Sea>>();
        var reached = band.Spread(ground, SeedsAround(chunk.X, chunk.Z, level),
            (x, z) => memo.TryGetValue((x >> 4, z >> 4), out var known) ? known : memo[(x >> 4, z >> 4)] = Seas(x, z));

        var (added, drained, rings) = (0, 0, 0);
        var now = sapi.World.ElapsedMilliseconds;
        for (var i = 0; i < ChunkSize * ChunkSize; i++)
        {
            var (x, z) = (chunk.X * ChunkSize + i % ChunkSize, chunk.Z * ChunkSize + i / ChunkSize);
            var cell = ground.At(x, z);
            if (cell.Kind != ShoreKind.Land) continue;
            var held = cell.Fluid != 0 && Carries(accessor.GetBlock(cell.Fluid));
            if (reached.TryGetValue((x, z), out var wanted))
            {
                if (held) continue;
                writes.Enqueue((new BlockPos(x, level, z, Dimensions.NormalWorld), wanted.Fluid), now + wanted.Rings * Delay(wanted.Fluid));
                (added, rings) = (added + 1, Math.Max(rings, wanted.Rings));
            }
            else if (held)
            {
                writes.Enqueue((new BlockPos(x, level, z, Dimensions.NormalWorld), 0), now + Delay(cell.Fluid));
                drained++;
            }
        }
        if (added + drained == 0) return;
        log("chunk {0} {1} level {2}: {3} cells take water, {4} drain, {5} blocks in from the sea; {6} ms",
            [chunk.X, chunk.Z, level, added, drained, rings, started.ElapsedMilliseconds]);
    }

    private IReadOnlyList<Sea> Seas(int x, int z)
    {
        var model = sea.Model;
        var (height, period) = model.WavesAt(x + 0.5, z + 0.5);
        var (everydayHeight, everydayPeriod) = model.EverydayWavesAt(x + 0.5, z + 0.5);
        return [new Sea(height, period), new Sea(everydayHeight, everydayPeriod)];
    }

    private void Write()
    {
        var now = sapi.World.ElapsedMilliseconds;
        var accessor = sapi.World.BlockAccessor;
        while (writes.TryPeek(out var write, out var due) && due <= now)
        {
            writes.Dequeue();
            if (accessor.GetChunkAtBlockPos(write.Pos) == null || !OpenWater.Land(accessor, write.Pos)) continue;
            accessor.SetBlock(write.Fluid, write.Pos, BlockLayersAccess.Fluid);
        }
    }

    private int Carrier(Block fluid)
    {
        if (carriers.TryGetValue(fluid.Id, out var known)) return known;
        var still = sapi.World.GetBlock(new AssetLocation(fluid.Code.Domain, $"{fluid.FirstCodePart()}-still-{StillLevel}"));
        return carriers[fluid.Id] = still?.Id ?? fluid.Id;
    }

    private int Delay(int fluid) =>
        sapi.World.GetBlock(fluid).GetBehavior<BlockBehaviorFiniteSpreadingLiquid>() is { } spreading ? SpreadDelay(spreading) : 0;
}
