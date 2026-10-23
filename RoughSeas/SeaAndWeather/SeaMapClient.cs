using System;
using System.Collections.Concurrent;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace RoughSeas.SeaAndWeather;

public class SeaMapClient(SeaWeatherSystem sea, ICoreClientAPI capi) : ISeaSource
{
    private const double VanillaSpeedOfTime = 60;

    private readonly ConcurrentDictionary<(int X, int Z), Region> regions = new(Environment.ProcessorCount, 0);
    private readonly BeaufortScale beaufort = new(sea.Config.Beaufort);

    public void Start()
    {
        capi.Network.GetChannel(SeaWeatherSystem.Channel).SetMessageHandler<SeaMapSnapshot>(Receive);
        capi.Event.MapRegionLoaded += (_, region) => Load(region);
        capi.Event.MapRegionUnloaded += (coord, _) => regions.TryRemove((coord.X, coord.Y), out Region? _);
    }

    public SeaSample Sample(double x, double z)
    {
        var cellBlocks = sea.Config.Map.CellBlocks;
        var cellX = x / cellBlocks - 0.5;
        var cellZ = z / cellBlocks - 0.5;
        var leftX = (int)Math.Floor(cellX);
        var topZ = (int)Math.Floor(cellZ);
        var (tx, tz) = (cellX - leftX, cellZ - topZ);

        var sum = new double[9];
        var weight = 0.0;
        for (var dx = 0; dx <= 1; dx++)
        for (var dz = 0; dz <= 1; dz++)
        {
            if (Cell(leftX + dx, topZ + dz) is not { } cell) continue;
            var w = (dx == 0 ? 1 - tx : tx) * (dz == 0 ? 1 - tz : tz);
            Accumulate(sum, cell, w);
            weight += w;
        }
        if (weight <= 0) return default;
        return new SeaSample(sum[0] / weight, sum[1] / weight, sum[2] / weight, sum[3] / weight, sum[4] / weight,
            sum[5] / weight, sum[6] / weight, sum[7] / weight, sum[8] / weight);
    }

    public (double X, double Z) Wind(double x, double z)
    {
        var sample = Sample(x, z);
        return (sample.WindX, sample.WindZ);
    }

    public double StormPresence(double x, double z) => Sample(x, z).StormPresence;

    public SeaState At(double x, double z)
    {
        var sample = Sample(x, z);
        return new SeaState(sample.WindX, sample.WindZ, beaufort.ForceAt(sample.WindSpeed).Force, sample.WaveHeight, sample.WavePeriod,
            sample.StormPresence, null);
    }

    public SeaCurrent CurrentAt(double x, double z)
    {
        var sample = Sample(x, z);
        return new SeaCurrent(sample.CurrentX, sample.CurrentZ, sample.WaterCelsius, null);
    }

    public float? RainCloudness(double x, double z, double totalDays) => Covers(x, z) ? (float)Sample(x, z).RainCloudness : null;

    public double RegionMps(int regionX, int regionZ) => Fresh((regionX, regionZ))?.Mps ?? 0;

    public WaveGeneration Generation(double x, double z, long slot)
    {
        var regionSize = capi.World.BlockAccessor.RegionSize;
        return Fresh(((int)Math.Floor(x / regionSize), (int)Math.Floor(z / regionSize)))?.Generation(slot) ?? default;
    }

    public bool Covers(double x, double z)
    {
        var regionSize = capi.World.BlockAccessor.RegionSize;
        return Fresh(((int)Math.Floor(x / regionSize), (int)Math.Floor(z / regionSize))) != null;
    }

