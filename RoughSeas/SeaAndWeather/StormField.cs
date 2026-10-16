using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.MathTools;

namespace RoughSeas.SeaAndWeather;

public class StormField
{
    private const int TileSize = 512;
    private const int CacheLimit = 20000;

    private readonly long seed;
    private readonly SeaWeatherConfig config;
    private readonly WorldClimate climate;
    private readonly PrevailingWinds prevailing;
    private readonly double hoursPerYear;
    private readonly ConcurrentDictionary<(int Type, int CellX, int CellZ, long Slot), Storm?> born = new();
    private readonly ConcurrentDictionary<(int TileX, int TileZ, long Hour), Storm[]> nearTile = new();

    public StormField(long seed, SeaWeatherConfig config, WorldClimate climate, PrevailingWinds prevailing, double hoursPerYear)
    {
        this.seed = seed;
        this.config = config;
        this.climate = climate;
        this.prevailing = prevailing;
        this.hoursPerYear = hoursPerYear;
    }

    public Storm[] Near(double x, double z, double hours)
    {
        var key = ((int)Math.Floor(x / TileSize), (int)Math.Floor(z / TileSize), (long)Math.Floor(hours));
        if (nearTile.TryGetValue(key, out var storms)) return storms;
        if (nearTile.Count > CacheLimit) nearTile.Clear();
        return nearTile.GetOrAdd(key, static (tile, field) => field.StormsReaching(tile.TileX, tile.TileZ, tile.Hour), this);
    }

    public IEnumerable<Storm> Active(double x, double z, double hours, double range) =>
        Candidates(x, z, hours, hours, range)
            .Where(storm => storm.IsActive(hours))
            .Where(storm => Distance(storm.CentreAt(hours), x, z) <= range + storm.Radius);

    private Storm[] StormsReaching(int tileX, int tileZ, long hour)
    {
        var centreX = (tileX + 0.5) * TileSize;
        var centreZ = (tileZ + 0.5) * TileSize;
        var tileReach = TileSize * Math.Sqrt(0.5);
        return Candidates(centreX, centreZ, hour, hour + 1, tileReach)
            .Where(storm => storm.BornHours <= hour + 1 && storm.DiesHours >= hour)
            .Where(storm =>
            {
                var midHour = Math.Clamp(hour + 0.5, storm.BornHours, storm.DiesHours);
                var travelPerHour = Math.Sqrt(storm.VelocityX * storm.VelocityX + storm.VelocityZ * storm.VelocityZ);
                return Distance(storm.CentreAt(midHour), centreX, centreZ) <= storm.Radius + tileReach + travelPerHour;
            })
            .ToArray();
    }

    private IEnumerable<Storm> Candidates(double x, double z, double fromHours, double toHours, double range)
    {
        if (born.Count > CacheLimit) born.Clear();
        for (var type = 0; type < config.Storms.Length; type++)
        {
            var stormType = config.Storms[type];
            var reach = stormType.LifetimeHours.Max * stormType.SpeedBlocksPerHour.Max + stormType.Radius.Max + range;
            var firstSlot = (long)Math.Floor((fromHours - stormType.LifetimeHours.Max) / stormType.SlotHours);
            var lastSlot = (long)Math.Floor(toHours / stormType.SlotHours);
            for (var cellX = Cell(x - reach, stormType); cellX <= Cell(x + reach, stormType); cellX++)
            for (var cellZ = Cell(z - reach, stormType); cellZ <= Cell(z + reach, stormType); cellZ++)
            for (var slot = firstSlot; slot <= lastSlot; slot++)
            {
                var storm = born.GetOrAdd((type, cellX, cellZ, slot), static (key, field) => field.Brew(key.Type, key.CellX, key.CellZ, key.Slot), this);
                if (storm != null) yield return storm;
            }
        }
    }

    private Storm? Brew(int type, int cellX, int cellZ, long slot)
    {
        var stormType = config.Storms[type];
        var rand = new LCGRandom(seed + 7919L * (type + 1));
        rand.InitPositionSeed(cellX, (int)slot, cellZ);
        if (rand.NextDouble() >= stormType.Chance) return null;

        var originX = (cellX + rand.NextDouble()) * stormType.CellSize;
        var originZ = (cellZ + rand.NextDouble()) * stormType.CellSize;
        var bornHours = (slot + rand.NextDouble()) * stormType.SlotHours;
        var northern = prevailing.LatitudeDegrees(originZ) >= 0;
        if (rand.NextDouble() >= Likelihood(stormType, originX, originZ, bornHours, northern)) return null;

        var track = prevailing.TowardRadians(originZ) + (rand.NextDouble() * 2 - 1) * config.Storm.TrackSpreadDegrees * GameMath.DEG2RAD_DOUBLE;
        var (towardX, towardZ) = PrevailingWinds.Toward(track);
        var speed = stormType.SpeedBlocksPerHour.Lerp(rand.NextDouble());
        return new Storm
        {
            Type = stormType,
            Shape = config.Storm,
            BornHours = bornHours,
            LifetimeHours = stormType.LifetimeHours.Lerp(rand.NextDouble()),
            OriginX = originX,
            OriginZ = originZ,
            VelocityX = towardX * speed,
            VelocityZ = towardZ * speed,
            Radius = stormType.Radius.Lerp(rand.NextDouble()),
            PeakMps = stormType.PeakMps.Lerp(rand.NextDouble()),
            Northern = northern
        };
    }

    private double Likelihood(StormTypeConfig stormType, double x, double z, double hours, bool northern)
    {
        var (celsius, rainfall) = climate.At(x, z);
        var yearFraction = (hours / hoursPerYear % 1 + 1) % 1;
        var month = ((int)(yearFraction * 12) + (northern ? 0 : 6)) % 12;
        return stormType.Temperature.Weight(celsius)
            * Math.Pow(rainfall, stormType.RainfallExponent)
            * stormType.MonthlyWeight[month];
    }

    private static int Cell(double coordinate, StormTypeConfig stormType) => (int)Math.Floor(coordinate / stormType.CellSize);

    private static double Distance((double X, double Z) centre, double x, double z) =>
        Math.Sqrt((centre.X - x) * (centre.X - x) + (centre.Z - z) * (centre.Z - z));
}
