using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using RoughSeas.SeaAndWeather;
using RoughSeas.Vessels;

namespace RoughSeas.Storms;

/// <summary>
/// Carries a vessel with the water by its drag on her hull, where her physics runs: the server while nobody steers her,
/// the controlling client while someone does. Her <see cref="HullSet"/> holds the velocity the water has given her, on
/// top of the way a rower makes through the water; <see cref="DriftPhysics"/> keeps it through vanilla's physics. While
/// she is steered the server works out the same set from the same drag, for whoever takes her next.
/// </summary>
public class EntityBehaviorCurrentDrift(Entity entity) : EntityBehavior(entity)
{
    public const string Code = "roughseas:currentdrift";

    private const string SetKey = "roughseas:set";
    private const double StillMps = 0.01;
    private const double SyncedMps = 0.001;

    private readonly HullSet set = new();
    private StormsModSystem storms = null!;
    private SeaWeatherSystem sea = null!;
    private EntityBehaviorPassivePhysicsMultiBox? hullBoxes;
    private EntityBehaviorPassivePhysics? physics;
    private IMountable? mountable;
    private IHullWay? way;
    private LiveDrag? ownDrag;
    private JsonObject attributes = null!;
    private volatile Water? water;
    private volatile bool moves;
    private double turnPerSecond;
    private float sinceSync;
    private float sinceLog;
    private float sinceSwingLog;
    private (double X, double Z) synced;

    private sealed record Water(double X, double Z, HullDrag? Drag);

    public override string PropertyName() => Code;

    /// <summary>What her motion holds of the water's set now, blocks per tick.</summary>
    public (double X, double Z) Held => set.Held;

