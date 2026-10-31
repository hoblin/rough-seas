using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using RoughSeas.SeaAndWeather;
using RoughSeas.Vessels;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace RoughSeas.Shipyard;

/// <summary>
/// A vessel built up where she lies, stage by stage in the order her <see cref="FramingConfig.Stages"/> give, each
/// piece drawn where it is laid: from a bark sheet stripped off a standing tree, from bundles bound where they lay, or
/// from laths laid out on the ground.
/// </summary>
public class EntityBehaviorFraming(Entity entity) : EntityBehaviorBuild(entity), IWorkedHull
{
    public const string Code = "roughseas:framing";

    protected const int DataVersion = 1;
    protected const string WoodPrefix = "wood-";

    private const double HeldSlackSeconds = 0.5;

    private readonly Dictionary<long, long> laidAt = [];
    private FramingConfig config = null!;
    private Dictionary<FramingStage, Laying> layings = null!;
    protected ISeaWeather sea = null!;

    protected Framing framing = null!;

    public override bool Finished => framing.Finished(sea.Clock.Hours);

    public override double HoldLitres(double designed) => framing.Sealed ? designed + framing.Raised(stage => stage.RaisesLitres) : 0;

    public override double DepthMetres(double designed) => designed + framing.Raised(stage => stage.RaisesMetres);

    public override double Uncut => 0;

    public override double FloorMetres => 0;

    public override string PropertyName() => Code;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        base.Initialize(properties, attributes);
        config = Configure(attributes);
        config.Measure(entity.Properties.Client?.Shape?.Base is { } shape ? FramingConfig.ShapeAt(entity.Api, shape) : null);
        framing = new Framing(() => Tree, MarkDirty, config.Stages);
        layings = config.Stages.ToDictionary(stage => stage, stage => Laying.For(stage, config));
        sea = entity.Api.ModLoader.GetModSystem<SeaWeatherSystem>();
    }

    public static void Begin(Entity vessel, string? firstLaid = null) => Begin(vessel, DataVersion, tree =>
    {
        if (firstLaid != null) tree.SetInt(firstLaid, 1);
    });

    public override VesselPart[] Built(VesselPart[] designed) =>
        designed.Select(part => part.Name == config.FramePart
                ? part.As(framing.Wood is { } wood ? WoodPrefix + wood : part.Material, framing.Framed)
                : config.Stages.FirstOrDefault(stage => stage.Part == part.Name) is { } stage
                    ? part.As(framing.WoodOf(stage) is { } pieces ? WoodPrefix + pieces : part.Material, framing.Share(stage))
                : part).ToArray();

    public override Shell Formed(Shell designed) =>
        designed.Lined(framing.Wood is { } wood ? WoodPrefix + wood : designed.Backing?.Material ?? "", framing.Lined);

    public override BlockDropItemStack[] Wrecked(BlockDropItemStack[] designed) =>
        designed.Where(drop => drop.ResolvedItemstack is not { } stack || LaidAny(new DummySlot(stack))).Select(AsLaid).ToArray();

    public virtual (double Seconds, string? Refusal)? WorkWith(ItemSlot slot)
    {
        if (framing.Current is not { } stage || !layings[stage].Takes(slot)) return null;

        return (stage.Seconds * Math.Min(layings[stage].Pieces(slot), stage.Pieces - framing.Laid(stage)), layings[stage].Refusal(slot, framing));
    }

    public void Lay(EntityAgent byEntity, ItemSlot slot)
    {
        if (framing.Current is not { } stage) return;

        var laying = layings[stage];
        double setsAt = sea.Clock.Hours + laying.WetHours(entity.World, slot);
        if (WorkWith(slot) is not { Refusal: null } job || Hurried(byEntity, job.Seconds)) return;

        var wood = laying.WoodOf(slot);
        var pieces = laying.Pieces(slot);
        var piece = slot.Itemstack!.Collectible;
        bool creative = (byEntity as EntityPlayer)?.Player?.WorldData.CurrentGameMode == EnumGameMode.Creative;
        if (!creative) laying.Spend(slot, byEntity);
        slot.MarkDirty();
        Laid(stage, piece, pieces);
        framing.Lay(wood, setsAt, pieces);
        entity.World.PlaySoundAt(new AssetLocation("game:sounds/player/build"), entity, null, true, 16);
        Shipyard.Log(entity, "{0} lays {1} {2} of {3}{4}{5}", byEntity.GetName(), stage.Group, framing.Laid(stage), stage.Pieces,
            wood != null ? $", of {wood}" : "", stage.Sets ? $", setting in {setsAt - sea.Clock.Hours:F0} h" : "");
        if (framing.Built) Shipyard.Log(entity, "built{0}", framing.Stages.Any(stage => stage.Sets) ? $": her ribs set in {Math.Max(0, framing.SetsAt - sea.Clock.Hours):F0} h" : "");
    }

    public override WorldInteraction[]? GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled)
    {
        if (framing.Current is not { } stage) return null;

        var laying = layings[stage];
        return ObjectCacheUtil.GetOrCreate(world.Api, $"{Code}/help/{entity.Code}/{stage.Group}", () => new[]
        {
            new WorldInteraction { ActionLangCode = laying.Action, MouseButton = EnumMouseButton.Right, Itemstacks = laying.Shown(world).ToArray() }
        });
    }

    public override void GetInfoText(StringBuilder infotext)
    {
        if (framing.Progress(sea.Clock.Hours) is { } progress) infotext.AppendLine(progress);
        base.GetInfoText(infotext);
    }

    public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements)
    {
        base.OnTesselation(ref entityShape, shapePathForLogging, ref shapeIsCloned, ref willDeleteElements);
        if (entity.Api is not ICoreClientAPI capi) return;

        if (framing.Wood is { } wood)
        {
            WoodTexture.Draw(capi, ref entityShape, ref shapeIsCloned, WoodTexture.Frame, wood, entity.Properties.Client.Textures, capi.EntityTextureAtlas);
        }
        foreach (var stage in config.Stages)
        {
            if (stage.Texture is { } texture && framing.WoodOf(stage) is { } pieces)
            {
                WoodTexture.Draw(capi, ref entityShape, ref shapeIsCloned, texture, pieces, entity.Properties.Client.Textures, capi.EntityTextureAtlas);
            }
        }
    }

    protected virtual void Laid(FramingStage stage, CollectibleObject piece, int pieces) { }

    private bool Hurried(EntityAgent byEntity, double seconds)
    {
        long now = entity.World.ElapsedMilliseconds;
        if (laidAt.TryGetValue(byEntity.EntityId, out var last) && now - last < (seconds - HeldSlackSeconds) * 1000)
        {
            Shipyard.Log(entity, "{0} lays again {1} ms after the last piece, faster than its {2} s: ignored", byEntity.GetName(), now - last, seconds);
            return true;
        }

        laidAt[byEntity.EntityId] = now;
        return false;
    }

    protected virtual FramingConfig Configure(JsonObject attributes) => attributes.AsObject<FramingConfig>(new FramingConfig());

    protected override IEnumerable<string> Hidden() => framing.Hidden();

    protected override string ShapeKey() => framing.ShapeKey;

    protected override string FormKey() => framing.ShapeKey;

    private bool LaidAny(ItemSlot piece)
    {
        var stages = layings.Where(laying => laying.Value.Takes(piece)).Select(laying => laying.Key).ToArray();
        return stages.Length == 0 || stages.Any(stage => framing.Laid(stage) > 0);
    }

    private BlockDropItemStack AsLaid(BlockDropItemStack drop)
    {
        if (drop.ResolvedItemstack?.Collectible is not { } piece
            || layings.FirstOrDefault(laying => laying.Value.Takes(new DummySlot(drop.ResolvedItemstack))).Key is not { } stage
            || framing.WoodOf(stage) is not { } laid) return drop;

        var wood = Wood.Parse(laid);
        var asLaid = drop.Clone();
        asLaid.Code = new AssetLocation(wood.Domain, piece.CodeWithVariant(Wood.VariantCode, wood.Species).Path);
        return asLaid.Resolve(entity.World, "laid wreckage", entity.Code) ? asLaid : drop;
    }

}

