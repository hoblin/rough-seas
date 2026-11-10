using RoughSeas.SeaAndWeather;
using RoughSeas.Water;

namespace RoughSeas.Storms;

/// <summary>
/// The drawn sea around one body, built on the server as the body's client draws it: the same surface and field, aimed
/// the same way (<see cref="SurfaceAim"/>) from the sea map the server sent and run on the world's clock. The two sides
/// may differ for a few seconds in two cases: the handover between regions, which each side eases from what it drew before, and a
/// region the client takes for the first time (<see cref="SeaMap"/>).
/// </summary>
public sealed class SwellAround
{
    private readonly SeaSurface surface;
    private readonly SurfaceAim aim;
    private readonly Seabeds? seabeds;

    public SwellAround(SurfaceConfig config, Waves waves, SeaWeatherConfig sea, SeaClock clock, Seabeds? seabeds = null)
    {
        this.seabeds = seabeds;
        surface = new SeaSurface(config, waves);
        var field = new SurfaceField();
        aim = new SurfaceAim(surface, field, config, sea, clock);
        Sea = new DrawnSea(surface, field);
        Water = new DrawnWater(Sea);
    }

    public IDrawnSea Sea { get; }

    public DrawnWater Water { get; }

    /// <summary>Aims the sea at a body and brings it to <paramref name="seconds"/>, the waves' time.</summary>
    public void At(ISeaSource map, double x, double z, double seconds, int regionSize)
    {
        aim.Aim(map, x, z, seconds, regionSize, foam: false);
        surface.Advance(seconds);
        Over(map, x, z);
    }

    /// <summary>Lays the seabed under a body: one sea serves several bodies, each read over its own bed.</summary>
    public void Over(ISeaSource map, double x, double z)
    {
        if (seabeds != null) surface.Seabed = seabeds.At(map, x, z, surface.Trains.Vectors(), surface.Trains.AmplitudeShares(), surface.Trains.Dominant);
    }

    /// <summary>Brings the sea to <paramref name="seconds"/> without aiming it again, between aims.</summary>
    public void Advance(double seconds) => surface.Advance(seconds);
}
