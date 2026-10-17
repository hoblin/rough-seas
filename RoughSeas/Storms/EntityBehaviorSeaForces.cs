using System;
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

    private const double FounderedMetres = 100;
    private const string HoldAnimation = "swimidle";
    private const float HoldDepthMetres = -0.85f;
    private const float HoldReachMetres = 0.15f;

    private StormsModSystem storms = null!;
    private ISeaWeather sea = null!;
    private EntityBehaviorVesselIntegrity? hull;
    private EntityBehaviorBilge? bilge;
    private EntityBehaviorMooring? mooring;
    private double windageSquareMetres;
    private Seaworthiness dry;
    private float speedMultiplier;
    private Vec3f?[]? dryOffsets;
    private float sinceUpdate;
    private double sinceWave;

    /// <summary>Once a wave period, server side, while the vessel floats and the oceans are dangerous: the sea state of that wave.</summary>
    public event Action<SeaState>? Wave;

    public double BeamMetres { get; private set; }

    public double MastMetres { get; private set; }

    /// <summary>How she takes the sea now, with the water in her bilge.</summary>
    public Seaworthiness Seaworthiness => dry.Laden(bilge?.Fill ?? 0, storms.Config.Swamping);

    /// <summary>
    /// How much lower than empty the water aboard makes her float: down to her gunwale when full, or, heavier than
    /// water when swamped, to the bottom.
    /// </summary>
    public double SettlingMetres => bilge switch
    {
        null => 0,
        { Swamped: true, Sinks: true } => FounderedMetres,
        _ => dry.FreeboardMetres - Seaworthiness.FreeboardMetres
    };

    public override string PropertyName() => Code;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        storms = entity.Api.ModLoader.GetModSystem<StormsModSystem>();
        sea = entity.Api.ModLoader.GetModSystem<SeaWeatherSystem>();
        BeamMetres = attributes["beamMetres"].AsDouble();
        MastMetres = attributes["mastMetres"].AsDouble();
        windageSquareMetres = attributes["windageSquareMetres"].AsDouble();
        dry = Seaworthiness.From(attributes);
        speedMultiplier = properties.Attributes?["speedMultiplier"].AsFloat(1) ?? 1;
    }

    public override void AfterInitialized(bool onFirstSpawn)
    {
        hull = entity.GetBehavior<EntityBehaviorVesselIntegrity>();
        bilge = entity.GetBehavior<EntityBehaviorBilge>();
        mooring = entity.GetBehavior<EntityBehaviorMooring>();
    }

    public double Force(SeaState state) =>
        storms.Waves.Force(state.WindSpeed, state.WaveHeight, BeamMetres, windageSquareMetres) * Exposure;

    public double BreakerEnergy(double waveHeight) => storms.Waves.BreakerEnergy(waveHeight, BeamMetres);

    public double WaterDepth(int maxBlocks)
    {
        var accessor = entity.World.BlockAccessor;
        var pos = entity.Pos.AsBlockPos;
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
        if (bilge != null) Lade();
        if (entity.World.Side != EnumAppSide.Server) return;
        sinceUpdate += deltaTime;
        if (sinceUpdate < storms.Config.TickSeconds) return;

        sinceWave += sinceUpdate;
        sinceUpdate = 0;
        if (!entity.FeetInLiquid || !sea.DangerousOceans) return;

        using var _ = storms.Budget.Measure();
        var state = sea.At(entity.Pos.X, entity.Pos.Z);
        if (state.WaveHeight < storms.Config.Waves.MinHeight || sinceWave < storms.Waves.Period(state.WaveHeight)) return;

        sinceWave = 0;
        Wave?.Invoke(state);
        TakeWave(state);
    }

    private void TakeWave(SeaState state)
    {
        var waves = storms.Waves;
        var rand = entity.World.Rand;
        var height = Waves.WaveHeight(state.WaveHeight, rand.NextDouble());
        var swamped = bilge?.Swamped == true;
        var surf = waves.BreaksAt(WaterDepth(storms.Config.Waves.MaxDepthBlocks), height);
        var breaking = swamped || surf || rand.NextDouble() < waves.BreakingChance(state.WindSpeed);
        var crest = waves.Crest(height, breaking);
        var shipped = waves.ShippedLitres(height, BeamMetres) * Seaworthiness.ShippedShare(crest) * Exposure;
        var capsizes = breaking && Seaworthiness.Capsizes(height);
        if (!breaking && shipped <= 0) return;

        bilge?.Ship(shipped);
        var cause = swamped ? "swamped" : surf ? "surf" : breaking ? "whitecap" : "swell";
        storms.Log(entity, "{0} wave {1:F1} m in a {2:F1} m sea, {3:F2} m over a {4:F2} m freeboard: shipped {5:F0} L, {6}",
            cause, height, state.WaveHeight, Seaworthiness.Overtopping(crest), Seaworthiness.FreeboardMetres, shipped,
            bilge == null ? "washed off" : $"bilge {bilge.Litres:F0} of {bilge.CapacityLitres:F0} L");
        if (breaking && hull != null) Strike(height, hull);
        if (capsizes) Capsize(height, swamped);
    }

    private void Strike(double breakerMetres, EntityBehaviorVesselIntegrity hull)
    {
        var energy = BreakerEnergy(breakerMetres) * (1 + (bilge?.Fill ?? 0)) * Exposure;
        hull.Strike(energy);
        storms.Log(entity, "{0:F1} m breaker struck with {1:F0} J, hull now {2:F2}", breakerMetres, energy, hull.Integrity);
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
        bilge?.Ship(bilge.CapacityLitres);
    }

    private void Lade()
    {
        var swamped = bilge!.Swamped;
        if (entity is EntityBoat boat)
        {
            var dryMass = Mass();
            boat.SpeedMultiplier = swamped ? 0 : speedMultiplier * (float)Math.Pow(dryMass / (dryMass + bilge.Litres), storms.Config.Swamping.SpeedExponent);
            var seats = Seats;
            dryOffsets ??= seats.Select(seat => seat.Config?.RiderOffset).ToArray();
            for (var i = 0; i < seats.Length; i++) Hold(boat, seats[i], swamped ? Alongside(i) : dryOffsets[i], swamped);
        }
        if (!swamped || !bilge.Sinks || entity.World.Side != EnumAppSide.Server) return;

        var thrown = 0;
        foreach (var seat in Seats)
        {
            if (seat.Passenger is EntityAgent passenger && passenger.TryUnmount()) thrown++;
        }
        if (thrown > 0) storms.Log(entity, "foundered: {0} thrown in the water", thrown);
    }

    private Vec3f Alongside(int seat) => new(0, HoldDepthMetres, (float)(BeamMetres / 2 + HoldReachMetres) * (seat % 2 == 0 ? 1 : -1));

    private static void Hold(EntityBoat boat, IMountableSeat seat, Vec3f? offset, bool swamped)
    {
        if (seat.Config != null) seat.Config.RiderOffset = offset;
        var animations = seat.Passenger?.AnimManager;
        if (animations == null || animations.IsAnimationActive(HoldAnimation) == swamped) return;

        var seated = seat.Config?.Animation ?? boat.MountAnimations["idle"];
        animations.StopAnimation(swamped ? seated : HoldAnimation);
        animations.StartAnimation(swamped ? HoldAnimation : seated);
    }

    private double Mass() => entity.Properties.Weight + Seats.Sum(seat => seat.Passenger?.Properties.Weight ?? 0);

    private IMountableSeat[] Seats => entity.GetBehavior<EntityBehaviorSeatable>()?.Seats ?? [];

    private double Exposure => mooring?.Exposure ?? 1;
}
