using System;
using System.Collections.Generic;
using System.Linq;
using RoughSeas.SeaAndWeather;
using RoughSeas.Vessels;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace RoughSeas.Wind;

public class EntityBehaviorWindage(Entity entity) : EntityBehavior(entity), IWindOnHull
{
    public const string Code = "roughseas:windage";
    public const string TackedKey = "roughseas:tacked";

    private const string SailKey = "sailPosition";
    private const string ShapeKey = "roughseas:rig";

    private WindModSystem wind = null!;
    private SeaWeatherSystem sea = null!;
    private IHull? hull;
    private VesselInWind body = null!;
    private RigRenderer? drawn;
    private string[] drawnGroups = [];
    private string[] steps = [];
    private string cloth = "";
    private Dictionary<string, string> parts = new();
    private IHullForm[] forms = [];
    private bool blown;
    private byte[]? published;
    private long loggedAt;

    public Sail? Sail => body.Sail;

    public bool Rigged => forms.All(form => form.Finished);

    public override string PropertyName() => Code;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        wind = entity.Api.ModLoader.GetModSystem<WindModSystem>();
        sea = entity.Api.ModLoader.GetModSystem<SeaWeatherSystem>();
        body = VesselInWind.From(attributes, wind.Config);
        drawnGroups = attributes["drawn"].AsArray<string>()?.OfType<string>().ToArray() ?? [];
        steps = attributes["steps"].AsArray<string>()?.OfType<string>().ToArray() ?? [];
        parts = attributes["parts"].AsObject<Dictionary<string, string>?>(null) ?? new();
        cloth = attributes["cloth"].AsString()
            ?? (Sail == null ? "" : throw new InvalidOperationException($"{entity.Code}'s sail names no cloth texture: roughseas:windage needs cloth with a sail"));
    }

    public override void AfterInitialized(bool onFirstSpawn)
    {
        hull = IHull.Of(entity);
        forms = entity.SidedProperties.Behaviors.OfType<IHullForm>().ToArray();
        if (Sail != null && entity.WatchedAttributes.GetBytes(ShapeKey) is { } saved) Sail.Show(SerializerUtil.Deserialize<RigShape>(saved));
        if (entity.Api is ICoreClientAPI capi && Sail != null) drawn = new RigRenderer(capi, entity, this, cloth, parts);
    }

    public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements)
    {
        if (drawn == null || !Rigged) return;

        drawn.Shaped(entityShape.Clone());
        willDeleteElements = (willDeleteElements ?? []).Concat(drawnGroups).ToArray();
    }

    public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
    {
        if (mode != EnumInteractMode.Interact || entity.Api.Side != EnumAppSide.Server || Sail == null || !Rigged
            || byEntity is not EntityPlayer player || entity.GetBehavior<EntityBehaviorSelectionBoxes>() is not { } boxes) return;

        var step = Array.FindIndex(steps, code => boxes.IsAPCode(player.EntitySelection, code));
        if (step < 0 || step == entity.WatchedAttributes.GetInt(TackedKey) || Sail.Shunting) return;

        entity.WatchedAttributes.SetInt(TackedKey, step);
        handled = EnumHandling.PreventSubsequent;
        wind.Log(entity, "{0} shunts her: her tack goes to the step at her {1}, her mast rakes toward it", player.GetName(), step == 0 ? "bow" : "stern");
    }

    public override void OnGameTick(float deltaTime)
    {
        if (drawn == null || hull == null) return;

        var helm = entity.GetInterface<IMountable>()?.Controller;
        if (!blown && Sail != null && (helm == null || helm != ((ICoreClientAPI)entity.Api).World.Player.Entity)
            && entity.WatchedAttributes.GetBytes(ShapeKey) is { } bytes) Sail.Show(SerializerUtil.Deserialize<RigShape>(bytes));
        blown = false;
    }

    /// <summary>Server only: the rig as the client that steers her moves it.</summary>
    public void Reported(IServerPlayer player, RigShape shape)
    {
        if (Sail == null || entity.GetInterface<IMountable>()?.Controller != player.Entity) return;

        Sail.Show(shape);
        Published(SerializerUtil.Serialize(shape));
    }

    private void Published(byte[] bytes)
    {
        entity.WatchedAttributes.SetBytes(ShapeKey, bytes);
        entity.WatchedAttributes.MarkPathDirty(ShapeKey);
    }

    private bool Changed(RigShape shape)
    {
        var bytes = SerializerUtil.Serialize(shape);
        if (published != null && bytes.AsSpan().SequenceEqual(published)) return false;

        published = bytes;
        return true;
    }

    public override void OnEntityDespawn(EntityDespawnData despawn)
    {
        drawn?.Dispose();
        drawn = null;
    }

    public void Hoist()
    {
        if (Sail == null) return;

        Sail.Hoist(entity.WatchedAttributes.GetInt(SailKey), entity.WatchedAttributes.GetInt(TackedKey));
    }

    public WindLoad Wind((double X, double Z) velocity, double seconds)
    {
        var (blowing, load) = Blow(velocity, seconds);
        if (load is { } blew) Note(blowing, velocity, blew);
        if (Sail?.Shape(entity.EntityId) is { } shape && Changed(shape))
        {
            if (entity.Api.Side == EnumAppSide.Server) Published(published!);
            else wind.Report(shape);
        }
        return load ?? default;
    }

    private ((double X, double Z) Blowing, WindLoad? Load) Blow((double X, double Z) velocity, double seconds)
    {
        var (was, shunting) = (Sail?.State, Sail?.Shunting);
        Hoist();
        blown = true;
        if (hull == null || !entity.Swimming) return (default, null);

        var blowing = sea.Map.Wind(entity.Pos.X, entity.Pos.Z);
        var load = body.Load(blowing, velocity, entity.Pos.Yaw, hull.Shape.KeelMetres + hull.Afloat.DraftMetres, seconds);
        if (Sail is { } shunted && shunted.Shunting != shunting)
            wind.Log(entity, "{0} her shunt to her {1} end: {2}", shunted.Shunting ? "begins" : "ends", entity.WatchedAttributes.GetInt(TackedKey) == 0 ? "bow" : "stern",
                shunted.Rig.End.Describe(shunted.State));
        if (Sail is { } sail && was != null && sail.Rig.End.Marks(sail.State).Zip(sail.Rig.End.Marks(was)).Max(pair => (pair.First - pair.Second).Length) is var moved
            && moved > wind.Config.JumpMetresPerSecond * seconds)
            wind.Log(entity, "her rig moved {0:F2} m in one {1:F3} s tick: {2}", moved, seconds, sail.Rig.End.Describe(sail.State));
        return (blowing, load);
    }

    private void Note((double X, double Z) blowing, (double X, double Z) velocity, WindLoad load)
    {
        var now = entity.World.ElapsedMilliseconds;
        if (now - loggedAt < wind.Config.LogSeconds * 1000) return;

        loggedAt = now;
        var (along, across) = HullShape.ToHull(blowing.X - velocity.X, blowing.Z - velocity.Z, entity.Pos.Yaw);
        var air = new Airflow(along, across);
        var (pushAlong, pushAcross) = HullShape.ToHull(load.X, load.Z, entity.Pos.Yaw);
        wind.Log(entity, "the wind {0:F1} m/s toward {1:F0}°, {2:F1} m/s apparent {3:F0}° off her model's bow from {4}; {5}: {6:F0} N toward her model's bow, {7:F0} N to {8}, {9:F0} N·m; she makes {10:F2} m/s",
            Speed(blowing), Compass.TowardDegrees(blowing.X, blowing.Z), air.Speed, air.FromBow * GameMath.RAD2DEG, air.Leeward > 0 ? "port" : "starboard",
            Sail is { Pressure: > 0 } sail ? $"sail {(sail.Aback ? "aback" : "drawing")} on tack {sail.Rig.Tacked}" : "bare",
            -pushAlong, Math.Abs(pushAcross), pushAcross > 0 ? "starboard" : "port", load.NewtonMetres, Speed(velocity));
        if (Sail is { Pressure: > 0, Load: { } rig })
            wind.Log(entity, "her rig holds the sail: sheet {0:F0} N, halyard {1:F0} N, {2}", rig.SheetNewtons, rig.HalyardNewtons,
                string.Join(", ", rig.Stays.Select(stay => $"{stay.Line} {stay.Newtons:F0} N")));
        if (Sail is { SquareMetres: > 0 } set) wind.Log(entity, "her rig stands: {0}", set.Rig.End.Describe(set.State));
    }

    private static double Speed((double X, double Z) v) => Math.Sqrt(v.X * v.X + v.Z * v.Z);
}
