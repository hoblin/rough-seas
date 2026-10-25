using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using RoughSeas.SeaAndWeather;
using RoughSeas.Vessels;

namespace RoughSeas.Storms;

public class EntityBehaviorSeaForces(Entity entity) : EntityBehavior(entity)
{
    public const string Code = "roughseas:seaforces";

    private const string HoldAnimation = "swimidle";
    private const float HoldDepthMetres = -0.85f;
    private const float HoldReachMetres = 0.15f;

    private StormsModSystem storms = null!;
    private VesselsModSystem vessels = null!;
    private ISeaWeather sea = null!;
    private EntityBehaviorVesselIntegrity? hull;
    private IBuoyant? buoyant;
    private EntityBehaviorBilge? bilge;
    private EntityBehaviorMooring? mooring;
    private double windageSquareMetres;
    private UnderwaterConfig underwater = new();
    private UnderwaterBody? body;
    private Seaworthiness dry;
    private float speedMultiplier;
    private readonly Dictionary<string, SeatPose> seated = new();
    private EntityBehaviorSeatable? seatable;
    private SeaClock.Watch watch = null!;
    private float sinceUpdate;
    private double sinceWave;

    /// <summary>Once a wave period, server side, while the vessel floats and the oceans are dangerous: the sea state of that wave.</summary>
    public event Action<SeaState>? Wave;

    /// <summary>Server side, each wave as she takes it, whether or not it broke on her or shipped water.</summary>
    public event Action<TakenWave>? Took;

    public double BeamMetres { get; private set; }

    public double MastMetres { get; private set; }

    public IBuoyant? Buoyant => buoyant;

    /// <summary>What she shows the water below her waterline now, at her live draft and mass; null where she has no draft.</summary>
    public UnderwaterBody? Underwater
    {
        get
        {
            if (buoyant is not { HasDraft: true } || IHull.Of(entity) is not { } hull) return null;

            var (draft, mass) = (buoyant.Afloat.DraftMetres, buoyant.Aboard.Total);
            if (body is { } known && known.DraftMetres == draft && known.MassKg == mass) return known;

            var shape = hull.Shape;
            return body = UnderwaterBody.Of(2 * shape.HalfLength, 2 * shape.HalfBeam, draft, mass, underwater, storms.Config.Drift);
        }
    }

    /// <summary>How she takes the sea now, with the water in her bilge.</summary>
    public Seaworthiness Seaworthiness
    {
        get
        {
            var laden = dry.Laden(bilge?.Fill ?? 0, storms.Config.Swamping);
            return buoyant is { HasDraft: true } ? laden.Floating(buoyant.Afloat.FreeboardMetres) : laden;
        }
    }

    public double SettlingMetres => buoyant is { HasDraft: true } ? buoyant.SettlingMetres : dry.FreeboardMetres - Seaworthiness.FreeboardMetres;

