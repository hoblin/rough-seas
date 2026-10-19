using System;
using System.Collections.Generic;
using System.Linq;
using RoughSeas.SeaAndWeather;
using RoughSeas.Storms;
using RoughSeas.Vessels;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace RoughSeas.Soundscape;

public sealed class VesselLoops(ICoreClientAPI capi, WaterConfig config, SeaWeatherSystem sea, SoundscapeModSystem sounds) : IDisposable
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
            voices.Update(vessel, sea);
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
        private readonly Loop leak = new(capi, config.Leak, config.Range, (state, volume) => sounds.Log(vessel, "leak trickle {0} at {1:F2}", state, volume));
        private readonly Loop lapping = new(capi, config.Lapping, config.Range, (state, volume) => sounds.Log(vessel, "lapping {0} at {1:F2}", state, volume));

        private string reading = "";

        public void Update(Entity vessel, SeaWeatherSystem sea)
        {
            var hull = vessel.GetBehavior<EntityBehaviorVesselIntegrity>();
            var bilge = vessel.GetBehavior<EntityBehaviorBilge>();
            var afloat = vessel.Alive && vessel.Swimming && hull?.Afloat.Sunk != true;
            var centre = vessel.Pos.XYZ;
            var fill = afloat ? bilge?.Fill ?? 0 : 0;
            var leaking = afloat && hull != null ? hull.Model.LitresPerSecond(hull.Afloat, hull.DepthMetres) : 0;
            var waves = afloat ? sea.At(centre.X, centre.Z).WaveHeight : 0;

            slosh.Play(fill, centre);
            leak.Play(leaking, Worst(hull, centre));
            lapping.Play(waves, centre);
            reading = $"{vessel.Code} {vessel.EntityId}: {(afloat ? "afloat" : "not afloat")}, bilge {fill:F2} full ({slosh}), " +
                $"leaking {leaking:F2} L/s ({leak}), waves {waves:F1} m ({lapping})";
        }

        public string Describe() => reading;

        public void Dispose()
        {
            slosh.Dispose();
            leak.Dispose();
            lapping.Dispose();
        }

        private static Vec3d Worst(EntityBehaviorVesselIntegrity? hull, Vec3d centre)
        {
            if (hull?.Model is not CrackingHull cracking) return centre;

            var place = HullShape.Places.MaxBy(cracking.Crack);
            return centre.AddCopy(hull.PointAt(place));
        }
    }

    private sealed class Loop(ICoreClientAPI capi, Cue cue, float range, Action<string, float> log) : IDisposable
    {
        private ILoadedSound? sound;

        public void Play(double amount, Vec3d at)
        {
            var volume = (float)Curve.At(cue.Volume, amount);
            if (volume < Silent || string.IsNullOrEmpty(cue.Sound))
            {
                if (sound?.IsPlaying != true) return;
                sound.Stop();
                log("stops", volume);
                return;
            }

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

        public override string ToString() => sound?.IsPlaying == true ? $"playing at {sound.Params.Volume:F2}" : "silent";

        public void Dispose() => sound?.Dispose();
    }
}
