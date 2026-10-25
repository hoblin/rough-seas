using System;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using RoughSeas.SeaAndWeather;
using RoughSeas.Vessels;

namespace RoughSeas.Storms;

public class EntityBehaviorBilge(Entity entity) : EntityBehavior(entity), IVesselHold
{
    public const string Code = "roughseas:bilge";

    private const string TreeKey = "roughseas:bilge";
    private const int DataVersion = 1;

    private StormsModSystem storms = null!;
    private ISeaWeather sea = null!;
    private SeaClock.Watch watch = null!;
    private BilgeRenderer? water;
    private IHullForm? form;
    private double holdLitres;
    private double deckSquareMetres;
    private double drainLitresPerHour;
    private double rained;
    private float sinceUpdate;

    public double CapacityLitres => holdLitres * Hollowed;

    public double UncutLitres => holdLitres - CapacityLitres;

    private double Hollowed => form?.Hollowed ?? 1;

    public double Litres
    {
        get => Tree?.GetDouble("litres") ?? 0;
        private set
        {
            var litres = Math.Clamp(value, 0, CapacityLitres);
            if (litres == Litres) return;

            Tree.SetDouble("litres", litres);
            entity.WatchedAttributes.MarkPathDirty(TreeKey);
        }
    }

    public double Fill => CapacityLitres > 0 ? Litres / CapacityLitres : 0;

    public bool Swamped => CapacityLitres > 0 && Litres >= CapacityLitres;

    private ITreeAttribute Tree => entity.WatchedAttributes.GetTreeAttribute(TreeKey);

    public override string PropertyName() => Code;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        storms = entity.Api.ModLoader.GetModSystem<StormsModSystem>();
        sea = entity.Api.ModLoader.GetModSystem<SeaWeatherSystem>();
        watch = sea.Clock.StartWatch();
        holdLitres = attributes["capacityLitres"].AsDouble();
        deckSquareMetres = attributes["deckSquareMetres"].AsDouble();
        drainLitresPerHour = attributes["drainLitresPerHour"].AsDouble();
        if (entity.Api is ICoreClientAPI capi)
        {
            water = new BilgeRenderer(capi, entity);
            entity.WatchedAttributes.RegisterModifiedListener(TreeKey, ShowWater);
            return;
        }

        if (Tree == null)
        {
            var tree = new TreeAttribute();
            tree.SetInt("version", DataVersion);
            tree.SetDouble("litres", 0);
            entity.WatchedAttributes.SetAttribute(TreeKey, tree);
        }
    }

    public override void AfterInitialized(bool onFirstSpawn)
    {
        form = entity.SidedProperties.Behaviors.OfType<IHullForm>().FirstOrDefault();
        if (form != null) form.Reformed += ShowWater;
    }

    public void Ship(double litres, string cause)
    {
        var wasSwamped = Swamped;
        Litres += litres;
        if (Swamped && !wasSwamped) storms.Log(entity, "swamped {0}: {1:F0} L aboard", cause, Litres);
    }

    public double Bail(double litres)
    {
        var bailed = Math.Min(litres, Litres);
        Litres -= bailed;
        storms.Log(entity, "bailed {0:F1} L: bilge {1:F0} of {2:F0} L", bailed, Litres, CapacityLitres);
        return bailed;
    }

    public override void OnGameTick(float deltaTime)
    {
        if (entity.World.Side != EnumAppSide.Server || CapacityLitres <= 0 || !entity.Alive) return;
        sinceUpdate += deltaTime;
        if (sinceUpdate < storms.Config.TickSeconds) return;

        using var _ = storms.Budget.Measure();
        var hours = watch.Lap().Hours;
        sinceUpdate = 0;
        var inflow = sea.DangerousOceans ? Rainfall() * storms.Config.Bilge.RainMillimetresPerHour * deckSquareMetres * hours : 0;
        Ship(inflow, "by rain");
        Litres -= drainLitresPerHour * hours;
        if ((rained += inflow) < 1) return;

        storms.Log(entity, "rain shipped {0:F1} L: bilge {1:F0} of {2:F0} L", rained, Litres, CapacityLitres);
        rained = 0;
    }

    public override void OnTesselated() => ShowWater();

    public override void OnEntityDespawn(EntityDespawnData despawn)
    {
        water?.Dispose();
        if (form != null) form.Reformed -= ShowWater;
        base.OnEntityDespawn(despawn);
    }

    public override void GetInfoText(StringBuilder infotext)
    {
        var litres = Litres;
        if (litres >= 1) infotext.AppendLine(Lang.Get("roughseas:bilge-water", Math.Round(litres), Math.Round(CapacityLitres)));
        base.GetInfoText(infotext);
    }

    // A hold still being cut is the top of the finished one, so its water stands among the upper levels.
    private void ShowWater() => water?.Show(Litres > 0 ? (int)Math.Ceiling((1 - (1 - Fill) * Hollowed) * water.Levels) : 0);

    private double Rainfall()
    {
        var accessor = entity.World.BlockAccessor;
        var pos = entity.Pos.AsBlockPos;
        if (accessor.GetRainMapHeightAt(pos) > pos.Y) return 0;

        return accessor.GetClimateAt(pos, EnumGetClimateMode.NowValues)?.Rainfall ?? 0;
    }
}
