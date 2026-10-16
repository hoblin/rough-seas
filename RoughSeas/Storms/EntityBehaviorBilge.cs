using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using RoughSeas.SeaAndWeather;

namespace RoughSeas.Storms;

public class EntityBehaviorBilge(Entity entity) : EntityBehavior(entity)
{
    public const string Code = "roughseas:bilge";

    private const string TreeKey = "roughseas:bilge";
    private const int DataVersion = 1;

    private StormsModSystem storms = null!;
    private ISeaWeather sea = null!;
    private double deckSquareMetres;
    private double drainLitresPerHour;
    private float sinceUpdate;

    public double CapacityLitres { get; private set; }

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
        CapacityLitres = attributes["capacityLitres"].AsDouble();
        deckSquareMetres = attributes["deckSquareMetres"].AsDouble();
        drainLitresPerHour = attributes["drainLitresPerHour"].AsDouble();
        if (entity.World.Side == EnumAppSide.Server && Tree == null)
        {
            var tree = new TreeAttribute();
            tree.SetInt("version", DataVersion);
            tree.SetDouble("litres", 0);
            entity.WatchedAttributes.SetAttribute(TreeKey, tree);
        }
    }

    public void Ship(double litres)
    {
        var wasSwamped = Swamped;
        Litres += litres;
        if (Swamped && !wasSwamped) storms.Log(entity, "swamped: {0:F0} L aboard", Litres);
    }

    public double Bail(double litres)
    {
        var bailed = Math.Min(litres, Litres);
        Litres -= bailed;
        return bailed;
    }

    public override void OnGameTick(float deltaTime)
    {
        if (entity.World.Side != EnumAppSide.Server || CapacityLitres <= 0) return;
        sinceUpdate += deltaTime;
        if (sinceUpdate < storms.Config.TickSeconds) return;

        using var _ = storms.Budget.Measure();
        var hours = storms.GameHours(sinceUpdate);
        sinceUpdate = 0;
        var inflow = sea.DangerousOceans ? Rainfall() * storms.Config.Bilge.RainMillimetresPerHour * deckSquareMetres * hours : 0;
        Litres += inflow - drainLitresPerHour * hours;
    }

    public override void GetInfoText(StringBuilder infotext)
    {
        var litres = Litres;
        if (litres >= 1) infotext.AppendLine(Lang.Get("roughseas:bilge-water", Math.Round(litres), Math.Round(CapacityLitres)));
        base.GetInfoText(infotext);
    }

    private double Rainfall()
    {
        var accessor = entity.World.BlockAccessor;
        var pos = entity.Pos.AsBlockPos;
        if (accessor.GetRainMapHeightAt(pos) > pos.Y) return 0;

        return accessor.GetClimateAt(pos, EnumGetClimateMode.NowValues)?.Rainfall ?? 0;
    }
}
