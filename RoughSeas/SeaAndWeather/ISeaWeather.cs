using System.Collections.Generic;

namespace RoughSeas.SeaAndWeather;

/// <summary>
/// The sea and weather state, on either side: the server computes it, and a client reads the map the server sends it.
/// Reach it with <c>api.ModLoader.GetModSystem&lt;SeaWeatherSystem&gt;()</c>.
/// </summary>
public interface ISeaWeather
{
    /// <summary>The world's "dangerous oceans" setting: whether the sea may do harm.</summary>
    bool DangerousOceans { get; }

    /// <summary>The game time the state is read at, in total game hours.</summary>
    double NowHours { get; }

    /// <summary>The state at a block position now. Positions are absolute block coordinates.</summary>
    SeaState At(double x, double z);

    /// <summary>The ocean current and the water's temperature at a block position.</summary>
    SeaCurrent CurrentAt(double x, double z);
}

/// <summary>The model itself, on the server alone: <c>SeaWeatherSystem.Model</c>.</summary>
public interface ISeaModel : ISeaWeather
{
    /// <summary>The state at a block position at a game time, in total game hours.</summary>
    SeaState At(double x, double z, double totalHours);

    /// <summary>Storms whose reach overlaps a circle of <paramref name="range"/> blocks around a position, now.</summary>
    IEnumerable<Storm> StormsWithin(double x, double z, double range);

    /// <summary>Whether the world's ocean map puts open sea at a position, built or not; coarse, a few dozen blocks to a cell.</summary>
    bool IsSea(double x, double z);

    /// <summary>The storms alone at a position and game time: their wind, combined presence, rain and the strongest of them.</summary>
    WindSample StormsAt(double x, double z, double totalHours);
}

/// <summary>What vanilla's channels and our renderers read, on either side: the model on the server, the received map on a client.</summary>
public interface ISeaSource
{
    SeaSample Sample(double x, double z);

    (double X, double Z) Wind(double x, double z);

    double StormPresence(double x, double z);

    SeaState At(double x, double z);

    SeaCurrent CurrentAt(double x, double z);

    float? RainCloudness(double x, double z, double totalDays);

    double RegionMps(int regionX, int regionZ);
}
