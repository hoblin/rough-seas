using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using RoughSeas.SeaAndWeather;
using RoughSeas.Vessels;

namespace RoughSeas.Storms;

public class EntityBehaviorSeaForces(Entity entity) : EntityBehavior(entity)
{
    public const string Code = "roughseas:seaforces";

    private StormsModSystem storms = null!;
    private SeaWeatherSystem sea = null!;
    private EntityBehaviorVesselIntegrity? hull;
    private EntityBehaviorBilge? bilge;
    private EntityBehaviorMooring? mooring;
    private double windageSquareMetres;
    private float sinceUpdate;
    private double sinceWave;

    public double BeamMetres { get; private set; }

    public double MastMetres { get; private set; }

    public override string PropertyName() => Code;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        storms = entity.Api.ModLoader.GetModSystem<StormsModSystem>();
        sea = entity.Api.ModLoader.GetModSystem<SeaWeatherSystem>();
        BeamMetres = attributes["beamMetres"].AsDouble();
        MastMetres = attributes["mastMetres"].AsDouble();
        windageSquareMetres = attributes["windageSquareMetres"].AsDouble();
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
        if (entity.World.Side != EnumAppSide.Server || hull == null) return;
        sinceUpdate += deltaTime;
        if (sinceUpdate < storms.Config.TickSeconds) return;

        sinceWave += sinceUpdate;
        sinceUpdate = 0;
        if (!entity.FeetInLiquid || !sea.DangerousOceans) return;

        using var _ = sea.Budget.Measure();
        var state = sea.At(entity.Pos.X, entity.Pos.Z);
        var waves = storms.Waves;
        if (state.WaveHeight < storms.Config.Waves.MinHeight || sinceWave < waves.Period(state.WaveHeight)) return;

        sinceWave = 0;
        var swamped = bilge?.Swamped == true;
        var surf = waves.BreaksAt(WaterDepth(storms.Config.Waves.MaxDepthBlocks), state.WaveHeight);
        var breaks = swamped || surf ? 1 : waves.BreakingChance(state.WindSpeed);
        if (entity.World.Rand.NextDouble() >= breaks) return;

        var energy = BreakerEnergy(state.WaveHeight) * (1 + (bilge?.Fill ?? 0)) * Exposure;
        var shipped = waves.ShippedLitres(state.WaveHeight, BeamMetres) * Exposure;
        hull.Strike(energy);
        bilge?.Ship(shipped);
        storms.Log(entity, "breaker {0:F1} m ({1}), {2:F0} J, shipped {3:F0} L, hull now {4:F2}",
            state.WaveHeight, swamped ? "swamped" : surf ? "surf" : "whitecap", energy, shipped, hull.Integrity);
    }

    private double Exposure => mooring?.Exposure ?? 1;
}
