using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
/// A vessel built up from the bark sheet stripped off a standing tree, stage by stage in the order her
/// <see cref="FramingConfig.Stages"/> give, each piece drawn where it lies.
/// </summary>
public class EntityBehaviorFraming(Entity entity) : EntityBehaviorBuild(entity)
{
    public const string Code = "roughseas:framing";

    private const int DataVersion = 1;
    private const string WoodPrefix = "wood-";

    private FramingConfig config = null!;
    private Framing framing = null!;
    private Dictionary<FramingStage, Laying> layings = null!;
    private ISeaWeather sea = null!;

    public override bool Finished => framing.Finished(sea.Clock.Hours);

    public override double Hollowed => framing.Sealed ? 1 : 0;

    public override double Uncut => 0;

    public override double FloorMetres => 0;

    public override string PropertyName() => Code;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        base.Initialize(properties, attributes);
        config = attributes.AsObject<FramingConfig>(new FramingConfig());
        config.Count(Pieces);
        framing = new Framing(() => Tree, MarkDirty, config.Stages);
        layings = config.Stages.ToDictionary(stage => stage, stage => Laying.For(stage, config));
        sea = entity.Api.ModLoader.GetModSystem<SeaWeatherSystem>();
    }

    public static void Begin(Entity vessel) => Begin(vessel, DataVersion, _ => { });

    public override VesselPart[] Built(VesselPart[] designed) =>
        designed.Select(part => part.Material == config.FramePart
            ? part.As(framing.Wood is { } wood ? WoodPrefix + wood : part.Material, framing.Framed)
            : part).ToArray();

    public override Shell Formed(Shell designed) =>
        designed.Lined(framing.Wood is { } wood ? WoodPrefix + wood : designed.Backing?.Material ?? config.FramePart, framing.Lined);

    /// <summary>The work the held item does on her now, with the lang key refusing it, or null when it does none.</summary>
    public (FramingStage Stage, string? Refusal)? WorkWith(ItemSlot slot)
    {
        if (framing.Current is not { } stage || !layings[stage].Takes(slot)) return null;

        return (stage, layings[stage].Refusal(slot, framing));
    }

    /// <summary>Lays the current stage's next piece from <paramref name="slot"/>. Server only.</summary>
    public void Lay(EntityAgent byEntity, ItemSlot slot)
    {
        if (framing.Current is not { } stage) return;

        var laying = layings[stage];
        double setsAt = sea.Clock.Hours + laying.WetHours(entity.World, slot);
        if (WorkWith(slot) is not { Refusal: null }) return;

        var wood = laying.WoodOf(slot);
        bool creative = (byEntity as EntityPlayer)?.Player?.WorldData.CurrentGameMode == EnumGameMode.Creative;
        if (!creative) laying.Spend(slot, byEntity);
        slot.MarkDirty();
        framing.Lay(wood, setsAt);
        entity.World.PlaySoundAt(new AssetLocation("game:sounds/player/build"), entity, null, true, 16);
        Shipyard.Log(entity, "{0} lays {1} {2} of {3}{4}{5}", byEntity.GetName(), stage.Group, framing.Laid(stage), stage.Pieces,
            wood != null ? $", of {wood}" : "", stage.Sets ? $", setting in {setsAt - sea.Clock.Hours:F0} h" : "");
        if (framing.Built) Shipyard.Log(entity, "built: her ribs set in {0:F0} h", Math.Max(0, framing.SetsAt - sea.Clock.Hours));
    }

    public override WorldInteraction[]? GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled)
    {
        if (framing.Current is not { } stage) return null;

        var laying = layings[stage];
        return ObjectCacheUtil.GetOrCreate(world.Api, $"{Code}/help/{stage.Group}", () => new[]
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
        if (entity.Api is ICoreClientAPI capi && framing.Wood is { } wood)
        {
            LathTexture.Draw(capi, ref entityShape, ref shapeIsCloned, wood, entity.Properties.Client.Textures, capi.EntityTextureAtlas);
        }
    }

    protected override IEnumerable<string> Hidden() => framing.Hidden();

    protected override string ShapeKey() => framing.ShapeKey;

    protected override string FormKey() => $"{framing.Wood}/{framing.Framed}/{framing.Lined}/{framing.Sealed}";

    private int Pieces(string group)
    {
        if (entity.Properties.Client?.Shape?.Base is not { } shape) return 0;

        var path = shape.CopyWithPathPrefixAndAppendixOnce("shapes/", ".json");
        return ObjectCacheUtil.GetOrCreate(entity.Api, $"{Code}/pieces/{path}/{group}",
            () => FramingConfig.PiecesIn(Shape.TryGet(entity.Api, path), group));
    }
}

public sealed class FramingConfig
{
    public FramingStage[] Stages = [];

    public static FramingConfig? Of(EntityProperties type) =>
        (type.Server?.BehaviorsAsJsonObj ?? type.Client?.BehaviorsAsJsonObj)?
            .FirstOrDefault(behavior => behavior["code"].AsString() == EntityBehaviorFraming.Code)?.AsObject<FramingConfig>();

    public static int PiecesIn(Shape? shape, string group) => shape?.GetElementByName(group)?.Children?.Length ?? 0;

    /// <summary>Counts each stage's pieces with <paramref name="pieces"/>, from the group that draws it.</summary>
    public void Count(System.Func<string, int> pieces)
    {
        foreach (var stage in Stages) stage.Pieces = pieces(stage.Group);
    }

    /// <summary>The part of her designed build that her laths make: its share and reference, taken by the wood they are of.</summary>
    public string FramePart = "";

    public string Laths = "";

    public string Rope = "";

    public string Pitch = "";

    /// <summary>How long a knife takes the bark off a standing trunk: the real time of that work over sixty.</summary>
    public double BarkSeconds;

    public int TrunkBlocks;
}
