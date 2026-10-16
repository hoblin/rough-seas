using System.Collections.Generic;

namespace RoughSeas.SeaAndWeather;

/// <summary>
/// The sea and weather state, computed from the world seed, position and game time alike on server and client.
/// Reach it with <c>api.ModLoader.GetModSystem&lt;SeaWeatherSystem&gt;()</c>.
/// </summary>
public interface ISeaWeather
{
    /// <summary>The world's "dangerous oceans" setting: whether the sea may harm vessels and swimmers.</summary>
    bool DangerousOceans { get; }

    /// <summary>The state at a block position now. Positions are absolute block coordinates.</summary>
    SeaState At(double x, double z);

    /// <summary>The state at a block position at a game time, in total game hours.</summary>
    SeaState At(double x, double z, double totalHours);

    /// <summary>Storms whose reach overlaps a circle of <paramref name="range"/> blocks around a position, now.</summary>
    IEnumerable<Storm> StormsWithin(double x, double z, double range);
}
