using System;
using System.Linq;
using RoughSeas.SeaAndWeather;
using RoughSeas.Vessels;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace RoughSeas.Wind;

public class EntityBehaviorWindage(Entity entity) : EntityBehavior(entity), IWindOnHull
{
    public const string Code = "roughseas:windage";
    public const string SailWindKey = "sailWind";

    private WindModSystem wind = null!;
    private SeaWeatherSystem sea = null!;
    private JsonObject attributes = null!;
    private IHull? hull;
    private IRigged? rigged;
    private VesselInWind body = null!;
    private long loggedAt;

    public override string PropertyName() => Code;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        wind = entity.Api.ModLoader.GetModSystem<WindModSystem>();
        sea = entity.Api.ModLoader.GetModSystem<SeaWeatherSystem>();
        this.attributes = attributes;
    }

    public override void AfterInitialized(bool onFirstSpawn)
    {
        hull = IHull.Of(entity);
        rigged = entity.SidedProperties.Behaviors.OfType<IRigged>().FirstOrDefault();
        body = VesselInWind.From(attributes, wind.Config, rigged?.Aloft);
    }

    public (double X, double Z) Blowing => sea.Map.Wind(entity.Pos.X, entity.Pos.Z);

    public WindLoad Wind((double X, double Z) velocity, double seconds)
    {
        rigged?.Trim();
        var load = Blow(velocity, seconds);
        rigged?.Trimmed(seconds);
        return load;
    }

    private WindLoad Blow((double X, double Z) velocity, double seconds)
    {
        if (hull == null || !entity.Swimming) return default;

        var blowing = Blowing;
        var load = body.Load(blowing, velocity, entity.Pos.Yaw, hull.Shape.KeelMetres + hull.Afloat.DraftMetres, seconds,
            entity.World.Config.GetString(SailWindKey).ToFloat(1));
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
        wind.Log(entity, "the wind {0:F1} m/s toward {1:F0}°, {2:F1} m/s apparent {3:F0}° off her model's bow from {4}: {5:F0} N toward her model's bow, {6:F0} N to {7}, {8:F0} N·m; she makes {9:F2} m/s",
            Speed(blowing), Compass.TowardDegrees(blowing.X, blowing.Z), air.Speed, air.FromBow * GameMath.RAD2DEG, air.Leeward > 0 ? "port" : "starboard",
            -pushAlong, Math.Abs(pushAcross), pushAcross > 0 ? "starboard" : "port", load.NewtonMetres, Speed(velocity));
    }

    private static double Speed((double X, double Z) v) => Math.Sqrt(v.X * v.X + v.Z * v.Z);
}
