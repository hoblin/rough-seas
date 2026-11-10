using System;
using RoughSeas.SeaAndWeather;
using RoughSeas.Water;
using Vintagestory.API.Client;

namespace RoughSeas.Storms;

public sealed class SurfaceClock : IRenderer
{
    private const double LogEverySeconds = 10;

    private readonly StormsModSystem storms;
    private readonly SeaWeatherSystem sea;
    private readonly SeaSurface surface;
    private readonly ICoreClientAPI capi;
    private readonly WaveTime time;
    private readonly SurfaceAim aim;
    private readonly SurfMaker shore;
    private double sinceAim = double.PositiveInfinity;
    private double sinceLog = double.PositiveInfinity;

    public SurfaceClock(StormsModSystem storms, SeaWeatherSystem sea, SeaSurface surface, SurfaceField field, SurfMaker shore, ICoreClientAPI capi)
    {
        this.storms = storms;
        this.shore = shore;
        this.sea = sea;
        this.surface = surface;
        this.capi = capi;
        time = new WaveTime(sea.Clock);
        aim = new SurfaceAim(surface, field, storms.Config.Surface, sea.Config, sea.Clock);
        capi.Event.RegisterRenderer(this, EnumRenderStage.Before, "roughseas-surface");
    }

    public double RenderOrder => -1;

    public int RenderRange => 0;

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        var seconds = time.Advance(deltaTime, capi.IsGamePaused ? 0 : capi.World.Calendar.SpeedOfTime);
        if ((sinceAim += deltaTime) >= storms.Config.Surface.AimSeconds) Aim(seconds);
        surface.Advance(seconds);
        if ((sinceLog += deltaTime) < LogEverySeconds) return;
        sinceLog = 0;
        var foam = surface.Foam;
        var trains = surface.Trains;
        storms.Log("surface: {0:F1} s, drawn {1:F1} m against the sea's {2:F1} m, waves toward {3:F0}°, steepness {4:F2}; whitecaps {5:F2}, streaks {6:F2}, spray {7:F2}",
            seconds, trains.DrawnHeight, trains.SeaHeight, Degrees(trains.TowardRadians), trains.Steepness, foam.Caps, foam.Streaks, foam.Spray);
    }

    public void Dispose() => capi.Event.UnregisterRenderer(this, EnumRenderStage.Before);

    private void Aim(double seconds)
    {
        sinceAim = 0;
        var pos = capi.World.Player?.Entity?.Pos;
        if (pos == null) return;
        aim.Aim(sea.Map, pos.X, pos.Z, seconds, capi.World.BlockAccessor.RegionSize, foam: true);
        SurfaceShader.Upload();
        if (shore.Update(capi.World, sea.Map, pos.X, pos.Z, surface.Trains.Vectors(), surface.Trains.AmplitudeShares(), surface.Trains.Dominant, capi.ElapsedMilliseconds / 1000.0) is not { } field) return;
        surface.Seabed = field;
        SurfaceShader.Upload(field);
    }

    private static double Degrees(double radians) => (radians * 180 / Math.PI + 360) % 360;
}
