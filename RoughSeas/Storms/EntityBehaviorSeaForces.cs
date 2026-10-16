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

    private StormsModSystem storms = null!;
    private ISeaWeather sea = null!;
    private EntityBehaviorVesselIntegrity? hull;
    private EntityBehaviorBilge? bilge;
    private EntityBehaviorMooring? mooring;
    private double windageSquareMetres;
    private float sinceUpdate;
    private double sinceWave;

    /// <summary>Once a wave period, server side, while the vessel floats and the oceans are dangerous: the sea state of that wave.</summary>
    public event Action<SeaState>? Wave;

    public double BeamMetres { get; private set; }

    public double MastMetres { get; private set; }

    public Seaworthiness Seaworthiness { get; private set; }

    public override string PropertyName() => Code;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        storms = entity.Api.ModLoader.GetModSystem<StormsModSystem>();
        sea = entity.Api.ModLoader.GetModSystem<SeaWeatherSystem>();
        BeamMetres = attributes["beamMetres"].AsDouble();
        MastMetres = attributes["mastMetres"].AsDouble();
        windageSquareMetres = attributes["windageSquareMetres"].AsDouble();
        Seaworthiness = Seaworthiness.From(attributes);
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
        if (entity.World.Side != EnumAppSide.Server || !entity.Alive) return;
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
        Meet(state);
    }

    private void Meet(SeaState state)
    {
        var waves = storms.Waves;
        var rand = entity.World.Rand;
        var swamped = bilge?.Swamped == true;
        var surf = waves.BreaksAt(WaterDepth(storms.Config.Waves.MaxDepthBlocks), state.WaveHeight);
        var breaking = swamped || surf || rand.NextDouble() < waves.BreakingChance(state.WindSpeed);
        var height = Waves.OneWave(state.WaveHeight, rand.NextDouble());
        var crest = waves.Crest(height, breaking);
        var shipped = waves.ShippedLitres(height, BeamMetres) * Seaworthiness.ShippedShare(crest) * Exposure;
        var capsizes = breaking && Seaworthiness.Capsizes(height);
        if (!breaking && shipped <= 0) return;

        bilge?.Ship(shipped);
        var cause = swamped ? "swamped" : surf ? "surf" : breaking ? "whitecap" : "swell";
        storms.Log(entity, "{0} wave {1:F1} m in a {2:F1} m sea, {3:F2} m over a {4:F2} m freeboard: shipped {5:F0} L, {6}",
            cause, height, state.WaveHeight, Seaworthiness.Overtopping(crest), Seaworthiness.FreeboardMetres, shipped,
            bilge == null ? "washed off" : $"bilge {bilge.Litres:F0} of {bilge.CapacityLitres:F0} L");
        if (breaking && hull != null) Strike(state, hull);
        if (capsizes) Capsize(height, swamped);
    }

    private void Strike(SeaState state, EntityBehaviorVesselIntegrity hull)
    {
        var energy = BreakerEnergy(state.WaveHeight) * (1 + (bilge?.Fill ?? 0)) * Exposure;
        hull.Strike(energy);
        storms.Log(entity, "breaker struck with {0:F0} J, hull now {1:F2}", energy, hull.Integrity);
    }

    private void Capsize(double breaker, bool swamped)
    {
        var seats = entity.GetBehavior<EntityBehaviorSeatable>()?.Seats ?? [];
        var thrown = seats.Select(seat => seat.Passenger).OfType<EntityAgent>().Count(passenger => passenger.TryUnmount());
        if (swamped && thrown == 0) return;

        storms.Log(entity, "capsized by a {0:F1} m breaker against {1:F2} m: {2} thrown in the water", breaker, Seaworthiness.CapsizeBreakerMetres, thrown);
        bilge?.Ship(bilge.CapacityLitres);
    }

    private double Exposure => mooring?.Exposure ?? 1;
}
