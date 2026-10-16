using System.Collections.Generic;

namespace RoughSeas.SeaAndWeather;

/// <summary>
/// The sea and weather state: wind, storms and currents from the world seed, worldgen climate, position and game time,
/// alike on server and client; waves also from the ocean map each side has loaded.
/// Reach it with <c>api.ModLoader.GetModSystem&lt;SeaWeatherSystem&gt;()</c>.
/// </summary>
public interface ISeaWeather
{
    /// <summary>The world's "dangerous oceans" setting: whether the sea may harm vessels and swimmers.</summary>
    bool DangerousOceans { get; }

    /// <summary>The game time the state is read at, in total game hours.</summary>
    double NowHours { get; }

    /// <summary>The state at a block position now. Positions are absolute block coordinates.</summary>
    SeaState At(double x, double z);

    /// <summary>The state at a block position at a game time, in total game hours.</summary>
    SeaState At(double x, double z, double totalHours);

    /// <summary>The ocean current and the water's temperature at a block position: steady, from seed and position alone.</summary>
    SeaCurrent CurrentAt(double x, double z);

    /// <summary>Storms whose reach overlaps a circle of <paramref name="range"/> blocks around a position, now.</summary>
    IEnumerable<Storm> StormsWithin(double x, double z, double range);

    /// <summary>Whether worldgen puts open sea at a position, loaded or not; coarse, a few dozen blocks to a cell.</summary>
    bool IsSea(double x, double z);

    /// <summary>The storms alone at a position and game time: their wind, combined presence, rain and the strongest of them.</summary>
    WindSample StormsAt(double x, double z, double totalHours);
}
