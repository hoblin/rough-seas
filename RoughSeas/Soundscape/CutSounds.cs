using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RoughSeas.Soundscape;

public sealed class CutSounds
{
    private const int CheckEveryMs = 10;

    private readonly ICoreClientAPI capi;
    private readonly SoundscapeModSystem sounds;
    private readonly List<Cutting> playing = [];

    public CutSounds(ICoreClientAPI capi, SoundscapeModSystem sounds)
    {
        (this.capi, this.sounds) = (capi, sounds);
        capi.Event.RegisterGameTickListener(_ => playing.RemoveAll(Played), CheckEveryMs);
    }

    public void Play(SoundCutPacket packet)
    {
        var cut = packet.Cut;
        var sound = capi.World.LoadSound(new SoundParams
        {
            Location = new AssetLocation(cut.Location),
            Position = new Vec3f((float)packet.X, (float)packet.Y, (float)packet.Z),
            Range = cut.Range
        });
        if (sound == null) return;

        playing.Add(new Cutting(sound, cut));
        sounds.Log("{0} at {1:F0} {2:F0} {3:F0}, up to {4:F2} s", cut.Location, packet.X, packet.Y, packet.Z, cut.UntilSeconds);
    }

    private bool Played(Cutting cutting)
    {
        var sound = cutting.Sound;
        if (!cutting.Started)
        {
            if (!sound.IsReady) return false;
            sound.Start();
            cutting.Started = true;
            return false;
        }
        var at = sound.PlaybackPosition;
        if (sound.IsPlaying && at < cutting.Cut.UntilSeconds) return false;

        sound.Stop();
        sounds.Log("{0} stopped at {1:F2} s", cutting.Cut.Location, at);
        return true;
    }

    private sealed class Cutting(ILoadedSound sound, SoundCut cut)
    {
        public ILoadedSound Sound { get; } = sound;
        public SoundCut Cut { get; } = cut;
        public bool Started { get; set; }
    }
}
