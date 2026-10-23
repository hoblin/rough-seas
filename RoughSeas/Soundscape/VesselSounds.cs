using System;
using System.Collections.Generic;
using System.Linq;
using RoughSeas.Storms;
using RoughSeas.Vessels;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace RoughSeas.Soundscape;

public sealed class VesselSounds(ICoreClientAPI capi, WaterConfig config, Splashes splashes, SoundscapeModSystem sounds) : IDisposable
{
    private const float Silent = 0.005f;

    private readonly Dictionary<long, Voices> heard = new();

    public void Update(Listener listener)
    {
        var near = capi.World.GetEntitiesAround(listener.Pos, (float)config.ListenMetres, (float)config.ListenMetres,
            entity => entity.HasBehavior<EntityBehaviorVesselIntegrity>() || entity.HasBehavior<EntityBehaviorBilge>());
        var ids = near.Select(entity => entity.EntityId).ToHashSet();
        foreach (var gone in heard.Keys.Where(id => !ids.Contains(id)).ToArray())
        {
            heard[gone].Dispose();
            heard.Remove(gone);
        }
        foreach (var vessel in near)
        {
            if (!heard.TryGetValue(vessel.EntityId, out var voices)) heard[vessel.EntityId] = voices = new Voices(capi, config, vessel, sounds);
            voices.Update(vessel, splashes);
        }
    }

    public string Describe() => heard.Count == 0
        ? "no vessel within hearing"
        : string.Join("\n", heard.Values.Select(voices => voices.Describe()));

    public void Dispose()
    {
        foreach (var voices in heard.Values) voices.Dispose();
        heard.Clear();
    }

    private sealed class Voices(ICoreClientAPI capi, WaterConfig config, Entity vessel, SoundscapeModSystem sounds) : IDisposable
    {
        private readonly Loop slosh = new(capi, config.Slosh, config.Range, (state, volume) => sounds.Log(vessel, "bilge slosh {0} at {1:F2}", state, volume));
        private readonly Dictionary<HullPlace, Loop> leaks = HullShape.Places.ToDictionary(place => place,
            place => new Loop(capi, config.Leak, config.Range, (state, volume) => sounds.Log(vessel, "leak trickle at the {0} {1} at {2:F2}", place.Code(), state, volume)));
        private readonly CrestWatch crests = new();

        private string reading = "";
        private double lastWave;

        public void Update(Entity vessel, Splashes splashes)
        {
            var hull = vessel.GetBehavior<EntityBehaviorVesselIntegrity>();
            var bilge = vessel.GetBehavior<EntityBehaviorBilge>();
            var afloat = vessel.Alive && vessel.Swimming && hull?.Afloat.Sunk != true;
            var centre = vessel.Pos.XYZ;
            var fill = afloat ? bilge?.Fill ?? 0 : 0;
            var room = bilge is { CapacityLitres: > 0 } ? 1 - bilge.Fill : 1;

            slosh.Play(fill, centre);
            if (!afloat) crests.Forget();
            else if (crests.Watch(splashes.HeightAt(centre)) is { } wave)
            {
                lastWave = wave;
                var (fromX, fromZ) = splashes.From;
                splashes.Play(hull == null ? centre : centre.AddCopy(hull.PointAt(hull.Toward(fromX, fromZ))), wave, $"{vessel.Code} {vessel.EntityId} splash");
            }
            var leaking = 0.0;
            foreach (var (place, loop) in leaks)
            {
                var litresPerSecond = afloat && hull != null ? hull.Model.LitresPerSecond(place, hull.Afloat, hull.DepthMetres) * room : 0;
                leaking += litresPerSecond;
                loop.Play(litresPerSecond, hull == null ? centre : centre.AddCopy(hull.PointAt(place)));
            }
            reading = $"{vessel.Code} {vessel.EntityId}: {(afloat ? "afloat" : "not afloat")}, bilge {fill:F2} full ({slosh}), " +
                $"leaking {leaking:F2} L/s heard ({leaks.Values.Count(loop => loop.Playing)} cracks), last splash of a {lastWave:F2} m wave";
        }

        public string Describe() => reading;

        public void Dispose()
        {
            slosh.Dispose();
            foreach (var loop in leaks.Values) loop.Dispose();
        }
    }

    private sealed class Loop(ICoreClientAPI capi, Cue cue, float range, Action<string, float> log) : IDisposable
    {
        private const float FadeSeconds = 1.5f;

        private ILoadedSound? sound;
        private bool fading;

        public bool Playing => sound?.IsPlaying == true && !fading;

        public void Play(double amount, Vec3d at)
        {
            var volume = (float)Curve.At(cue.Volume, amount);
            if (volume < Silent || string.IsNullOrEmpty(cue.Sound))
            {
                Silence();
                return;
            }
            if (fading) return;

            sound ??= capi.World.LoadSound(new SoundParams
            {
                Location = new AssetLocation(cue.Sound).WithPathAppendixOnce(".ogg"),
                ShouldLoop = true,
                Position = at.ToVec3f(),
                DisposeOnFinish = false,
                Range = range,
                SoundType = EnumSoundType.Ambient,
                Volume = 0
            });
            if (sound is not { IsReady: true }) return;

            sound.SetPosition(at.ToVec3f());
            sound.SetVolume(volume);
            if (sound.IsPlaying) return;

            sound.Start();
            sound.PlaybackPosition = sound.SoundLengthSeconds * (float)capi.World.Rand.NextDouble();
            log("starts", volume);
        }

        public void Silence()
        {
            if (!Playing) return;
            fading = true;
            sound!.FadeOut(FadeSeconds, faded => { faded.Stop(); fading = false; });
            log("fades out", 0);
        }

        public override string ToString() => sound?.IsPlaying == true ? $"playing at {sound.Params.Volume:F2}" : "silent";

        public void Dispose() => sound?.Dispose();
    }
}
