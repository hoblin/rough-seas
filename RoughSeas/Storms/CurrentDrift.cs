using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using RoughSeas.SeaAndWeather;
using RoughSeas.Vessels;

namespace RoughSeas.Storms;

/// <summary>
/// Carries a vessel with the water by its drag on her hull, where her physics runs: the server while nobody steers her,
/// the controlling client while someone does. Her motion holds the set, the velocity the water has given her, on top of
/// vanilla's own; <see cref="DriftPhysics"/> keeps vanilla's water drag and the rower's motion from erasing it.
/// </summary>
public class CurrentDrift(Entity entity) : EntityBehavior(entity)
{
    public const string Code = "roughseas:currentdrift";

    private const string SetKey = "roughseas:set";
    private const double TicksPerSecond = 60;

    private StormsModSystem storms = null!;
    private SeaWeatherSystem sea = null!;
    private JsonObject attributes = null!;
    private EntityBehaviorPassivePhysicsMultiBox? hullBoxes;
    private IBuoyant? buoyant;
    private IMountable? mountable;
    private HullForm form;
    private readonly object setLock = new();
    private double setX;
    private double setZ;
    private volatile Water? water;
    private volatile bool driving;
    private double turnPerSecond;
    private float sinceSync;
    private float sinceLog;

    private sealed record Water(double X, double Z, HullDrag Drag);

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
        buoyant = entity.SidedProperties.Behaviors.Find(behavior => behavior is IBuoyant) as IBuoyant;
        var box = entity.SelectionBox;
        form = HullForm.From(attributes, storms.Config.Drift, Math.Max(box.XSize, box.ZSize), Math.Min(box.XSize, box.ZSize));
    }

    /// <summary>The water's drag on her now, newtons east and south.</summary>
    public (double X, double Z) Pull()
    {
        if (water is not { } w) return default;
        lock (setLock) return w.Drag.Pull(w.X - setX, w.Z - setZ, entity.Pos.Yaw);
    }

    /// <summary>A mooring stops her set away from it, along the unit vector from the mooring to her.</summary>
    public void Hold(double outwardX, double outwardZ)
    {
        lock (setLock)
        {
            var outward = setX * outwardX + setZ * outwardZ;
            if (outward <= 0) return;
            setX -= outward * outwardX;
            setZ -= outward * outwardZ;
        }
    }

    /// <summary>While a mooring holds her, she turns about her bow under the water running past; let go, she stops turning.</summary>
    public void HeldAtBow(bool held, float deltaTime)
    {
        if (!held || water is not { } w || hullBoxes == null)
        {
            turnPerSecond = 0;
            return;
        }

        var pos = entity.Pos;
        lock (setLock) turnPerSecond = w.Drag.Swing(w.X - setX, w.Z - setZ, pos.Yaw, turnPerSecond, deltaTime);
        var yaw = (float)GameMath.Mod(pos.Yaw + turnPerSecond * deltaTime, GameMath.TWOPI);
        if (hullBoxes.AdjustCollisionBoxesToYaw(deltaTime, true, yaw)) pos.Yaw = yaw;
        else turnPerSecond = 0;
    }

    public override void OnGameTick(float deltaTime)
    {
        if (!entity.Alive) return;

        var client = entity.Api as ICoreClientAPI;
        var steered = mountable?.IsBeingControlled() == true;
        var drives = client == null ? !steered : mountable?.Controller == client.World.Player.Entity;
        if (client != null && !drives || !entity.Swimming)
        {
            Ashore();
            return;
        }

        using var _ = storms.Budget.Measure();
        var pos = entity.Pos;
        var current = sea.CurrentAt(pos.X, pos.Z);
        var wind = sea.Source.Wind(pos.X, pos.Z);
        var stokes = sea.Config.Currents.StokesDriftPerMps;
        var w = water = Drag() is { } drag ? new Water(current.X + wind.X * stokes, current.Z + wind.Z * stokes, drag) : null;
        if (w == null)
        {
            Ashore();
            return;
        }

        if (client != null && !driving) TakeHelm();
        driving = drives;
        if (steered && client == null)
        {
            lock (setLock) (setX, setZ) = Settled(w, deltaTime);
        }
        if (client == null) Sync(deltaTime);
        Report(w, steered, deltaTime);
    }

    internal void BeforeMove(Vec3d motion, float seconds, double damping)
    {
        if (!driving || water is not { } w) return;

        lock (setLock)
        {
            var (x, z) = Settled(w, seconds);
            motion.X += (x / damping - setX) / TicksPerSecond;
            motion.Z += (z / damping - setZ) / TicksPerSecond;
            (setX, setZ) = (x, z);
        }
    }

    internal void AfterMove(Vec3d motion)
    {
        if (!driving) return;

        lock (setLock)
        {
            if (motion.X == 0) setX = 0;
            if (motion.Z == 0) setZ = 0;
        }
    }

    internal void Rowed(Vec3d motion)
    {
        if (!driving) return;

        lock (setLock)
        {
            motion.X += setX / TicksPerSecond;
            motion.Z += setZ / TicksPerSecond;
        }
    }

    private HullDrag? Drag()
    {
        var kg = buoyant?.Aboard.Total ?? entity.Properties.Weight;
        var density = storms.Config.Waves.WaterDensity;
        var draft = buoyant is { HasDraft: true } ? buoyant.Afloat.DraftMetres : kg / (density * form.LengthMetres * form.BeamMetres);
        return draft > 0 && kg > 0 ? new HullDrag(form, draft, kg, density, storms.Config.Drift) : null;
    }

    private (double X, double Z) Settled(Water w, double seconds)
    {
        var (x, z) = w.Drag.Settle(w.X - setX, w.Z - setZ, entity.Pos.Yaw, seconds);
        return (w.X - x, w.Z - z);
    }

    private void Ashore()
    {
        driving = false;
        water = null;
        turnPerSecond = 0;
        lock (setLock) (setX, setZ) = (0, 0);
        if (entity.World.Side == EnumAppSide.Server && entity.WatchedAttributes.HasAttribute(SetKey))
        {
            entity.WatchedAttributes.RemoveAttribute(SetKey);
        }
    }

    private void TakeHelm()
    {
        var synced = entity.WatchedAttributes.GetTreeAttribute(SetKey);
        lock (setLock)
        {
            (setX, setZ) = (synced?.GetDouble("x") ?? 0, synced?.GetDouble("z") ?? 0);
            entity.Pos.Motion.X = setX / TicksPerSecond;
            entity.Pos.Motion.Z = setZ / TicksPerSecond;
        }
        storms.Log(entity, "takes the helm carried at {0:F2} m/s toward {1:F0}°", Math.Sqrt(setX * setX + setZ * setZ), Compass.TowardDegrees(setX, setZ));
    }

    private void Sync(float deltaTime)
    {
        if ((sinceSync += deltaTime) < storms.Config.Drift.SyncSeconds) return;

        sinceSync = 0;
        var tree = entity.WatchedAttributes.GetOrAddTreeAttribute(SetKey);
        lock (setLock)
        {
            tree.SetDouble("x", setX);
            tree.SetDouble("z", setZ);
        }
        entity.WatchedAttributes.MarkPathDirty(SetKey);
    }

    private void Report(Water w, bool steered, float deltaTime)
    {
        if ((sinceLog += deltaTime) < storms.Config.Drift.LogSeconds) return;

        sinceLog = 0;
        var (pullX, pullZ) = Pull();
        double x, z;
        lock (setLock) (x, z) = (setX, setZ);
        storms.Log(entity, "{0}: the water runs {1:F2} m/s toward {2:F0}°, carries her at {3:F2} m/s toward {4:F0}°, pulls {5:F0} N; {6:F0} kg aboard, heading {7:F0}°",
            steered ? "steered" : "adrift", Math.Sqrt(w.X * w.X + w.Z * w.Z), Compass.TowardDegrees(w.X, w.Z),
            Math.Sqrt(x * x + z * z), Compass.TowardDegrees(x, z), Math.Sqrt(pullX * pullX + pullZ * pullZ), w.Drag.Kilograms,
            Compass.TowardDegrees(Math.Sin(entity.Pos.Yaw), Math.Cos(entity.Pos.Yaw)));
    }
}
