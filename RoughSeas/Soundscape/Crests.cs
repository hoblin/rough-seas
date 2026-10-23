using System;
using RoughSeas.SeaAndWeather;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RoughSeas.Soundscape;

public sealed class Crests(ICoreClientAPI capi, CrestsConfig config, SeaClock clock, SoundscapeModSystem sounds)
{
    private readonly SeaClock.Watch watch = clock.StartWatch();

    public void Update(Listener listener)
    {
        var lived = watch.Lap().LivedSeconds;
        if (!listener.AtSea) return;

        Scatter(config.Hiss, listener.Foam.Caps, lived, listener);
        Scatter(config.Plunge, listener.Foam.Spray, lived, listener);
    }

    private void Scatter(ScatteredCue cue, double share, double seconds, Listener listener)
    {
        var rand = capi.World.Rand;
        if (share <= 0 || rand.NextDouble() >= cue.PerSecond * share * seconds) return;

        var volume = (float)Curve.At(cue.Volume, share);
        if (volume <= 0) return;

        var from = Math.Atan2(-listener.Sea.WindZ, -listener.Sea.WindX);
        var bearing = from + (rand.NextDouble() - 0.5) * config.SpreadDegrees * GameMath.DEG2RAD;
        var metres = config.Metres[0] + rand.NextDouble() * (config.Metres[1] - config.Metres[0]);
        var at = listener.Pos.AddCopy(Math.Cos(bearing) * metres, 0, Math.Sin(bearing) * metres);
        capi.World.PlaySoundAt(new AssetLocation(cue.Sound), at.X, at.Y, at.Z, null, true, config.Range, volume);
        sounds.Log("{0} at {1:F0} m bearing {2:F0}°, share {3:F2}, volume {4:F2}", cue.Sound, metres,
            (bearing * GameMath.RAD2DEG + 360) % 360, share, volume);
    }
}
