using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Server;

namespace RoughSeas.SeaAndWeather;

public class SeaModel(SeaWeatherSystem sea, ICoreServerAPI sapi, SeaWeatherModel model, WaterBody water) : ISeaModel, ISeaSource
{
    private const int CacheLimit = 4096;

    private static readonly (double X, double Z)[] RegionSamples = [(0.5, 0.5), (0.25, 0.25), (0.75, 0.25), (0.25, 0.75), (0.75, 0.75)];

    private readonly WindSea windSea = new(sea.Config.Waves);
    private readonly BeaufortScale beaufort = new(sea.Config.Beaufort);
    private readonly ConcurrentDictionary<(int X, int Z), (double Hours, double Mps)> regions = new(Environment.ProcessorCount, 0);

    public bool DangerousOceans => sea.DangerousOceans;

    public double NowHours => sea.NowHours;

    public SeaWeatherModel Weather => model;

    public static IEnumerable<(double X, double Z)> SamplesOf(int regionX, int regionZ, int regionSize) =>
        RegionSamples.Select(sample => ((regionX + sample.X) * regionSize, (regionZ + sample.Z) * regionSize));

    public SeaState At(double x, double z) => At(x, z, NowHours);

    public SeaState At(double x, double z, double totalHours)
    {
        var wind = model.Wind(x, z, totalHours);
        var (height, period) = Waves(x, z, totalHours, wind);
        return new SeaState(wind.X, wind.Z, beaufort.ForceAt(wind.Speed).Force, height, period, wind.Presence, wind.Storm?.Code);
    }

    public SeaSample Sample(double x, double z)
    {
        var hours = NowHours;
        var wind = model.Wind(x, z, hours);
        var (height, period) = Waves(x, z, hours, wind);
        var current = model.Current(x, z);
        return new SeaSample(wind.X, wind.Z, wind.Presence, model.RainCloudness(x, z, hours), height, period, current.X, current.Z, current.WaterCelsius);
    }

    public SeaCurrent CurrentAt(double x, double z) => model.Current(x, z);

    public IEnumerable<Storm> StormsWithin(double x, double z, double range) => model.Storms.Active(x, z, NowHours, range);

    public bool IsSea(double x, double z) => model.IsSea(x, z);

    public WindSample StormsAt(double x, double z, double totalHours) => model.StormsAt(x, z, totalHours);

    public (double X, double Z) Wind(double x, double z)
    {
        var wind = model.Wind(x, z, NowHours);
        return (wind.X, wind.Z);
    }

    public double StormPresence(double x, double z) => model.StormsAt(x, z, NowHours).Presence;

    public Reach Upwind(double x, double z, double towardRadians) => water.Upwind(x, z, towardRadians);

    public string WaterHere(double x, double z) => water.Describe(x, z);

    public float? RainCloudness(double x, double z, double totalDays)
    {
        using var _ = sea.Budget.Measure();
        return (float)model.RainCloudness(x, z, totalDays * sapi.World.Calendar.HoursPerDay);
    }

    /// <summary>
    /// A region's wind for vanilla's region state: the strongest of five samples, so that readers of the region alone
    /// (wind sound, windmills, clouds) feel a squall anywhere in it.
    /// </summary>
    public double RegionMps(int regionX, int regionZ)
    {
        var now = NowHours;
        if (regions.TryGetValue((regionX, regionZ), out var cached) && Math.Abs(now - cached.Hours) < sea.Config.Vanilla.RegionRefreshHours) return cached.Mps;
        if (regions.Count > CacheLimit) regions.Clear();
        var mps = SamplesOf(regionX, regionZ, sapi.WorldManager.RegionSize).Max(at => model.Wind(at.X, at.Z, now).Speed);
        regions[(regionX, regionZ)] = (now, mps);
        return mps;
    }

    private (double Height, double Period) Waves(double x, double z, double hours, WindSample wind)
    {
        if (wind.Speed <= 0) return (0, 0);
        var reach = water.Upwind(x, z, Compass.Radians(wind.X, wind.Z));
        if (reach.FetchMetres <= 0) return (0, 0);
        return model.SustainedWinds(x, z, hours, wind.Speed)
            .Select(sustained => windSea.Grow(sustained.Mps, reach.FetchMetres, reach.MeanDepthMetres, sustained.Hours))
            .MaxBy(sea => sea.Height);
    }
}
