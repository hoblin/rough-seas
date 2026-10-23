using System;
using System.Linq;
using RoughSeas.Storms;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RoughSeas.Soundscape;

public sealed class Splashes
{
    private const double Silent = 0.005;

    private readonly ICoreClientAPI capi;
    private readonly SplashCue cue;
    private readonly float range;
    private readonly StormsModSystem storms;
    private readonly SoundscapeModSystem sounds;
    private readonly SplashPool pool;

    public Splashes(ICoreClientAPI capi, SplashCue cue, float range, StormsModSystem storms, SoundscapeModSystem sounds)
    {
        (this.capi, this.cue, this.range, this.storms, this.sounds) = (capi, cue, range, storms, sounds);
        var pattern = new AssetLocation(cue.Sound);
        var files = string.IsNullOrEmpty(cue.Sound)
            ? []
            : capi.Assets.GetLocations(pattern.Path.TrimEnd('*'), pattern.Domain).OrderBy(file => file.Path, StringComparer.Ordinal).ToList();
        pool = new SplashPool(files, cue.FullFromMetres);
        sounds.Log("{0} splashes in {1}", pool.Count, cue.Sound);
    }

    public double HeightAt(Vec3d at) => storms.DrawnSea?.HeightAt(at.X, at.Z) ?? 0;

    public (double X, double Z) From =>
        storms.Surface is { } surface ? (-Math.Cos(surface.WavesTowardRadians), -Math.Sin(surface.WavesTowardRadians)) : (0, 0);

    public AssetLocation? Play(Vec3d at, double waveHeight, string what, AssetLocation? previous)
    {
        var volume = cue.VolumeAt(waveHeight);
        if (volume < Silent || pool.Pick(waveHeight, capi.World.Rand, previous) is not { } file) return previous;

        capi.World.PlaySoundAt(file, at.X, at.Y, at.Z, null, true, range, (float)volume);
        sounds.Log("{0}: {1} of a {2:F2} m wave at volume {3:F2}", what, file.Path, waveHeight, volume);
        return file;
    }
}
