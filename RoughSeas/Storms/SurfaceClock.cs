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
    private readonly ICoreClientAPI capi;
    private double aimedAt = double.NegativeInfinity;
    private double loggedAt = double.NegativeInfinity;
    private double seaHeight;
    private double elapsed;

    public SurfaceClock(StormsModSystem storms, SeaWeatherSystem sea, SeaSurface surface, ICoreClientAPI capi)
    {
        this.storms = storms;
        this.sea = sea;
        this.surface = surface;
        this.capi = capi;
        capi.Event.RegisterRenderer(this, EnumRenderStage.Before, "roughseas-surface");
    }

    public double RenderOrder => -1;

    public int RenderRange => 0;

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        var seconds = elapsed += deltaTime;
        if (seconds - aimedAt >= storms.Config.Surface.AimSeconds) Aim(seconds);
        surface.Advance(seconds);
        if (seconds - loggedAt < LogEverySeconds) return;
        loggedAt = seconds;
        var foam = surface.Foam;
        storms.Log("surface: drawn {0:F1} m against the sea's {1:F1} m, toward {2:F0}°, steepness {3:F2}; whitecaps {4:F2}, streaks {5:F2}, spray {6:F2}",
            surface.DrawnHeight, seaHeight, (surface.TowardRadians * 180 / Math.PI + 360) % 360, surface.Steepness, foam.Caps, foam.Streaks, foam.Spray);
    }

    public void Dispose() => capi.Event.UnregisterRenderer(this, EnumRenderStage.Before);

    private void Aim(double seconds)
    {
        aimedAt = seconds;
        var pos = capi.World.Player?.Entity?.Pos;
        if (pos == null) return;
        var state = sea.At(pos.X, pos.Z);
        seaHeight = state.Oceanity > 0 ? state.WaveHeight / state.Oceanity : 0;
        surface.Aim(seaHeight, Math.Atan2(state.WindZ, state.WindX), state.Beaufort);
    }
}
