using System;
using System.Collections.Generic;
using System.Linq;
using RoughSeas.Vessels;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace RoughSeas.Shipyard;

/// <summary>
/// A finished hull rigged where she lies: the float her first boom joined to her, then the rest of her booms, lashing,
/// spars, sail and stays, in the order her stages give.
/// </summary>
public class EntityBehaviorRigging(Entity entity) : EntityBehaviorFraming(entity), IMasted
{
    public new const string Code = "roughseas:rigging";

    private const string FloatKey = "float";
    private const string SailKey = "sailPosition";
    private const int ShortestPole = 2;
    private const int LongestPole = 5;

    private RiggingConfig rig = null!;

    private string? FloatWood => Tree?.GetString(FloatKey);

    public override double FloorMetres => rig.FloorMetres;

    public double MastMetres(double designed) =>
        framing.Stages.FirstOrDefault(stage => stage.Group == rig.Mast) is { } mast && framing.Laid(mast) > 0 ? designed : 0;

    public double FloatOffMetres => rig.FloatOffMetres;

    public double LashedShare => framing.Stages.FirstOrDefault(stage => stage.Group == rig.Lashing) is { } lashing ? framing.Share(lashing) : 1;

    /// <summary>
    /// What of her rig comes away whole when her float does: each pole laid, as long as its piece took, the planks
    /// and the pieces that <paramref name="keeps"/> names by stage, and her float as firewood. Lashings and seams part.
    /// </summary>
    public IEnumerable<ItemStack> Stripped(IWorldAccessor world, IReadOnlyDictionary<string, string> keeps, double firewoodLitres)
    {
        var own = Wood.Of(entity.Properties.Variant)?.Code;
        foreach (var stage in framing.Stages)
        {
            int laid = framing.Laid(stage);
            var codes = stage.Work switch
            {
                FramingWork.Pole when (framing.Wood ?? own) is { } wood => stage.Metres.Take(laid).Select(metres => Pole(wood, metres)),
                FramingWork.Plank when (framing.WoodOf(stage) ?? own) is { } wood => Enumerable.Repeat(Wood.Parse(wood).Plank, laid),
                _ when keeps.TryGetValue(stage.Group, out var kept) => Enumerable.Repeat(new AssetLocation(kept), laid),
                _ => []
            };
            foreach (var code in codes)
            {
                if (world.GetItem(code) is { } item) yield return new ItemStack(item);
                else if (world.GetBlock(code) is { } block) yield return new ItemStack(block);
            }
        }
        if (FloatStage is { WoodLitres: { Length: > 0 } litres } hewing && world.GetItem(new AssetLocation("game:firewood")) is { } firewood)
        {
            int pieces = (int)Math.Round(litres[framing.Laid(hewing)] / firewoodLitres);
            if (pieces > 0) yield return new ItemStack(firewood, pieces);
        }
    }

    private AssetLocation Pole(string wood, double metres) =>
        new(Shipyard.Mod.Info.ModID, $"pole-{wood}-{Math.Clamp((int)Math.Ceiling(metres), ShortestPole, LongestPole)}");

    public override string PropertyName() => Code;

    public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
    {
        if (!framing.Built && entity.WatchedAttributes.GetInt(SailKey) != 0) entity.WatchedAttributes.SetInt(SailKey, 0);
    }

    public static void Begin(Entity vessel, RiggingConfig rig, string poleWood, string floatWood) =>
        Begin(vessel, DataVersion, tree =>
        {
            tree.SetString(FloatKey, floatWood);
            tree.SetString(RoughSeas.Shipyard.Framing.WoodKey, poleWood);
            tree.SetInt(rig.Stages[0].Group, 1);
        });

    public override VesselPart[] Built(VesselPart[] designed) =>
        base.Built(designed).Select(part => part.Name == rig.FloatPart && FloatWood is { } wood ? part.As(WoodPrefix + wood, FloatShare(stage => stage.WoodLitres)) : part).ToArray();

    public override double WaterplaneSquareMetres(double designed) =>
        FloatStage is { WaterlineSquareMetres: { Length: > 0 } waterline } stage
            ? designed + waterline[framing.Laid(stage)] - waterline[^1]
            : designed;

    public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements)
    {
        base.OnTesselation(ref entityShape, shapePathForLogging, ref shapeIsCloned, ref willDeleteElements);
        if (entity.Api is not ICoreClientAPI capi || FloatWood is not { } wood) return;

        foreach (var (part, face) in new[] { (WoodTexture.Float, WoodTexture.BareSide), (WoodTexture.FloatBark, WoodTexture.Bark), (WoodTexture.FloatRings, WoodTexture.Rings) })
        {
            WoodTexture.Draw(capi, ref entityShape, ref shapeIsCloned, part, wood, entity.Properties.Client.Textures, capi.EntityTextureAtlas, face);
        }
    }

    private FramingStage? FloatStage => framing.Stages.FirstOrDefault(stage => stage.Group == rig.FloatStage);

    private double FloatShare(System.Func<FramingStage, double[]> measured) =>
        FloatStage is { } stage && measured(stage) is { Length: > 0 } values ? values[framing.Laid(stage)] / values[^1] : 1;

    protected override FramingConfig Configure(JsonObject attributes) => rig = attributes.AsObject<RiggingConfig>(new RiggingConfig());

    protected override IEnumerable<string> Hidden() => framing.Built ? base.Hidden() : base.Hidden().Concat(rig.Rigged);

    protected override string FormKey() => $"{base.FormKey()}/{FloatWood}";
}

public class RiggingConfig : FramingConfig
{
    public string FloatPart = "";

    /// <summary>The stage that hews the float from its logs: its passes measure what wood is left and how wide it stands at the water.</summary>
    public string FloatStage = "";

    public int FloatLogs;

    public double FloatOffMetres;

    /// <summary>The stage whose first piece stands her mast.</summary>
    public string Mast = "";

    /// <summary>The stage that lashes her booms to her float.</summary>
    public string Lashing = "";

    /// <summary>The top of the hold's floor above her origin: her hull's, finished before she was rigged.</summary>
    public double FloorMetres;

    /// <summary>The shape's groups shown only once every piece is laid.</summary>
    public string[] Rigged = [];

    public static new RiggingConfig? Of(EntityProperties type) => Of<RiggingConfig>(type, EntityBehaviorRigging.Code);
}