    private void Receive(SeaMapSnapshot snapshot)
    {
        if (snapshot.Version != SeaMapSnapshot.CurrentVersion) return;
        var key = (snapshot.RegionX, snapshot.RegionZ);
        var samples = snapshot.Samples();
        var generations = snapshot.Generations();
        regions.AddOrUpdate(key,
            _ => new Region(samples, samples, snapshot.TotalHours, 0, snapshot.CellsPerSide, snapshot.RegionMps, snapshot.FirstWaveSlot, generations),
            (_, old) => snapshot.TotalHours < old.SnapshotHours
                ? old
                : new Region(old.Side == snapshot.CellsPerSide ? old.To : samples, samples, snapshot.TotalHours,
                    Span(snapshot.TotalHours - old.SnapshotHours), snapshot.CellsPerSide, snapshot.RegionMps, snapshot.FirstWaveSlot, generations));
        foreach (var stale in regions.Keys) Fresh(stale);
    }

    private double Now => capi.World.Calendar?.TotalHours ?? 0;

    private double Span(double hours) => hours <= sea.Config.Map.StaleHours && capi.World.Calendar is { } calendar
        ? sea.Config.Map.UpdateSeconds * VanillaSpeedOfTime * calendar.CalendarSpeedMul / 3600
        : 0;

    private Region? Fresh((int X, int Z) key)
    {
        if (!regions.TryGetValue(key, out var region)) return null;
        if (Now - region.SnapshotHours <= sea.Config.Map.StaleHours) return region;
        regions.TryRemove(key, out _);
        return null;
    }

    private void Load(IMapRegion region)
    {
        var data = region.GetModdata(SeaMapSnapshot.ModdataKey);
        if (data == null) return;
        try
        {
            Receive(SerializerUtil.Deserialize<SeaMapSnapshot>(data));
        }
        catch (Exception exception)
        {
            sea.Log("unreadable sea map in a map region: {0}", exception.Message);
        }
    }

    private SeaSample? Cell(int cellX, int cellZ)
    {
        var side = capi.World.BlockAccessor.RegionSize / sea.Config.Map.CellBlocks;
        var regionX = (int)Math.Floor((double)cellX / side);
        var regionZ = (int)Math.Floor((double)cellZ / side);
        if (Fresh((regionX, regionZ)) is not { } region || region.Side != side) return null;
        return region.At(cellX - regionX * side, cellZ - regionZ * side, Now);
    }

    private static void Accumulate(double[] sum, SeaSample cell, double w)
    {
        sum[0] += cell.WindX * w;
        sum[1] += cell.WindZ * w;
        sum[2] += cell.StormPresence * w;
        sum[3] += cell.RainCloudness * w;
        sum[4] += cell.WaveHeight * w;
        sum[5] += cell.WavePeriod * w;
        sum[6] += cell.CurrentX * w;
        sum[7] += cell.CurrentZ * w;
        sum[8] += cell.WaterCelsius * w;
    }

    private sealed record Region(SeaSample[] From, SeaSample[] To, double SnapshotHours, double SpanHours, int Side, double Mps,
        long FirstWaveSlot, WaveGeneration[] Generations)
    {
        public SeaSample At(int cellX, int cellZ, double now)
        {
            var index = cellZ * Side + cellX;
            var t = SpanHours > 0 ? GameMath.Clamp((now - SnapshotHours) / SpanHours, 0, 1) : 1;
            return Lerp(From[index], To[index], t);
        }

        public WaveGeneration Generation(long slot) =>
            slot >= FirstWaveSlot && slot - FirstWaveSlot < Generations.Length ? Generations[slot - FirstWaveSlot] : default;

        private static SeaSample Lerp(SeaSample a, SeaSample b, double t) => new(
            a.WindX + (b.WindX - a.WindX) * t,
            a.WindZ + (b.WindZ - a.WindZ) * t,
            a.StormPresence + (b.StormPresence - a.StormPresence) * t,
            a.RainCloudness + (b.RainCloudness - a.RainCloudness) * t,
            a.WaveHeight + (b.WaveHeight - a.WaveHeight) * t,
            a.WavePeriod + (b.WavePeriod - a.WavePeriod) * t,
            a.CurrentX + (b.CurrentX - a.CurrentX) * t,
            a.CurrentZ + (b.CurrentZ - a.CurrentZ) * t,
            a.WaterCelsius + (b.WaterCelsius - a.WaterCelsius) * t);
    }
}
