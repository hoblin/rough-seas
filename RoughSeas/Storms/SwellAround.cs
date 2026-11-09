using RoughSeas.SeaAndWeather;
using RoughSeas.Surf;
using Vintagestory.API.Common;

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
    private readonly SurfMaker? shore;
    private readonly IWorldAccessor? world;

    public SwellAround(SurfaceConfig config, Waves waves, SeaWeatherConfig sea, SeaClock clock, SurfMaker? shore = null, IWorldAccessor? world = null)
    {
        (this.shore, this.world) = (shore, world);
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
        if (world != null && shore?.Update(world, map, x, z, surface.Vectors(), world.ElapsedMilliseconds / 1000.0) is { } field) surface.Shore = field;
    }

    /// <summary>Brings the sea to <paramref name="seconds"/> without aiming it again, between aims.</summary>
    public void Advance(double seconds) => surface.Advance(seconds);
}
