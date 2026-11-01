using System;
using RoughSeas.SeaAndWeather;
using Vintagestory.API.Client;

namespace RoughSeas.Storms;

public sealed class SurfaceClock : IRenderer
{
    private const double LogEverySeconds = 10;

    private readonly StormsModSystem storms;
    private readonly SeaWeatherSystem sea;
    private readonly SeaSurface surface;
    private readonly SurfaceField field;
    private readonly ICoreClientAPI capi;
    private readonly WaveTime time;
    private double sinceAim = double.PositiveInfinity;
    private double sinceLog = double.PositiveInfinity;
    private (int X, int Z)? region;

    public SurfaceClock(StormsModSystem storms, SeaWeatherSystem sea, SeaSurface surface, SurfaceField field, ICoreClientAPI capi)
    {
        this.storms = storms;
        this.sea = sea;
        this.surface = surface;
        this.field = field;
        this.capi = capi;
        time = new WaveTime(sea.Clock);
        capi.Event.RegisterRenderer(this, EnumRenderStage.Before, "roughseas-surface");
    }

    public double RenderOrder => -1;

    public int RenderRange => 0;

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        var seconds = time.Advance(deltaTime, capi.IsGamePaused ? 0 : capi.World.Calendar.SpeedOfTime);
        if ((sinceAim += deltaTime) >= storms.Config.Surface.AimSeconds) Aim();
        surface.Advance(seconds);
        if ((sinceLog += deltaTime) < LogEverySeconds) return;
        sinceLog = 0;
        var foam = surface.Foam;
        storms.Log("surface: {0:F1} s, drawn {1:F1} m against the sea's {2:F1} m, waves toward {3:F0}°, wind toward {4:F0}°, steepness {5:F2}; whitecaps {6:F2}, streaks {7:F2}, spray {8:F2}",
            seconds, surface.DrawnHeight, surface.SeaHeight, Degrees(surface.WavesTowardRadians), Degrees(surface.TowardRadians), surface.Steepness, foam.Caps, foam.Streaks, foam.Spray);
    }

    public void Dispose() => capi.Event.UnregisterRenderer(this, EnumRenderStage.Before);

    private void Aim()
    {
        sinceAim = 0;
        var pos = capi.World.Player?.Entity?.Pos;
        if (pos == null) return;
        field.Update(sea.Source, pos.X, pos.Z, capi.World.BlockAccessor.RegionSize, sea.Config.Map.CellBlocks);
        SurfaceShader.Upload();
        var state = sea.At(field.HighestX, field.HighestZ);
        surface.Aim(field.Height, Math.Atan2(state.WindZ, state.WindX), state.Beaufort, field.Height / storms.Config.Surface.FullFoamMetres);

        region = Raise(surface, sea, pos.X, pos.Z, time.Seconds, capi.World.BlockAccessor.RegionSize, region);
    }

    /// <summary>Raises the trains of the region a position is in at this time of the waves; returns that region.</summary>
    /// <param name="region">The region the surface's trains were last raised for, if any.</param>
    public static (int X, int Z) Raise(SeaSurface surface, SeaWeatherSystem sea, double x, double z, double seconds, int regionSize,
        (int X, int Z)? region)
    {
        var here = ((int)Math.Floor(x / regionSize), (int)Math.Floor(z / regionSize));
        var slotHours = sea.Config.SeaSampleHours;
        var slot = (long)Math.Floor(sea.Clock.HoursAt(seconds) / slotHours);
        surface.Raise(sea.Source.Generation(x, z, slot - 1), sea.Source.Generation(x, z, slot), sea.Clock.SecondsAt(slot * slotHours), here != region);
        return here;
    }

    private static double Degrees(double radians) => (radians * 180 / Math.PI + 360) % 360;
}