    /// <summary>What her swing about a held bow adds to her motion, blocks per tick: a mooring leaves it alone.</summary>
    public (double X, double Z) PivotMotion => set.PivotMotion;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        storms = entity.Api.ModLoader.GetModSystem<StormsModSystem>();
        sea = entity.Api.ModLoader.GetModSystem<SeaWeatherSystem>();
        mountable = entity.GetInterface<IMountable>();
        this.attributes = attributes;
    }

    public override void AfterInitialized(bool onFirstSpawn)
    {
        hullBoxes = entity.GetBehavior<EntityBehaviorPassivePhysicsMultiBox>();
        physics = entity.GetBehavior<EntityBehaviorPassivePhysics>();
        way = entity.SidedProperties.Behaviors.OfType<IHullWay>().FirstOrDefault();
        if (way == null && IHull.Of(entity) is { } hull) ownDrag = new LiveDrag(entity, hull, attributes, entity.Api.ModLoader.GetModSystem<VesselsModSystem>());
    }

    public HullDrag? Drag => water?.Drag;

    /// <summary>The water's drag on her now, newtons in world axes.</summary>
    public (double X, double Z) Pull() =>
        water is { Drag: { } drag } w ? set.Pull(drag, w.X, w.Z, entity.Pos.Yaw) : default;

    /// <summary>A mooring stops her set away from it, along the unit vector from the mooring to her.</summary>
    public void Hold(double outwardX, double outwardZ) => set.Hold(outwardX, outwardZ);

    public double Slow(double outwardX, double outwardZ, double metresPerSecond) => set.Slow(outwardX, outwardZ, metresPerSecond);

    /// <summary>
    /// While a mooring holds her she turns about her bow under the water running past: her origin moves as the turn
    /// takes it, so the bow stays where the rope has it. Let go, she stops turning.
    /// </summary>
    public void HeldAtBow(bool held, float deltaTime)
    {
        if (!held || water is not { Drag: { } drag } w || hullBoxes == null || IHull.Of(entity) is not { } hull || deltaTime <= 0)
        {
            Release();
            return;
        }

        var pos = entity.Pos;
        var yaw = pos.Yaw;
        turnPerSecond = set.Swing(drag, w.X, w.Z, yaw, turnPerSecond, deltaTime);
        var turned = (float)GameMath.Mod(yaw + turnPerSecond * deltaTime, GameMath.TWOPI);
        if (!hullBoxes.AdjustCollisionBoxesToYaw(deltaTime, true, turned))
        {
            Release();
            if ((sinceSwingLog += deltaTime) >= storms.Config.Drift.LogSeconds)
            {
                sinceSwingLog = 0;
                storms.Log(entity, "can't swing: something stands where she would turn");
            }
            return;
        }

        pos.Yaw = turned;
        var (bowX, bowZ) = hull.Shape.Bow(yaw);
        var (nextX, nextZ) = hull.Shape.Bow(turned);
        set.Pivot((bowX - nextX) / deltaTime, (bowZ - nextZ) / deltaTime);
        LogSwing(w, drag, deltaTime);
    }

    [ReliesOn("Vintagestory.GameContent.EntityBoat", "OnGameTick", "runs updateBoatAngleAndMotion before base.OnGameTick, so this behaviour's add to the motion survives it")]
    public override void OnGameTick(float deltaTime)
    {
        if (!entity.Alive) return;

        var client = entity.Api as ICoreClientAPI;
        var steered = mountable?.IsBeingControlled() == true;
        var ours = client == null ? !steered : mountable?.Controller == client.World.Player?.Entity;
        if ((client != null && !ours) || !entity.Swimming)
        {
            Ashore();
            return;
        }

        using var _ = storms.Budget.Measure();
        var pos = entity.Pos;
        var current = Mechanic.Currents.On(entity.World) ? sea.CurrentAt(pos.X, pos.Z) : default;
        var w = water = new Water(current.X, current.Z, way?.WaterDrag ?? ownDrag?.Now);
        if (w.Drag is { } drag) set.Settle(drag, w.X, w.Z, pos.Yaw, deltaTime);
        else set.Relax(w.X, w.Z, 1 - WaterDrag.Kept(physics?.WaterDragValue ?? 1, deltaTime));

        if (ours && !moves) TakeOver();
        else if (!ours && moves) GiveOver();
        moves = ours;
        if (client == null) Sync(deltaTime);
        Report(w, steered, deltaTime);
    }

    internal void BeforeMove(Vec3d motion, float seconds, double drag, bool wet, bool swimming, double stepFactor)
    {
        if (!moves) return;

        // a driving hull has this drag undone along the water by Rowing.Swum already, whichever patch goes first
        var kept = wet && way is { Driving: true } ? 1 : WaterDrag.Kept(drag, seconds);
        set.Keep(motion, kept, stepFactor, swimming);
    }

    internal void AfterMove(Vec3d motion)
    {
        if (moves) set.Collided(motion);
    }

    internal void Rowed(Vec3d motion) => set.Rowed(motion, moves);

    /// <summary>For a bench switch: she stops being carried here and now.</summary>
    public void Stopped(Vec3d motion) => set.Drop(motion);

    private void TakeOver()
    {
        var tree = entity.WatchedAttributes.GetTreeAttribute(SetKey);
        if (tree == null) return;

        var (x, z) = (tree.GetDouble("x"), tree.GetDouble("z"));
        set.Take(x, z, entity.Pos.Motion);
        storms.Log(entity, "{0}: carried at {1:F2} m/s toward {2:F0}°",
            entity.Api.Side == EnumAppSide.Client ? "steered from this client, her physics runs here" : "nobody steers her, the server moves her",
            Speed(x, z), Compass.TowardDegrees(x, z));
    }

    private void GiveOver()
    {
        var (x, z) = set.Velocity;
        storms.Log(entity, "steered: her helm's client moves her, carried at {0:F2} m/s toward {1:F0}°", Speed(x, z), Compass.TowardDegrees(x, z));
        Release();
    }

    private void Release()
    {
        turnPerSecond = 0;
        set.Pivot(0, 0);
    }

    private void Ashore()
    {
        if (moves && entity.Api.Side == EnumAppSide.Client) storms.Log(entity, "no longer steered from this client");
        moves = false;
        water = null;
        turnPerSecond = 0;
        set.Clear();
        synced = default;
        if (entity.Api.Side == EnumAppSide.Server && entity.WatchedAttributes.HasAttribute(SetKey))
        {
            entity.WatchedAttributes.RemoveAttribute(SetKey);
        }
    }

    private void Sync(float deltaTime)
    {
        if ((sinceSync += deltaTime) < storms.Config.Drift.SyncSeconds) return;

        sinceSync = 0;
        var now = set.Velocity;
        if (Math.Abs(now.X - synced.X) < SyncedMps && Math.Abs(now.Z - synced.Z) < SyncedMps) return;

        synced = now;
        var tree = entity.WatchedAttributes.GetOrAddTreeAttribute(SetKey);
        tree.SetDouble("x", now.X);
        tree.SetDouble("z", now.Z);
        entity.WatchedAttributes.MarkPathDirty(SetKey);
    }

    private void Report(Water w, bool steered, float deltaTime)
    {
        if ((sinceLog += deltaTime) < storms.Config.Drift.LogSeconds) return;

        sinceLog = 0;
        var (x, z) = set.Velocity;
        if (Math.Max(Speed(x, z), Speed(w.X, w.Z)) < StillMps) return;

        var (pullX, pullZ) = Pull();
        storms.Log(entity, "{0}: the water runs {1:F2} m/s toward {2:F0}°, carries her at {3:F2} m/s toward {4:F0}°, pulls {5:F0} N; {6:F0} kg aboard, heading {7:F0}°",
            steered ? "steered" : "adrift", Speed(w.X, w.Z), Compass.TowardDegrees(w.X, w.Z), Speed(x, z), Compass.TowardDegrees(x, z),
            Speed(pullX, pullZ), w.Drag?.Kilograms ?? 0, Heading());
    }

    private void LogSwing(Water w, HullDrag drag, float deltaTime)
    {
        if ((sinceSwingLog += deltaTime) < storms.Config.Drift.LogSeconds) return;

        sinceSwingLog = 0;
        var pos = entity.Pos;
        var (x, z) = set.Velocity;
        var (along, across) = HullShape.ToHull(w.X - x, w.Z - z, pos.Yaw);
        if (Speed(along, across) < StillMps) return;

        var (pullX, pullZ) = drag.Pull(w.X - x, w.Z - z, pos.Yaw);
        storms.Log(entity, "swings at {0:F1}°/s, heading {1:F0}°, {2:F0}° off the stream with her stern {3}, the current pulling {4:F0} N",
            turnPerSecond * GameMath.RAD2DEG, Heading(), Math.Atan2(Math.Abs(across), Math.Abs(along)) * GameMath.RAD2DEG,
            along >= 0 ? "downstream" : "upstream", Speed(pullX, pullZ));
    }

    private double Heading() => Compass.TowardDegrees(Math.Sin(entity.Pos.Yaw), Math.Cos(entity.Pos.Yaw));

    private static double Speed(double x, double z) => Math.Sqrt(x * x + z * z);
}
