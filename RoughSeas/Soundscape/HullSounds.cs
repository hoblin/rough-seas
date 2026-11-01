using System.Linq;
using RoughSeas.Storms;
using RoughSeas.Vessels;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RoughSeas.Soundscape;

public sealed class HullSounds(SoundscapeModSystem sounds, ICoreServerAPI sapi)
{
    private const string LashedModel = "lashed";

    private SoundscapeConfig Config => sounds.Config;

    public void Start()
    {
        sapi.Event.OnEntitySpawn += Hear;
        sapi.Event.OnEntityLoaded += Hear;
    }

    public HullVoice? VoiceOf(EntityBehaviorVesselIntegrity hull)
    {
        if (!Config.Hull.Models.TryGetValue(hull.Model.Code, out var voices) || voices.Count == 0) return null;
        return voices.TryGetValue(hull.Material, out var voice) || voices.TryGetValue("wood", out voice) ? voice : voices.Values.First();
    }

    private void Hear(Entity entity)
    {
        var hull = entity.GetBehavior<EntityBehaviorVesselIntegrity>();
        if (hull != null)
        {
            hull.Struck += (place, severity, knock) => Struck(entity, hull, place, severity, knock);
            hull.BrokeUp += () => BrokeUp(entity, hull);
            foreach (var outboard in entity.SidedProperties.Behaviors.OfType<IOutboard>()) outboard.Struck += knock => FloatStruck(entity, hull, outboard, knock);
        }
        if (entity.GetBehavior<EntityBehaviorSeaForces>() is { } forces) forces.Took += wave => Took(entity, hull, forces, wave);
    }

    private void Struck(Entity vessel, EntityBehaviorVesselIntegrity hull, HullPlace place, double severity, Knock? knock)
    {
        var voice = VoiceOf(hull);
        var sound = knock != null && voice?.Harm != null ? voice.Harm
            : severity >= Config.Hull.HardFromSeverity && voice?.Hard != null ? voice.Hard
            : voice?.Knock;
        Play(vessel, sound, vessel.Pos.XYZ.Add(hull.PointAt(place)), Config.Hull.Range, Curve.At(Config.Hull.Volume, severity),
            $"{knock?.ToString().ToLowerInvariant() ?? "knock"} at the {place.Code()}");
    }

    private void FloatStruck(Entity vessel, EntityBehaviorVesselIntegrity hull, IOutboard outboard, Knock knock)
    {
        if (!Config.Hull.Models.TryGetValue(LashedModel, out var voices) || !voices.TryGetValue(hull.Material, out var voice) && !voices.TryGetValue("wood", out voice)) return;

        var at = vessel.Pos.XYZ.Add(hull.PointAt(outboard.Side));
        var sounds = knock == Knock.BrokenUp ? voice.Break ?? [] : voice.Harm is { } harm ? [harm] : [];
        foreach (var sound in sounds) Play(vessel, sound, at, Config.Hull.Range, Config.Hull.BreakVolume, $"float lashing {knock.ToString().ToLowerInvariant()}");
    }

    private void BrokeUp(Entity vessel, EntityBehaviorVesselIntegrity hull)
    {
        foreach (var sound in VoiceOf(hull)?.Break ?? []) Play(vessel, sound, vessel.Pos.XYZ, Config.Hull.Range, Config.Hull.BreakVolume, "broke up");
    }

    private void Took(Entity vessel, EntityBehaviorVesselIntegrity? hull, EntityBehaviorSeaForces forces, EntityBehaviorSeaForces.TakenWave wave)
    {
        var water = Config.Water;
        var pos = vessel.Pos;
        if (sapi.World.NearestPlayer(pos.X, pos.Y, pos.Z)?.Entity?.Pos.DistanceTo(pos) is not { } nearest || nearest > water.Range) return;

        var side = Windward(vessel, hull, forces, wave);
        var (hit, what) = wave.Breaking ? ((Cue)water.Slam, "slam")
            : wave.Height >= water.StrongSlapFromMetres ? (water.StrongSlap, "strong slap")
            : (water.Slap, "slap");
        Play(vessel, hit.Sound, side, water.Range, hit.VolumeAt(wave.Height), $"{what} of a {wave.Height:F1} m wave");
        if (wave.ShippedLitres > 0 && vessel.GetBehavior<EntityBehaviorBilge>() is { CapacityLitres: > 0 }) Play(vessel, water.Pour.Sound, side, water.Range, water.Pour.VolumeAt(wave.ShippedLitres), $"{wave.ShippedLitres:F0} L over the gunwale");
    }

    private static Vec3d Windward(Entity vessel, EntityBehaviorVesselIntegrity? hull, EntityBehaviorSeaForces forces, EntityBehaviorSeaForces.TakenWave wave)
    {
        if (hull != null) return vessel.Pos.XYZ.Add(hull.PointAt(hull.Toward(wave.FromX, wave.FromZ)));

        var length = new Vec2d(wave.FromX, wave.FromZ).Length();
        var reach = length > 0 ? forces.BeamMetres / 2 / length : 0;
        return vessel.Pos.XYZ.Add(wave.FromX * reach, 0, wave.FromZ * reach);
    }

    private void Play(Entity vessel, string? sound, Vec3d at, float range, double volume, string what)
    {
        if (string.IsNullOrEmpty(sound) || volume <= 0) return;

        sapi.World.PlaySoundAt(new AssetLocation(sound), at.X, at.Y, at.Z, null, true, range, (float)volume);
        sounds.Log(vessel, "{0}: {1} at volume {2:F2}", what, sound, volume);
    }
}