public class FramingConfig
{
    public FramingStage[] Stages = [];

    public static FramingConfig? Of(EntityProperties type) => Of<FramingConfig>(type, EntityBehaviorFraming.Code);

    protected static T? Of<T>(EntityProperties type, string code) where T : FramingConfig =>
        (type.Server?.BehaviorsAsJsonObj ?? type.Client?.BehaviorsAsJsonObj)?
            .FirstOrDefault(behavior => behavior["code"].AsString() == code)?.AsObject<T>();

    /// <summary>The shape's JSON: the typed <see cref="Shape"/> drops the <c>metres</c> its builder writes on each piece.</summary>
    public static JObject? ShapeAt(ICoreAPI api, AssetLocation shape)
    {
        var path = shape.CopyWithPathPrefixAndAppendixOnce("shapes/", ".json");
        return ObjectCacheUtil.GetOrCreate(api, $"{EntityBehaviorFraming.Code}/shape/{path}", () => api.Assets.TryGet(path)?.ToObject<JObject>());
    }

    /// <summary>Counts each stage's pieces from the group that draws it in <paramref name="shape"/>, and the metres of wood each takes.</summary>
    public void Measure(JObject? shape)
    {
        foreach (var stage in Stages)
        {
            stage.Metres = Named(shape?["elements"], stage.Group)?["children"]?.Select(piece => piece.Value<double?>("metres") ?? 0).ToArray() ?? [];
            stage.Pieces = stage.Metres.Length;
        }
    }

    private static JToken? Named(JToken? elements, string name) =>
        elements?.FirstOrDefault(element => element.Value<string>("name") == name)
        ?? elements?.Select(element => Named(element["children"], name)).FirstOrDefault(found => found != null);

    /// <summary>The name of the part of her designed build that her laths or poles make: its share and reference, taken by the wood they are of.</summary>
    public string FramePart = "";

    public string Laths = "";

    public string Poles = "";

    public string Rope = "";

    public string Pitch = "";

    public string Cloth = "";

    public string Planks = "";

    public string Hides = "";

    /// <summary>The hides' <c>type</c> variants a hide stage takes.</summary>
    public string[] HideStates = [];

    /// <summary>How long a knife takes the bark off a standing trunk: the real time of that work over sixty.</summary>
    public double BarkSeconds;

    public int TrunkBlocks;
}
