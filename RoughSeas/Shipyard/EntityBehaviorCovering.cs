using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using RoughSeas.Vessels;

namespace RoughSeas.Shipyard;

/// <summary>
/// A hide boat bound from laths laid out on the ground, then framed and covered piece by piece where she lies, upside
/// down when her build says so. Her cover is the hides sewn on her, by type, and a seam between each.
/// </summary>
public class EntityBehaviorCovering(Entity entity) : EntityBehaviorFraming(entity), ICovered
{
    public new const string Code = "roughseas:covering";

    private const string CoverKey = "cover";
    private const string HidesKey = "hides";
    private const string UprightKey = "upright";
    private const string Overturned = "Overturned";

    private CoveringConfig covering = null!;

    public override bool Finished => base.Finished && Upright;

    public bool Upright => Tree?.GetBool(UprightKey, true) ?? true;

    public int Hides => Tree?.GetInt(HidesKey) ?? 0;

    public HideCover? Cover => Tree == null ? null : new HideCover(
        Tree.GetTreeAttribute(CoverKey) is { } cover ? cover.ToDictionary(entry => entry.Key, entry => cover.GetInt(entry.Key)) : new Dictionary<string, int>(),
        System.Math.Max(0, Hides - 1));

    public override string PropertyName() => Code;

    public static void Begin(Entity vessel, CoveringConfig config, string wood, double setsAt) =>
        Begin(vessel, DataVersion, tree =>
        {
            tree.SetString(Framing.WoodKey, wood);
            tree.SetInt(config.Stages[0].Group, config.Stages[0].Pieces);
            tree.SetInt(config.Stages[1].Group, 1);
            tree.SetDouble(Framing.SetsAtKey, setsAt);
            if (config.Overturned) tree.SetBool(UprightKey, false);
        });

    public override (double Seconds, string? Refusal)? WorkWith(ItemSlot slot)
    {
        var job = base.WorkWith(slot);
        return job is { } work && framing.Current is { Work: FramingWork.Hide } && sea.Clock.Hours < framing.SetsAt ? (work.Seconds, "shipyard-frame-setting") : job;
    }

    public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
    {
        if (Upright || !framing.Built || mode != EnumInteractMode.Interact || byEntity.Controls.ShiftKey || !itemslot.Empty) return;

        handled = EnumHandling.PreventSubsequent;
        if (entity.World.Side != EnumAppSide.Server) return;

        Tree!.SetBool(UprightKey, true);
        MarkDirty();
        entity.World.PlaySoundAt(new AssetLocation("game:sounds/block/planks"), entity, null, true, 16);
        Shipyard.Log(entity, "turned over by {0}", byEntity.GetName());
    }

    public override WorldInteraction[]? GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled)
    {
        if (Upright || !framing.Built) return base.GetInteractionHelp(world, es, player, ref handled);

        return [new WorldInteraction { ActionLangCode = "roughseas:shipyard-turn-over", MouseButton = EnumMouseButton.Right }];
    }

    public override void GetInfoText(StringBuilder infotext)
    {
        base.GetInfoText(infotext);
        if (!Upright && framing.Built) infotext.AppendLine(Lang.Get("roughseas:shipyard-overturned"));
    }

    public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements)
    {
        base.OnTesselation(ref entityShape, shapePathForLogging, ref shapeIsCloned, ref willDeleteElements);
        if (Upright) return;

        if (!shapeIsCloned)
        {
            entityShape = entityShape.Clone();
            shapeIsCloned = true;
        }
        var turned = new ShapeElement
        {
            Name = Overturned,
            From = [0, 0, 0],
            To = [0, 0, 0],
            RotationOrigin = [0, covering.OverturnedPivot, 8],
            RotationX = 180,
            Children = entityShape.Elements,
            FacesResolved = new ShapeElementFace[6]
        };
        foreach (var child in turned.Children) child.ParentElement = turned;
        entityShape.Elements = [turned];
    }

    protected override void Laid(FramingStage stage, CollectibleObject piece, int pieces)
    {
        if (stage.Work != FramingWork.Hide || piece.Variant["type"] is not { } type) return;

        var cover = Tree!.GetOrAddTreeAttribute(CoverKey);
        cover.SetInt(type, cover.GetInt(type) + System.Math.Min(pieces, stage.Pieces - framing.Laid(stage)));
        Tree.SetInt(HidesKey, Hides + 1);
    }

    protected override string ShapeKey() => $"{base.ShapeKey()}/{Upright}";

    protected override FramingConfig Configure(JsonObject attributes) => covering = attributes.AsObject<CoveringConfig>(new CoveringConfig());
}

public class CoveringConfig : FramingConfig
{
    public JToken? Layout;

    public bool Overturned;

    public double OverturnedPivot;

    public static new CoveringConfig? Of(EntityProperties type) => Of<CoveringConfig>(type, EntityBehaviorCovering.Code);
}
