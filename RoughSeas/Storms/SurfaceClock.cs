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
    private readonly WaveTime time = new();
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
        capi.Event.RegisterRenderer(this, EnumRenderStage.Before, "roughseas-surface");
    }

    public double RenderOrder => -1;

    public int RenderRange => 0;

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        var calendar = capi.World.Calendar;
        var seconds = time.Advance(deltaTime, capi.IsGamePaused ? 0 : calendar.SpeedOfTime, calendar.TotalHours, calendar.CalendarSpeedMul);
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

        var regionSize = capi.World.BlockAccessor.RegionSize;
        var here = ((int)Math.Floor(pos.X / regionSize), (int)Math.Floor(pos.Z / regionSize));
        var crossed = here != region;
        region = here;
        var slotHours = sea.Config.SeaSampleHours;
        var slot = (long)Math.Floor(time.HoursAt(time.Seconds) / slotHours);
        surface.Raise(sea.Source.Generation(pos.X, pos.Z, slot - 1), sea.Source.Generation(pos.X, pos.Z, slot), time.SecondsAt(slot * slotHours), crossed);
    }

    private static double Degrees(double radians) => (radians * 180 / Math.PI + 360) % 360;
}
