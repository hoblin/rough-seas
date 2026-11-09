using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using RoughSeas.SeaAndWeather;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RoughSeas.Shore;

/// <summary>
/// Vanilla's own still water, salt or fresh, in the fluid layer under the full blocks of a shore at its still-water level,
/// as far as the sheet a wave throws up the shore reaches (<see cref="Band"/>): the higher of the everyday sea's and the
/// sea's now. The band's reach is worked out again rather than recorded, once for each water level, off the main thread:
/// when a chunk loads, when a block changes in or beside the band, and when the sea at its shore changes. The water it
/// lays is vanilla's and is saved with the chunks; each cell is wetted or drained to match after its water's own spread delay.
/// </summary>
public sealed class Groundwater
{
    private const int ChunkSize = GlobalConstants.ChunkSize;
    private const int TickMilliseconds = 100;
    private const int StillLevel = 7;
    private const double SeaChange = 0.05;
    private const int SeaCell = 8;

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
    private readonly HashSet<(int X, int Z)> pending = [];
    private readonly Dictionary<int, int> versions = [];
    private readonly Dictionary<int, Flood> floods = [];
    private readonly PriorityQueue<(int X, int Y, int Z), long> writes = new();
    private readonly Dictionary<(int X, int Y, int Z), int> planned = [];
    private readonly Dictionary<int, int> carriers = [];
    private readonly Dictionary<(int X, int Z), IReadOnlyList<Sea>> seasNow = [];
    private readonly Dictionary<(int X, int Z), Sea> seaOfShore = [];
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
        sapi.Event.ChunkColumnLoaded += (coord, _) => Survey((coord.X, coord.Y));
        sapi.Event.ChunkColumnUnloaded += coord => seaOfShore.Remove((coord.X, coord.Z));
        sapi.Event.DidPlaceBlock += (_, _, selection, _) => Changed(selection.Position);
        sapi.Event.DidBreakBlock += (_, _, selection) => Changed(selection.Position);
        sapi.Event.RegisterGameTickListener(_ => Tick(), TickMilliseconds);
    }

    public void Changed(BlockPos pos)
    {
        var chunk = (pos.X / ChunkSize, pos.Z / ChunkSize);
        Survey(chunk);
        foreach (var level in LevelsAround(chunk).Where(level => pos.Y >= level && pos.Y <= level + config.Band.HighestRiseBlocks).ToList())
        {
            Stale(level, chunk);
        }
    }

    public void Stirred(BlockPos pos) => Survey((pos.X / ChunkSize, pos.Z / ChunkSize));

    public string Describe(BlockPos pos)
    {
        var level = pos.Y;
        var ground = new ShoreGround(sapi.World.BlockAccessor, level, beds, config.Band.HighestRiseBlocks);
        var cell = ground.At(pos.X, pos.Z);
        var held = sapi.World.BlockAccessor.GetBlock(pos, BlockLayersAccess.Fluid).Code?.ToShortString() ?? "nothing";
        var nearest = SeedsAround((pos.X / ChunkSize, pos.Z / ChunkSize), level)
            .OrderBy(seed => (seed.X - pos.X) * (seed.X - pos.X) + (seed.Z - pos.Z) * (seed.Z - pos.Z)).FirstOrDefault();
        if (nearest == default) return $"no shore at level {level} within {config.Band.HaloBlocks} blocks; here: {cell.Kind} holding {held}";
        var reaches = string.Join(", ", Seas(nearest.X, nearest.Z).Select(state =>
            $"{state.Height:F1} m every {state.Period:F1} s runs up {band.RunUpFrom(ground, nearest, state):F2} m"));
        return $"here: {cell.Kind} holding {held}, {cell.Elevation:F2} m over still water, bed {cell.Bed?.Material}; "
            + $"nearest shore {nearest.X - sapi.World.DefaultSpawnPosition.X:0} {nearest.Z - sapi.World.DefaultSpawnPosition.Z:0}: {reaches}";
    }

    private void Survey((int X, int Z) chunk)
    {
        if (surveying.Add(chunk)) surveys.Enqueue(chunk);
    }

    private void Stale(int level, (int X, int Z) around)
    {
        versions[level] = versions.GetValueOrDefault(level) + 1;
        for (var dx = -Halo; dx <= Halo; dx++)
        for (var dz = -Halo; dz <= Halo; dz++) pending.Add((around.X + dx, around.Z + dz));
    }

    private int Halo => (config.Band.HaloBlocks + ChunkSize - 1) / ChunkSize;

    private void Tick()
    {
        using var _ = Budget.Measure();
        var budget = Stopwatch.StartNew();
        bool Spare() => budget.ElapsedMilliseconds < config.Band.TickBudgetMilliseconds;
        Write(Spare);
        Refresh();
        while (Spare() && surveys.TryDequeue(out var chunk))
        {
            surveying.Remove(chunk);
            Surveyed(chunk);
        }
        foreach (var chunk in pending.ToList())
        {
            if (!Spare()) break;
            if (Diffed(chunk)) pending.Remove(chunk);
        }
    }

    private void Refresh()
    {
        var now = sea.Clock.Hours;
        if (!double.IsNaN(refreshedHours) && now - refreshedHours < config.Band.RefreshMinutes / 60) return;
        refreshedHours = now;
        seasNow.Clear();
        foreach (var gone in seeds.Keys.Where(chunk => !Loaded(chunk)).ToList()) seeds.Remove(gone);
        foreach (var (chunk, found) in seeds.ToList())
        {
            var strongest = found.Values.SelectMany(level => level).Select(seed => Seas(seed.X, seed.Z)[0]).MaxBy(state => state.Height);
            if (seaOfShore.TryGetValue(chunk, out var known)
                && Math.Abs(known.Height - strongest.Height) < SeaChange && Math.Abs(known.Period - strongest.Period) < SeaChange) continue;
            seaOfShore[chunk] = strongest;
            foreach (var level in found.Keys) Stale(level, chunk);
        }
    }

    private bool Loaded((int X, int Z) chunk)
    {
        for (var dx = -Halo; dx <= Halo; dx++)
        for (var dz = -Halo; dz <= Halo; dz++)
        {
            if (sapi.WorldManager.GetMapChunk(chunk.X + dx, chunk.Z + dz) != null) return true;
        }
        return false;
    }

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
            if (!Carries(fluid) || OpenWater.Land(accessor, probe) || !Shore(accessor, probe, x, y, z)) continue;
            if (!found.TryGetValue(y, out var level)) found[y] = level = [];
            level.Add(new ShoreSeed(x, z, Carrier(fluid)));
        }

        var known = seeds.GetValueOrDefault(chunk);
        if (found.Count > 0) seeds[chunk] = found;
        else seeds.Remove(chunk);
        if (Same(known, found)) return;
        log("chunk {0} {1}: shore at {2}", [chunk.X, chunk.Z, found.Count > 0 ? string.Join(", ", found.Select(level => $"{level.Value.Count} cells of level {level.Key}")) : "none"]);
        foreach (var level in found.Keys.Concat((IEnumerable<int>?)known?.Keys ?? []).Distinct()) Stale(level, chunk);
    }

    private static bool Shore(IBlockAccessor accessor, BlockPos probe, int x, int y, int z)
    {
        foreach (var side in BlockFacing.HORIZONTALS)
        {
            if (OpenWater.Land(accessor, probe.Set(x + side.Normali.X, y, z + side.Normali.Z))) return true;
        }
        return false;
    }

    private static bool Same(Dictionary<int, List<ShoreSeed>>? known, Dictionary<int, List<ShoreSeed>> found) =>
        known != null && known.Count == found.Count && found.All(level => known.TryGetValue(level.Key, out var seen) && seen.SequenceEqual(level.Value));

    private IEnumerable<int> LevelsAround((int X, int Z) chunk) =>
        Enumerable.Range(-Halo, 2 * Halo + 1)
            .SelectMany(dx => Enumerable.Range(-Halo, 2 * Halo + 1), (dx, dz) => seeds.GetValueOrDefault((chunk.X + dx, chunk.Z + dz)))
            .SelectMany(found => found?.Keys ?? Enumerable.Empty<int>())
            .Distinct();

    private IEnumerable<ShoreSeed> SeedsAround((int X, int Z) chunk, int level) =>
        Enumerable.Range(-Halo, 2 * Halo + 1)
            .SelectMany(dx => Enumerable.Range(-Halo, 2 * Halo + 1), (dx, dz) => seeds.GetValueOrDefault((chunk.X + dx, chunk.Z + dz)))
            .SelectMany(found => found?.GetValueOrDefault(level) ?? []);

    private bool Diffed((int X, int Z) chunk)
    {
        if (sapi.WorldManager.GetMapChunk(chunk.X, chunk.Z) == null) return true;
        var done = true;
        foreach (var level in LevelsAround(chunk).Concat(versions.Keys).Distinct().ToList())
        {
            if (Flooded(level) is { } reached) Diff(chunk, level, reached);
            else done = false;
        }
        return done;
    }

    private Dictionary<(int X, int Z), Reached>? Flooded(int level)
    {
        var version = versions.GetValueOrDefault(level);
        if (floods.TryGetValue(level, out var flood))
        {
            if (!flood.Task.IsCompleted) return null;
            if (flood.Version == version)
            {
                if (flood.Task.IsCompletedSuccessfully) return flood.Task.Result;
                log("level {0}: the band failed to spread: {1}", [level, flood.Task.Exception?.GetBaseException().ToString() ?? "cancelled"]);
                return [];
            }
        }

        var shore = seeds.Values.SelectMany(found => found.GetValueOrDefault(level) ?? []).ToList();
        var seas = new Dictionary<(int X, int Z), IReadOnlyList<Sea>>();
        foreach (var seed in shore) seas[(seed.X / SeaCell, seed.Z / SeaCell)] = Seas(seed.X, seed.Z);
        var ground = new ShoreGround(sapi.World.GetLockFreeBlockAccessor(), level, beds, config.Band.HighestRiseBlocks);
        floods[level] = new Flood(Task.Run(() =>
        {
            var started = Stopwatch.StartNew();
            var reached = band.Spread(ground, shore, (x, z) => seas[(x / SeaCell, z / SeaCell)]);
            log("level {0}: the band spreads from {1} cells of shore over {2} of land; {3} ms", [level, shore.Count, reached.Count, started.ElapsedMilliseconds]);
            return reached;
        }), version);
        return null;
    }

    private void Diff((int X, int Z) chunk, int level, Dictionary<(int X, int Z), Reached> reached)
    {
        var accessor = sapi.World.BlockAccessor;
        var probe = new BlockPos(Dimensions.NormalWorld);
        var now = sapi.World.ElapsedMilliseconds;
        var (added, drained, rings) = (0, 0, 0);
        for (var i = 0; i < ChunkSize * ChunkSize; i++)
        {
            var (x, z) = (chunk.X * ChunkSize + i % ChunkSize, chunk.Z * ChunkSize + i / ChunkSize);
            probe.Set(x, level, z);
            if (accessor.GetChunkAtBlockPos(probe) == null || !OpenWater.Land(accessor, probe)) continue;
            var fluid = accessor.GetBlock(probe, BlockLayersAccess.Fluid);
            var wanted = reached.TryGetValue((x, z), out var sheet) ? sheet.Fluid : 0;
            var cell = (x, level, z);
            if (Carries(fluid) == (wanted != 0))
            {
                planned.Remove(cell);
                continue;
            }
            if (planned.TryGetValue(cell, out var plan) && plan == wanted) continue;
            planned[cell] = wanted;
            writes.Enqueue(cell, now + (wanted != 0 ? sheet.Rings * Delay(wanted) : Delay(fluid.Id)));
            if (wanted != 0) (added, rings) = (added + 1, Math.Max(rings, sheet.Rings));
            else drained++;
        }
        if (added + drained == 0) return;
        log("chunk {0} {1} level {2}: {3} cells take water, {4} drain, {5} blocks in from the sea", [chunk.X, chunk.Z, level, added, drained, rings]);
    }

    private IReadOnlyList<Sea> Seas(int x, int z)
    {
        if (seasNow.TryGetValue((x / SeaCell, z / SeaCell), out var known)) return known;
        var cell = sea.Config.Map.CellBlocks;
        var now = sea.Map.Sample(x + 0.5, z + 0.5);
        var (height, period) = sea.Model.EverydayWavesIn(Math.Floor((double)x / cell) * cell, Math.Floor((double)z / cell) * cell, cell);
        return seasNow[(x / SeaCell, z / SeaCell)] = [new Sea(now.WaveHeight, now.WavePeriod), new Sea(height, period)];
    }

    private void Write(Func<bool> spare)
    {
        var now = sapi.World.ElapsedMilliseconds;
        var accessor = sapi.World.BlockAccessor;
        var probe = new BlockPos(Dimensions.NormalWorld);
        while (spare() && writes.TryPeek(out var cell, out var due) && due <= now)
        {
            writes.Dequeue();
            if (!planned.Remove(cell, out var fluid)) continue;
            probe.Set(cell.X, cell.Y, cell.Z);
            if (accessor.GetChunkAtBlockPos(probe) == null || !OpenWater.Land(accessor, probe)) continue;
            if (accessor.GetBlock(probe, BlockLayersAccess.Fluid).Id == fluid) continue;
            // Not the bulk accessor: its commit takes a fluid removed under a solid for the solid removed, and lowers the rain heightmap under it.
            accessor.SetBlock(fluid, probe, BlockLayersAccess.Fluid);
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

    private sealed record Flood(Task<Dictionary<(int X, int Z), Reached>> Task, int Version);
}
