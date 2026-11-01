using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using RoughSeas.SeaAndWeather;
using RoughSeas.Vessels;

namespace RoughSeas.Storms;

/// <summary>
/// Moves a floating hull through the water where her physics runs: on the server while nobody steers her, on the
/// helm's client while someone does. The water moves with the current; her
/// <see cref="UnderwaterBody"/> answers it, and the stroke that drives her, the same way on both sides. Vanilla's
/// water drag, a fixed factor a tick, is taken off her while she floats with a known body.
/// </summary>
public class EntityBehaviorCurrentDrift(Entity entity) : EntityBehavior(entity)
{
    public const string Code = "roughseas:currentdrift";

    /// <summary>Vanilla's motion is in blocks per 1/60 s: passive physics moves an entity by <c>Motion * 60 * dt</c>.</summary>
    private const double TicksPerSecond = 60;

    /// <summary>Vanilla's passive physics scales a swimming entity's motion by <c>WaterDragValue^(dt * 33)</c>.</summary>
    private const double DragTicksPerSecond = 33;

    private const float MaxStepSeconds = 0.5f;
    private const double StillMps = 0.01;

    private StormsModSystem storms = null!;
    private SeaWeatherSystem sea = null!;
    private EntityBehaviorPassivePhysics? physics;
    private EntityBehaviorSeaForces? forces;
    private IMountable? mountable;
    private double waterDrag = GlobalConstants.WaterDrag;
    private bool? here;
    private float sinceLog;
    private UnderwaterBody? logged;

    /// <summary>The water's velocity over the ground where she last moved, m/s: the current's.</summary>
    public (double X, double Z) Water { get; private set; }

    /// <summary>Her velocity over the ground, m/s.</summary>
    public (double X, double Z) Velocity => (entity.Pos.Motion.X * TicksPerSecond, entity.Pos.Motion.Z * TicksPerSecond);

    public UnderwaterBody? Body => forces?.Underwater;

    public override string PropertyName() => Code;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        storms = entity.Api.ModLoader.GetModSystem<StormsModSystem>();
        sea = entity.Api.ModLoader.GetModSystem<SeaWeatherSystem>();
        mountable = entity.GetInterface<IMountable>();
        // a client speaks only of the hulls it steers
        if (entity.Api.Side == EnumAppSide.Client) here = false;
    }

    public override void AfterInitialized(bool onFirstSpawn)
    {
        forces = entity.GetBehavior<EntityBehaviorSeaForces>();
        physics = entity.GetBehavior<EntityBehaviorPassivePhysics>();
        if (physics != null) waterDrag = physics.WaterDragValue;
    }

    /// <summary>
    /// Moves her for <paramref name="deltaTime"/> seconds, called right after vanilla's
    /// <c>EntityBoat.updateBoatAngleAndMotion</c> on both sides; it acts only on the side her physics runs on.
    /// <paramref name="before"/> is her motion before vanilla's steering rewrote it from her forward speed.
    /// </summary>
    internal void Step(EntityBoat boat, float deltaTime, (double X, double Z) before)
    {
        if (physics == null || !Simulated()) return;

        var body = entity.Swimming ? Body : null;
        // vanilla's water drag stays on a hull that isn't afloat, or whose underwater body is unknown
        physics.WaterDragValue = body == null ? waterDrag : 1;
        if (!entity.Swimming) return;

        using var _ = storms.Budget.Measure();
        var seconds = Math.Min(deltaTime, MaxStepSeconds);
        var motion = entity.Pos.Motion;
        var drag = Math.Pow(waterDrag, seconds * DragTicksPerSecond);
        var pos = entity.Pos;
        var current = sea.CurrentAt(pos.X, pos.Z);
        Water = (current.X, current.Z);
        if (body == null)
        {
            // carried as vanilla's drag would take her toward the water's speed instead of rest, or wholly where the
            // steering has just set her speed through it
            var share = boat.ForwardSpeed != 0 ? 1 : 1 - drag;
            motion.X += Water.X / TicksPerSecond * share;
            motion.Z += Water.Z / TicksPerSecond * share;
            return;
        }

        // vanilla's drag still damps her heave
        motion.Y *= drag;
        var row = Row(boat, motion);
        var (vx, vz) = body.Carry(before.X * TicksPerSecond, before.Z * TicksPerSecond, Water.X, Water.Z, pos.Yaw, row.X, row.Z, seconds);
        motion.X = vx / TicksPerSecond;
        motion.Z = vz / TicksPerSecond;
        Log(body, row, seconds);
    }

    /// <summary>
    /// The speed through still water vanilla's stroke or sail holds her at, m/s: the motion its steering sets, less
    /// the one physics frame of vanilla's water drag it would have met before moving her.
    /// </summary>
    private (double X, double Z) Row(EntityBoat boat, Vec3d motion)
    {
        if (boat.ForwardSpeed == 0) return (0, 0);

        var frame = Math.Pow(waterDrag, GlobalConstants.PhysicsFrameTime * DragTicksPerSecond) * TicksPerSecond;
        return (motion.X * frame, motion.Z * frame);
    }

    private bool Simulated()
    {
        if (entity.Api is ICoreClientAPI capi)
        {
            var steeredHere = mountable?.Controller is { } helm && helm == capi.World.Player?.Entity;
            Hand(steeredHere, "steered from this client: she moves here", "no longer steered from this client");
            return steeredHere;
        }

        var unsteered = mountable?.IsBeingControlled() != true;
        Hand(unsteered, "unsteered: the server moves her", "steered: her helm's client moves her");
        return unsteered;
    }

    private void Hand(bool simulated, string taken, string released)
    {
        if (simulated == here) return;

        here = simulated;
        storms.Log(entity, "drift: {0}", simulated ? taken : released);
    }

    private void Log(UnderwaterBody body, (double X, double Z) row, float seconds)
    {
        if (logged == null || Math.Abs(body.MassKg - logged.MassKg) > 1 || Math.Abs(body.DraftMetres - logged.DraftMetres) > 0.005)
        {
            logged = body;
            storms.Log(entity, "underwater: {0:F2} x {1:F2} m at {2:F2} m draft, {3:F2} m2 bow-on, {4:F2} m2 broadside; {5:F0} kg, {6:F0} kg moving ahead, {7:F0} kg abeam",
                body.LengthMetres, body.BeamMetres, body.DraftMetres, body.FrontalSquareMetres, body.LateralSquareMetres, body.MassKg, body.SurgeKg, body.SwayKg);
        }

        sinceLog += seconds;
        if (sinceLog < storms.Config.Drift.LogSeconds) return;

        sinceLog = 0;
        var (vx, vz) = Velocity;
        var (wx, wz) = Water;
        if (Math.Max(Speed(vx, vz), Speed(wx, wz)) < StillMps) return;

        var (along, across) = HullShape.ToHull(vx - wx, vz - wz, entity.Pos.Yaw);
        storms.Log(entity, "drift: water {0:F2} m/s toward {1:F0}, she makes {2:F2} m/s toward {3:F0} over the ground, through the water {4:F2} ahead and {5:F2} abeam, stroke {6:F2} m/s",
            Speed(wx, wz), Toward(wx, wz), Speed(vx, vz), Toward(vx, vz), -along, across, Speed(row.X, row.Z));
    }

    private static double Speed(double x, double z) => Math.Sqrt(x * x + z * z);

    private static double Toward(double x, double z) => (Compass.FromDegrees(x, z) + 180) % 360;
}
