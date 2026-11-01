using System;
using RoughSeas.SeaAndWeather;

namespace RoughSeas.Storms;

/// <summary>
/// Aims a drawn surface at a point: its field over the point's region and the eight around it, and the waves the
/// point's region raises by the waves' clock, all read from the sea map. A client aims the sea it draws with it
/// (<see cref="SurfaceClock"/>), and the server the sea it judges a body by (<see cref="SwellAround"/>).
/// </summary>
public sealed class SurfaceAim(SeaSurface surface, SurfaceField field, SurfaceConfig config, SeaWeatherConfig sea, SeaClock clock)
{
    private (int X, int Z)? region;

    /// <param name="seconds">The waves' time, in seconds at vanilla's pace.</param>
    /// <param name="foam">Whether to aim the foam too, which is drawn only.</param>
    public void Aim(ISeaSource map, double x, double z, double seconds, int regionSize, bool foam)
    {
        field.Update(map, x, z, regionSize, sea.Map.CellBlocks);
        var state = map.At(field.HighestX, field.HighestZ);
        surface.Aim(field.Height, Math.Atan2(state.WindZ, state.WindX), state.Beaufort, foam ? field.Height / config.FullFoamMetres : 0);

        var here = ((int)Math.Floor(x / regionSize), (int)Math.Floor(z / regionSize));
        var crossed = here != region;
        region = here;
        var slot = (long)Math.Floor(clock.HoursAt(seconds) / sea.SeaSampleHours);
        surface.Raise(map.Generation(x, z, slot - 1), map.Generation(x, z, slot), clock.SecondsAt(slot * sea.SeaSampleHours), crossed);
    }
}
