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
    private EntityBehaviorSeaForces? forces;
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
    private double madeSeconds;
    private double swayed;
    private (double X, double Z) madeFrom;
    private (double X, double Z) given;
    private (double X, double Z) wayMade;

    private sealed record Water(WaterOnHull On, HullDrag? Drag, (double X, double Z) Wash = default);

    public override string PropertyName() => Code;

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
        forces = entity.GetBehavior<EntityBehaviorSeaForces>();
        way = entity.SidedProperties.Behaviors.OfType<IHullWay>().FirstOrDefault();
        if (way == null && IHull.Of(entity) is { } hull) ownDrag = new LiveDrag(entity, hull, attributes, entity.Api.ModLoader.GetModSystem<VesselsModSystem>());
    }

    public HullDrag? Drag => water?.Drag;

    /// <summary>The water's drag on her now, newtons in world axes.</summary>
    public (double X, double Z) Pull() =>
        water is { Drag: { } drag } w ? set.Pull(drag, w.On, entity.Pos.Yaw) : default;

    /// <summary>Everything the water does to her now, its drag and its push, newtons in world axes.</summary>
    public (double X, double Z) Load()
    {
        if (water is not { Drag: { } drag } w) return default;
        var (pullX, pullZ) = set.Pull(drag, w.On, entity.Pos.Yaw);
        var (pushX, pushZ) = drag.Newtons(w.Wash.X, w.Wash.Z, entity.Pos.Yaw);
        return (pullX + pushX, pullZ + pushZ);
    }

    /// <summary>Her velocity now, m/s in world axes: what the water has given her and her own way through it.</summary>
    public (double X, double Z) Velocity
    {
        get
        {
            var (x, z) = set.Velocity;
            var (wayX, wayZ) = HullShape.ToWorld(WayAlong(), 0, entity.Pos.Yaw);
            return (x + wayX, z + wayZ);
        }
    }

    /// <summary>A force on her for <paramref name="seconds"/>, newtons in world axes, taken by her mass and the water she drags.</summary>
    public void Impulse(double newtonsX, double newtonsZ, double seconds)
    {
        if (water?.Drag is not { } drag) return;
        var (x, z) = drag.Acceleration(newtonsX, newtonsZ, entity.Pos.Yaw);
        set.Push(x * seconds, z * seconds);
    }

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
        turnPerSecond = set.Swing(drag, w.On, yaw, turnPerSecond, deltaTime);
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
        var current = Current();
        var waves = entity is EntityBoat boat && forces != null ? forces.Ride.Water(boat, deltaTime) : default;
        var on = waves with { CurrentX = current.X, CurrentZ = current.Z, WayAlong = WayAlong() };
        var drag = way?.WaterDrag ?? ownDrag?.Now;
        var wash = drag != null ? set.Settle(drag, on, pos.Yaw, deltaTime) : set.Relax(on, pos.Yaw, 1 - WaterDrag.Kept(physics?.WaterDragValue ?? 1, deltaTime), deltaTime);
        var w = water = new Water(on, drag, wash);

        if (ours && !moves) TakeOver();
        else if (!ours && moves) GiveOver();
        moves = ours;
        if (client == null) Sync(deltaTime);
        Report(w, steered, deltaTime);
    }

    private (double X, double Z) Current()
    {
        if (!Mechanic.Currents.On(entity.World)) return default;
        var current = sea.CurrentAt(entity.Pos.X, entity.Pos.Z);
        return (current.X, current.Z);
    }

    private double WayAlong() => entity is EntityBoat { ForwardSpeed: not 0 } boat ? -VesselDraught.Realized(boat.ForwardSpeed) : 0;

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
        (madeSeconds, given, wayMade) = (0, default, default);
        if (entity.Api.Side == EnumAppSide.Server && entity.WatchedAttributes.HasAttribute(SetKey))
        {
            entity.WatchedAttributes.RemoveAttribute(SetKey);
        }
    }

    private void Sync(float deltaTime)
    {
        if ((sinceSync += deltaTime) < storms.Config.Drift.SyncSeconds) return;

        sinceSync = 0;
        var now = set.Own;
        if (Math.Abs(now.X - synced.X) < SyncedMps && Math.Abs(now.Z - synced.Z) < SyncedMps) return;

        synced = now;
        var tree = entity.WatchedAttributes.GetOrAddTreeAttribute(SetKey);
        tree.SetDouble("x", now.X);
        tree.SetDouble("z", now.Z);
        entity.WatchedAttributes.MarkPathDirty(SetKey);
    }

    private void Report(Water w, bool steered, float deltaTime)
    {
        var pos = entity.Pos;
        var (x, z) = set.Velocity;
        if (moves)
        {
            if (madeSeconds == 0) madeFrom = (pos.X, pos.Z);
            var (wayX, wayZ) = HullShape.ToWorld(w.On.WayAlong, 0, pos.Yaw);
            madeSeconds += deltaTime;
            swayed = Math.Max(swayed, Speed(x, z));
            given = (given.X + x * deltaTime, given.Z + z * deltaTime);
            wayMade = (wayMade.X + wayX * deltaTime, wayMade.Z + wayZ * deltaTime);
        }
        if ((sinceLog += deltaTime) < storms.Config.Drift.LogSeconds) return;

        sinceLog = 0;
        if (Math.Max(Speed(x, z), Speed(w.On.X, w.On.Z)) < StillMps) return;

        var (pullX, pullZ) = Pull();
        storms.Log(entity, "{0}: the current runs {1:F2} m/s toward {2:F0}°, the water with the waves {3:F2} m/s toward {4:F0}°, it carries her at {5:F2} m/s toward {6:F0}°, drags {7:F0} N; {8:F0} kg aboard, heading {9:F0}°",
            steered ? "steered" : "adrift", Speed(w.On.CurrentX, w.On.CurrentZ), Compass.TowardDegrees(w.On.CurrentX, w.On.CurrentZ), Speed(w.On.X, w.On.Z),
            Compass.TowardDegrees(w.On.X, w.On.Z), Speed(x, z), Compass.TowardDegrees(x, z), Speed(pullX, pullZ), w.Drag?.Kilograms ?? 0, Heading());
        if (madeSeconds <= 0) return;

        var (acrossMade, acrossGiven, acrossWay) = (Across(pos.X - madeFrom.X, pos.Z - madeFrom.Z), Across(given.X, given.Z), Across(wayMade.X, wayMade.Z));
        storms.Log(entity, "made good over {0:F0} s: {1:F3} {2:F3} m/s over the ground, {3:F3} {4:F3} m/s given her by the water, {5:F3} {6:F3} m/s her own way; across her heading {7:F3} m/s made of {8:F3} given and {9:F3} her way; the water swayed her up to {10:F2} m/s",
            madeSeconds, (pos.X - madeFrom.X) / madeSeconds, (pos.Z - madeFrom.Z) / madeSeconds, given.X / madeSeconds, given.Z / madeSeconds,
            wayMade.X / madeSeconds, wayMade.Z / madeSeconds, acrossMade / madeSeconds, acrossGiven / madeSeconds, acrossWay / madeSeconds, swayed);
        swayed = 0;
    }

    private double Across(double x, double z) => HullShape.ToHull(x, z, entity.Pos.Yaw).Across;

    private void LogSwing(Water w, HullDrag drag, float deltaTime)
    {
        if ((sinceSwingLog += deltaTime) < storms.Config.Drift.LogSeconds) return;

        sinceSwingLog = 0;
        var pos = entity.Pos;
        var (x, z) = set.Own;
        var (along, across) = HullShape.ToHull(w.On.CurrentX - x, w.On.CurrentZ - z, pos.Yaw);
        if (Speed(along, across) < StillMps) return;

        var (pullX, pullZ) = set.Pull(drag, w.On, pos.Yaw);
        storms.Log(entity, "swings at {0:F1}°/s, heading {1:F0}°, {2:F0}° off the stream with her stern {3}, the current pulling {4:F0} N",
            turnPerSecond * GameMath.RAD2DEG, Heading(), Math.Atan2(Math.Abs(across), Math.Abs(along)) * GameMath.RAD2DEG,
            along >= 0 ? "downstream" : "upstream", Speed(pullX, pullZ));
    }

    private double Heading() => Compass.TowardDegrees(Math.Sin(entity.Pos.Yaw), Math.Cos(entity.Pos.Yaw));

    private static double Speed(double x, double z) => Math.Sqrt(x * x + z * z);
}
