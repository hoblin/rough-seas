using System;
using RoughSeas.SeaAndWeather;

namespace RoughSeas.Storms;

/// <summary>
/// The drawn sea around one vessel on the server: the trains her region raises, as high as the sea where she floats,
/// on the waves' clock. A client near her draws the same waves, which it scales by its field to the same height there,
/// so a vessel nobody steers is carried by the waves the players see.
/// </summary>
public sealed class LocalSea
{
    private readonly SeaSurface surface;
    private readonly SeaWeatherSystem sea;
    private readonly double aimSeconds;
    private readonly WaveTime time;
    private double sinceAim = double.PositiveInfinity;
    private (int X, int Z)? region;

    public LocalSea(StormsModSystem storms, SeaWeatherSystem sea)
    {
        this.sea = sea;
        surface = new SeaSurface(storms.Config.Surface, storms.Waves);
        aimSeconds = storms.Config.Surface.AimSeconds;
        time = new WaveTime(sea.Clock);
        Drawn = new DrawnSea(surface, (_, _) => 1);
    }

    public IDrawnSea Drawn { get; }

    /// <summary>Runs the waves on by a tick, aimed at the vessel's position.</summary>
    public void Advance(double x, double z, double deltaSeconds, double speedOfTime, int regionSize)
    {
        var seconds = time.Advance(deltaSeconds, speedOfTime);
        if ((sinceAim += deltaSeconds) >= aimSeconds)
        {
            sinceAim = 0;
            var state = sea.At(x, z);
            // no foam: nothing draws it here
            surface.Aim(state.WaveHeight, Math.Atan2(state.WindZ, state.WindX), state.Beaufort, 0);
            region = SurfaceClock.Raise(surface, sea, x, z, seconds, regionSize, region);
        }
        surface.Advance(seconds);
    }
}
