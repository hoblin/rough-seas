using System;
using RoughSeas.SeaAndWeather;
using RoughSeas.Vessels;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace RoughSeas.Wind;

public class EntityBehaviorWindage(Entity entity) : EntityBehavior(entity), IWindOnHull
{
    public const string Code = "roughseas:windage";

    private const string SailKey = "sailPosition";

    private WindModSystem wind = null!;
    private SeaWeatherSystem sea = null!;
    private IHull? hull;
    private VesselInWind body = null!;
    private SailRenderer? drawn;
    private string trimAnimation = "";
    private long loggedAt;

    public Sail? Sail => body.Sail;

    public override string PropertyName() => Code;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        wind = entity.Api.ModLoader.GetModSystem<WindModSystem>();
        sea = entity.Api.ModLoader.GetModSystem<SeaWeatherSystem>();
        body = VesselInWind.From(attributes, wind.Config, wind.Polar);
        trimAnimation = attributes["sail"]["trimAnimation"].AsString("");
    }

    public override void AfterInitialized(bool onFirstSpawn)
    {
        hull = IHull.Of(entity);
        if (entity.Api is ICoreClientAPI capi && Sail != null) drawn = new SailRenderer(capi, entity, Sail, trimAnimation);
    }

    public override void OnGameTick(float deltaTime)
    {
        if (drawn == null || hull == null) return;

        Wind(hull.Velocity);
        drawn.Trimmed();
    }

    public override void OnEntityDespawn(EntityDespawnData despawn)
    {
        drawn?.Dispose();
        drawn = null;
    }

    public WindLoad Wind((double X, double Z) velocity)
    {
        if (hull == null || !entity.Swimming) return default;

        Sail?.Set(entity.WatchedAttributes.GetInt(SailKey));
        var blowing = sea.Map.Wind(entity.Pos.X, entity.Pos.Z);
        var load = body.Load(blowing, velocity, entity.Pos.Yaw, hull.Shape.KeelMetres + hull.Afloat.DraftMetres);
        Note(blowing, velocity, load);
        return load;
    }

    private void Note((double X, double Z) blowing, (double X, double Z) velocity, WindLoad load)
    {
        var now = entity.World.ElapsedMilliseconds;
        if (now - loggedAt < wind.Config.LogSeconds * 1000) return;

        loggedAt = now;
        var (along, across) = HullShape.ToHull(blowing.X - velocity.X, blowing.Z - velocity.Z, entity.Pos.Yaw);
        var air = new Airflow(along, across);
        var (pushAlong, pushAcross) = HullShape.ToHull(load.X, load.Z, entity.Pos.Yaw);
        wind.Log(entity, "the wind {0:F1} m/s toward {1:F0}°, {2:F1} m/s apparent {3:F0}° off her bow from {4}; {5}: {6:F0} N ahead, {7:F0} N to {8}, {9:F0} N·m; she makes {10:F2} m/s",
            Speed(blowing), Compass.TowardDegrees(blowing.X, blowing.Z), air.Speed, air.FromBow * GameMath.RAD2DEG, air.Leeward > 0 ? "port" : "starboard",
            Sail is { Pressure: > 0 } sail ? $"sail drawing at {sail.Trim * GameMath.RAD2DEG:F0}°" : "bare",
            -pushAlong, Math.Abs(pushAcross), pushAcross > 0 ? "starboard" : "port", load.NewtonMetres, Speed(velocity));
    }

    private static double Speed((double X, double Z) v) => Math.Sqrt(v.X * v.X + v.Z * v.Z);
}
