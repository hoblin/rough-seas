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
using RoughSeas.Water;

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
    private JsonObject config = null!;
    private double? designedBeamMetres;
    private Seaworthiness dry;
    private (double BeamMetres, double LengthMetres) dryFor = (double.NaN, double.NaN);
    private float speedMultiplier;
    private EntityBehaviorSeatable? seatable;
    private IMasted? mast;
    private IHullWay? driven;
    private double designedMastMetres;
    private SeaClock.Watch watch = null!;
    private float sinceUpdate;
    private double sinceWave;

    /// <summary>Server side, each wave as she takes it, whether or not it broke on her or shipped water.</summary>
    public event Action<TakenWave>? Took;

    public double BeamMetres => designedBeamMetres ?? 2 * (hull?.Shape.HalfBeamMetres ?? 0);

    public double? LengthMetres => hull is { } measured ? 2 * measured.Shape.HalfLengthMetres : null;

    public double MastMetres => mast?.MastMetres(designedMastMetres) ?? designedMastMetres;

    public IBuoyant? Buoyant => buoyant;

    /// <summary>Whether vanilla's water drag is off her along the water just now (<see cref="IHullWay"/>).</summary>
    public bool Driven => driven?.Driving == true;

    /// <summary>How the drawn waves carry her on this side, while this side moves her.</summary>
    public WaveRide Ride { get; private set; } = null!;

    /// <summary>How she takes the sea now, with the water in her bilge.</summary>
    public Seaworthiness Seaworthiness
    {
        get
        {
            var laden = Dry.Laden(bilge?.Fill ?? 0, storms.Config.Swamping);
            return buoyant is { HasDraft: true } ? laden.Floating(buoyant.Afloat.FreeboardMetres) : laden;
        }
    }

    public double SettlingMetres => Dry.FreeboardMetres - Seaworthiness.FreeboardMetres;

    private Seaworthiness Dry
    {
        get
        {
            (double BeamMetres, double LengthMetres) size = (BeamMetres, LengthMetres ?? 0);
            if (size != dryFor) (dry, dryFor) = (Seaworthiness.From(config, size.BeamMetres, size.LengthMetres), size);
            return dry;
        }
    }

    public bool? KeelWet { get; set; }

    public Heave? Heave => buoyant is { HasDraft: true, Afloat.Sunk: false }
        ? new Heave(buoyant.Aboard.Total, buoyant.AddedKg, buoyant.KgPerMetre, buoyant.Afloat.DraftMetres, buoyant.DepthMetres, storms.Config.Response)
        : null;

    public override string PropertyName() => Code;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        storms = entity.Api.ModLoader.GetModSystem<StormsModSystem>();
        vessels = entity.Api.ModLoader.GetModSystem<VesselsModSystem>();
        var weather = entity.Api.ModLoader.GetModSystem<SeaWeatherSystem>();
        sea = weather;
        watch = sea.Clock.StartWatch();
        Ride = new WaveRide(storms, weather, this);
        config = attributes;
        designedBeamMetres = attributes["beamMetres"].Exists ? attributes["beamMetres"].AsDouble() : null;
        designedMastMetres = attributes["mastMetres"].AsDouble();
        speedMultiplier = properties.Attributes?["speedMultiplier"].AsFloat(1) ?? 1;
    }

    public override void AfterInitialized(bool onFirstSpawn)
    {
        hull = entity.GetBehavior<EntityBehaviorVesselIntegrity>();
        buoyant = entity.SidedProperties.Behaviors.OfType<IBuoyant>().FirstOrDefault();
        bilge = entity.GetBehavior<EntityBehaviorBilge>();
        mooring = entity.GetBehavior<EntityBehaviorMooring>();
        seatable = entity.GetBehavior<EntityBehaviorSeatable>();
        mast = entity.SidedProperties.Behaviors.OfType<IMasted>().FirstOrDefault();
        driven = entity.SidedProperties.Behaviors.OfType<IHullWay>().FirstOrDefault();
    }

    public double WaterDepth(int maxBlocks) => Sound(entity.Pos.XYZ, maxBlocks).Depth;

    public double WaterDepth(double alongMetres, double acrossMetres, int maxBlocks)
    {
        var (x, z) = HullShape.ToWorld(alongMetres, acrossMetres, entity.Pos.Yaw);
        return Sound(entity.Pos.XYZ.Add(x, 0, z), maxBlocks).Depth;
    }

    public Block Seabed(int maxBlocks) => entity.World.BlockAccessor.GetBlock(Sound(entity.Pos.XYZ, maxBlocks).Bottom);

    private (double Depth, BlockPos Bottom) Sound(Vec3d at, int maxBlocks) =>
        WaterColumn.Sound(entity.World.BlockAccessor, at.Add(0, -EntityBehaviorVesselIntegrity.WaterlineProbeMetres, 0).AsBlockPos, maxBlocks);

    public override void OnGameTick(float deltaTime)
    {
        if (!entity.Alive) return;
        if (bilge != null || buoyant != null) Lade();
        if (entity.World.Side != EnumAppSide.Server) return;
        sinceUpdate += deltaTime;
        if (sinceUpdate < storms.Config.TickSeconds) return;

        sinceUpdate = 0;
        sinceWave += watch.Lap().LivedSeconds;
        if (!entity.Swimming)
        {
            sinceWave = 0;
            return;
        }

        using var _ = storms.Budget.Measure();
        var here = storms.Swells!.At(entity.Pos.X, entity.Pos.Z).WaveHere(entity.Pos.X, entity.Pos.Z);
        if (here.Height < storms.Config.Waves.MinHeight || here.Period <= 0)
        {
            sinceWave = 0;
            return;
        }

        var windMps = sea.At(entity.Pos.X, entity.Pos.Z).WindSpeed;
        for (; sinceWave >= here.Period && entity.Alive; sinceWave -= here.Period) TakeWave(here, windMps);
    }

    private void TakeWave(WaveReading here, double windMps)
    {
        var waves = storms.Waves;
        var rand = entity.World.Rand;
        var height = WaveTheory.RayleighHeight(here.Height, rand.NextDouble());
        var swamped = bilge?.Swamped == true;
        var surf = rand.NextDouble() < here.Breaking;
        var breaks = surf || rand.NextDouble() < waves.BreakingChance(windMps);
        var breaking = swamped || breaks;
        var crest = waves.Crest(height, breaking);
        var shipped = Mechanic.BilgeWater.On(entity.World) ? waves.ShippedLitres(height, BeamMetres) * Seaworthiness.ShippedShare(crest) * Exposure : 0;
        var capsizes = storms.Config.Swamping.Capsizes && breaking && Seaworthiness.Capsizes(height);
        var (fromX, fromZ) = (-Math.Cos(here.TowardRadians), -Math.Sin(here.TowardRadians));
        Took?.Invoke(new TakenWave(height, breaking, shipped, fromX, fromZ));
        if (!breaking && shipped <= 0) return;

        bilge?.Ship(shipped, "by the sea");
        var cause = swamped ? "swamped" : surf ? "surf" : breaking ? "whitecap" : "swell";
        storms.Log(entity, "{0} wave {1:F1} m in a {2:F1} m sea, {3:F2} m over a {4:F2} m freeboard: shipped {5:F0} L, {6}",
            cause, height, here.Height, Seaworthiness.Overtopping(crest), Seaworthiness.FreeboardMetres, shipped,
            bilge == null ? "washed off" : $"bilge {bilge.Litres:F0} of {bilge.CapacityLitres:F0} L");
        if (breaks && hull != null) Slam(height, here, surf, hull);
        if (capsizes) Capsize(height, swamped);
    }

    private void Slam(double breakerMetres, WaveReading here, bool surf, EntityBehaviorVesselIntegrity hull)
    {
        var waves = storms.Waves;
        var plunging = surf
            ? waves.Plunges(breakerMetres, here.Period, here.BedSlope, here.Depth)
            : waves.PlungesAtSea(entity.World.Rand.NextDouble());
        var celerity = waves.BreakerCelerity(breakerMetres, here.Period, here.Depth);
        var pressure = waves.SlamPressure(celerity, plunging);
        var place = hull.Toward(-Math.Cos(here.TowardRadians), -Math.Sin(here.TowardRadians));
        storms.Log(entity, "{0:F1} m {1} breaker at {2:F1} m/s slams the {3} with {4:F0} kPa", breakerMetres,
            plunging ? "plunging" : surf ? "broken" : "spilling", celerity, place.Code(), pressure / 1000);
        hull.Strike(pressure, place, Blow.Slam);
    }

    private void Capsize(double breakerMetres, bool swamped)
    {
        var thrown = ThrowRiders();
        if (thrown == 0 && (swamped || bilge == null)) return;

        storms.Log(entity, "capsized by a {0:F1} m breaker against {1:F2} m: {2} thrown in the water", breakerMetres, Seaworthiness.CapsizeBreakerMetres, thrown);
        if (Mechanic.BilgeWater.On(entity.World)) bilge?.Ship(bilge.CapacityLitres, "capsized");
    }

    private void Lade()
    {
        var swamped = bilge?.Swamped == true;
        if (entity is EntityBoat boat)
        {
            boat.SpeedMultiplier = swamped ? 0 : speedMultiplier * (float)SpeedFactor();
            var poses = SeatPoses.Of(boat);
            var seats = Seats;
            for (var i = 0; i < seats.Length; i++)
            {
                var seat = seats[i];
                poses.Hold(seat, swamped ? new SeatPose(Alongside(i), HoldAnimation) : null);
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
        var thrown = ThrowRiders();
        if (thrown > 0) storms.Log(entity, "foundered: {0} thrown in the water", thrown);
    }

    private int ThrowRiders() => Seats.Count(seat => seat.Passenger is EntityAgent rider && Overboard(seat, rider));

    [ReliesOn("Vintagestory.API.Common.EntityPlayer", "OnReceivedServerPacket", "a teleport on dismount reaches a client still seated, and moves the vessel")]
    private static bool Overboard(IMountableSeat seat, EntityAgent rider)
    {
        seat.DoTeleportOnUnmount = false;
        bool thrown = rider.TryUnmount();
        seat.DoTeleportOnUnmount = true;
        return thrown;
    }

    private Vec3f Alongside(int seat) => new(0, HoldDepthMetres, (float)(BeamMetres / 2 + HoldReachMetres) * (seat % 2 == 0 ? 1 : -1));

    /// <param name="Height">This one wave's height, sampled from the sea's significant height; not a crest the client renders.</param>
    /// <param name="FromX">With <paramref name="FromZ"/>, the world direction the wave came from, not normalised.</param>
    public readonly record struct TakenWave(double Height, bool Breaking, double ShippedLitres, double FromX, double FromZ);

    private IMountableSeat[] Seats => seatable?.Seats ?? [];

    private double Exposure => mooring?.Exposure ?? 1;
}
