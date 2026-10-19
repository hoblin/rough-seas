using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RoughSeas.Soundscape;

public sealed class SeaBed(ICoreClientAPI capi, BedsConfig config) : IDisposable
{
    private const double Silent = 0.005;

    private readonly ILoadedSound?[] loops = new ILoadedSound?[config.Loops.Length];
    private readonly double[] volumes = new double[config.Loops.Length];

    public double[] Volumes => volumes;

    public void Load()
    {
        for (var i = 0; i < loops.Length; i++)
        {
            loops[i] = capi.World.LoadSound(new SoundParams
            {
                Location = new AssetLocation(config.Loops[i].Sound).WithPathAppendixOnce(".ogg"),
                ShouldLoop = true,
                RelativePosition = true,
                Position = new Vec3f(),
                DisposeOnFinish = false,
                SoundType = EnumSoundType.Ambient,
                Volume = 0
            });
        }
    }

    public void Update(double seconds, Listener listener)
    {
        var targets = listener.AtSea ? Curve.Mix(config, listener.WaveHeight) : new double[loops.Length];
        var ease = Math.Min(1, seconds / config.EaseSeconds);
        for (var i = 0; i < loops.Length; i++)
        {
            volumes[i] += (targets[i] - volumes[i]) * ease;
            if (loops[i] is not { IsReady: true } loop) continue;

            if (volumes[i] < Silent)
            {
                if (loop.IsPlaying) loop.Stop();
                continue;
            }
            if (!loop.IsPlaying)
            {
                loop.Start();
                loop.PlaybackPosition = loop.SoundLengthSeconds * (float)capi.World.Rand.NextDouble();
            }
            loop.SetVolume((float)volumes[i]);
        }
    }

    public string Describe() => string.Join(", ", config.Loops.Select((loop, i) => $"{loop.Sound} {volumes[i]:F2}"));

    public void Dispose()
    {
        foreach (var loop in loops) loop?.Dispose();
    }
}
