using System;
using System.Linq;
using RoughSeas.SeaAndWeather;
using RoughSeas.Vessels;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Wind;

public class EntityBehaviorWindage(Entity entity) : EntityBehavior(entity), IWindOnHull
{
    public const string Code = "roughseas:windage";
    public const string TackedKey = "roughseas:tacked";

    private const string SailKey = "sailPosition";

    private WindModSystem wind = null!;
    private SeaWeatherSystem sea = null!;
    private IHull? hull;
    private VesselInWind body = null!;
    private RigRenderer? drawn;
    private string[] drawnGroups = [];
    private string[] steps = [];
    private RigTextures? textures;
    private IHullForm[] forms = [];
    private bool blown;
    private long loggedAt;

    public Sail? Sail => body.Sail;

    public bool Rigged => forms.All(form => form.Finished);

    public override string PropertyName() => Code;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        wind = entity.Api.ModLoader.GetModSystem<WindModSystem>();
        sea = entity.Api.ModLoader.GetModSystem<SeaWeatherSystem>();
        body = VesselInWind.From(attributes, wind.Config, wind.Polar);
        drawnGroups = attributes["drawn"].AsArray<string>()?.OfType<string>().ToArray() ?? [];
        steps = attributes["steps"].AsArray<string>()?.OfType<string>().ToArray() ?? [];
        textures = attributes["textures"].AsObject<RigTextures?>(null);
    }

    public override void AfterInitialized(bool onFirstSpawn)
    {
        hull = IHull.Of(entity);
        forms = entity.SidedProperties.Behaviors.OfType<IHullForm>().ToArray();
        if (entity.Api is ICoreClientAPI capi && Sail != null) drawn = new RigRenderer(capi, entity, this, textures!);
    }

    public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements)
    {
        if (drawn != null && Rigged) willDeleteElements = (willDeleteElements ?? []).Concat(drawnGroups).ToArray();
    }

    public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
    {
        if (mode != EnumInteractMode.Interact || entity.Api.Side != EnumAppSide.Server || Sail == null || !Rigged
            || byEntity is not EntityPlayer player || entity.GetBehavior<EntityBehaviorSelectionBoxes>() is not { } boxes) return;

        var step = Array.FindIndex(steps, code => boxes.IsAPCode(player.EntitySelection, code));
        if (step < 0 || step == entity.WatchedAttributes.GetInt(TackedKey)) return;

        entity.WatchedAttributes.SetInt(TackedKey, step);
        handled = EnumHandling.PreventSubsequent;
        wind.Log(entity, "{0} shunts her: her tack goes to the step at her {1}, her mast rakes toward it", player.GetName(), step == 0 ? "bow" : "stern");
    }

    public override void OnGameTick(float deltaTime)
    {
        if (drawn == null || hull == null) return;

        if (!blown && entity.IsRendered) Wind(hull.Velocity);
        blown = false;
        drawn.Trimmed();
    }

    public override void OnEntityDespawn(EntityDespawnData despawn)
    {
        drawn?.Dispose();
        drawn = null;
    }

    public void Hoist()
    {
        if (Sail == null) return;

        Sail.Rig.Tacked = entity.WatchedAttributes.GetInt(TackedKey);
        Sail.Hoist(entity.WatchedAttributes.GetInt(SailKey));
    }

    public WindLoad Wind((double X, double Z) velocity)
    {
        Hoist();
        blown = true;
        if (hull == null || !entity.Swimming) return default;

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