    public override string PropertyName() => Code;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        storms = entity.Api.ModLoader.GetModSystem<StormsModSystem>();
        vessels = entity.Api.ModLoader.GetModSystem<VesselsModSystem>();
        sea = entity.Api.ModLoader.GetModSystem<SeaWeatherSystem>();
        watch = sea.Clock.StartWatch();
        BeamMetres = attributes["beamMetres"].AsDouble();
        MastMetres = attributes["mastMetres"].AsDouble();
        windageSquareMetres = attributes["windageSquareMetres"].AsDouble();
        underwater = attributes["underwater"].AsObject<UnderwaterConfig>(new UnderwaterConfig());
        dry = Seaworthiness.From(attributes);
        speedMultiplier = properties.Attributes?["speedMultiplier"].AsFloat(1) ?? 1;
    }

    public override void AfterInitialized(bool onFirstSpawn)
    {
        hull = entity.GetBehavior<EntityBehaviorVesselIntegrity>();
        buoyant = entity.SidedProperties.Behaviors.OfType<IBuoyant>().FirstOrDefault();
        bilge = entity.GetBehavior<EntityBehaviorBilge>();
        mooring = entity.GetBehavior<EntityBehaviorMooring>();
        seatable = entity.GetBehavior<EntityBehaviorSeatable>();
    }

    public double Force(SeaState state) =>
        storms.Waves.Force(state.WindSpeed, state.WaveHeight, BeamMetres, windageSquareMetres) * Exposure;

    public double WaterDepth(int maxBlocks) => WaterDepth(entity.Pos.XYZ, maxBlocks);

    private double WaterDepth(Vec3d at, int maxBlocks)
    {
        var accessor = entity.World.BlockAccessor;
        var pos = at.Add(0, -EntityBehaviorVesselIntegrity.WaterlineProbeMetres, 0).AsBlockPos;
        var depth = 0;
        while (depth < maxBlocks && accessor.GetBlock(pos, BlockLayersAccess.Fluid).IsLiquid())
        {
            depth++;
            pos.Down();
        }
        return depth;
    }

    public override void OnGameTick(float deltaTime)
    {
        if (!entity.Alive) return;
        if (bilge != null || buoyant != null) Lade();
        if (entity.World.Side != EnumAppSide.Server) return;
        sinceUpdate += deltaTime;
        if (sinceUpdate < storms.Config.TickSeconds) return;

        sinceUpdate = 0;
        sinceWave += watch.Lap().LivedSeconds;
        if (!entity.Swimming || !sea.DangerousOceans)
        {
            sinceWave = 0;
            return;
        }

        using var _ = storms.Budget.Measure();
        var state = sea.At(entity.Pos.X, entity.Pos.Z);
        if (state.WaveHeight < storms.Config.Waves.MinHeight)
        {
            sinceWave = 0;
            return;
        }

        for (; sinceWave >= state.WavePeriod && entity.Alive; sinceWave -= state.WavePeriod)
        {
            Wave?.Invoke(state);
            TakeWave(state);
        }
    }

    private void TakeWave(SeaState state)
    {
        var waves = storms.Waves;
        var rand = entity.World.Rand;
        var height = Waves.WaveHeight(state.WaveHeight, rand.NextDouble());
        var swamped = bilge?.Swamped == true;
        var depth = WaterDepth(storms.Config.Waves.MaxDepthBlocks);
        var surf = waves.BreaksAt(depth, height);
        var breaks = surf || rand.NextDouble() < waves.BreakingChance(state.WindSpeed);
        var breaking = swamped || breaks;
        var crest = waves.Crest(height, breaking);
        var shipped = waves.ShippedLitres(height, BeamMetres) * Seaworthiness.ShippedShare(crest) * Exposure;
        var capsizes = storms.Config.Swamping.Capsizes && breaking && Seaworthiness.Capsizes(height);
        Took?.Invoke(new TakenWave(height, breaking, shipped, -state.WindX, -state.WindZ));
        if (!breaking && shipped <= 0) return;

        bilge?.Ship(shipped, "by the sea");
        var cause = swamped ? "swamped" : surf ? "surf" : breaking ? "whitecap" : "swell";
        storms.Log(entity, "{0} wave {1:F1} m in a {2:F1} m sea, {3:F2} m over a {4:F2} m freeboard: shipped {5:F0} L, {6}",
            cause, height, state.WaveHeight, Seaworthiness.Overtopping(crest), Seaworthiness.FreeboardMetres, shipped,
            bilge == null ? "washed off" : $"bilge {bilge.Litres:F0} of {bilge.CapacityLitres:F0} L");
        if (breaks && hull != null) Slam(height, state, depth, surf, hull);
        if (capsizes) Capsize(height, swamped);
    }

    private void Slam(double breakerMetres, SeaState state, double depth, bool surf, EntityBehaviorVesselIntegrity hull)
    {
        var waves = storms.Waves;
        var plunging = surf
            ? waves.Plunges(breakerMetres, state.WavePeriod, BedSlope(state, depth), depth)
            : waves.PlungesAtSea(entity.World.Rand.NextDouble());
        var celerity = waves.BreakerCelerity(breakerMetres, state.WavePeriod,
            depth < storms.Config.Waves.MaxDepthBlocks ? depth : double.PositiveInfinity);
        var pressure = waves.SlamPressure(celerity, plunging);
        var place = hull.Toward(-state.WindX, -state.WindZ);
        storms.Log(entity, "{0:F1} m {1} breaker at {2:F1} m/s slams the {3} with {4:F0} kPa", breakerMetres,
            plunging ? "plunging" : surf ? "broken" : "spilling", celerity, place.Code(), pressure / 1000);
        hull.Strike(pressure, place, Blow.Slam);
    }

    private double BedSlope(SeaState state, double depth)
    {
        var wind = Math.Sqrt(state.WindX * state.WindX + state.WindZ * state.WindZ);
        if (wind == 0) return 0;

        var blocks = storms.Config.Waves.BedSlopeBlocks;
        var seaward = entity.Pos.XYZ.Add(-state.WindX / wind * blocks, 0, -state.WindZ / wind * blocks);
        return Math.Max(0, WaterDepth(seaward, storms.Config.Waves.MaxDepthBlocks) - depth) / blocks;
    }

    private void Capsize(double breakerMetres, bool swamped)
    {
        var thrown = 0;
        foreach (var seat in Seats)
        {
            if (seat.Passenger is EntityAgent passenger && passenger.TryUnmount()) thrown++;
        }
        if (thrown == 0 && (swamped || bilge == null)) return;

        storms.Log(entity, "capsized by a {0:F1} m breaker against {1:F2} m: {2} thrown in the water", breakerMetres, Seaworthiness.CapsizeBreakerMetres, thrown);
        bilge?.Ship(bilge.CapacityLitres, "capsized");
    }

    private void Lade()
    {
        var swamped = bilge?.Swamped == true;
        if (entity is EntityBoat boat)
        {
            boat.SpeedMultiplier = swamped ? 0 : speedMultiplier * (float)SpeedFactor();
            var seats = Seats;
            for (var i = 0; i < seats.Length; i++)
            {
                var seat = seats[i];
                if (seat.Config == null) continue;
                if (!seated.TryGetValue(seat.SeatId, out var dry))
                {
                    seated[seat.SeatId] = dry = new SeatPose(seat.Config.RiderOffset, seat.Config.Animation ?? boat.MountAnimations["idle"]);
                }
                Hold(seat, swamped ? new SeatPose(Alongside(i), HoldAnimation) : dry);
            }
        }
        if (buoyant?.Afloat.Sunk == true && entity.World.Side == EnumAppSide.Server) Founder();
    }

    private double SpeedFactor()
    {
        if (buoyant != null) return storms.Config.Swamping.SpeedFactor(buoyant.ReferenceKg, buoyant.Aboard.Total);

        var mass = vessels.Mass;
        return storms.Config.Swamping.SpeedFactor(entity.Properties.Weight + mass.CrewKg, mass.Of(entity, entity.Properties.Weight).Total);
    }

    private void Founder()
    {
        var thrown = 0;
        foreach (var seat in Seats)
        {
            if (seat.Passenger is EntityAgent passenger && passenger.TryUnmount()) thrown++;
        }
        if (thrown > 0) storms.Log(entity, "foundered: {0} thrown in the water", thrown);
    }

    private Vec3f Alongside(int seat) => new(0, HoldDepthMetres, (float)(BeamMetres / 2 + HoldReachMetres) * (seat % 2 == 0 ? 1 : -1));

    /// <summary>
    /// Puts the seat in this pose. The pose is the seat's own animation, which vanilla stops when its rider leaves, so a
    /// rider who lets go of a swamped hull does not keep holding on.
    /// </summary>
    private static void Hold(IMountableSeat seat, SeatPose pose)
    {
        var config = seat.Config;
        var shown = config.Animation;
        config.RiderOffset = pose.Offset;
        config.Animation = pose.Animation;
        if (shown == pose.Animation || seat.Passenger?.AnimManager is not { } animations) return;

        if (shown != null) animations.StopAnimation(shown);
        animations.StartAnimation(pose.Animation);
    }

    private readonly record struct SeatPose(Vec3f? Offset, string Animation);

    /// <param name="Height">This one wave's height, sampled from the sea's significant height; not a crest the client renders.</param>
    /// <param name="FromX">With <paramref name="FromZ"/>, the world direction the wave came from, not normalised.</param>
    public readonly record struct TakenWave(double Height, bool Breaking, double ShippedLitres, double FromX, double FromZ);

    private IMountableSeat[] Seats => seatable?.Seats ?? [];

    private double Exposure => mooring?.Exposure ?? 1;
}
