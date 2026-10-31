using System.Collections.Generic;
using System.Linq;
using RoughSeas.Vessels;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Shipyard;

/// <summary>
/// A finished hull rigged where she lies: the float her first boom joined to her, then the rest of her booms, lashing,
/// spars, sail and stays, in the order her stages give.
/// </summary>
public class EntityBehaviorRigging(Entity entity) : EntityBehaviorFraming(entity)
{
    public new const string Code = "roughseas:rigging";

    private const string FloatKey = "float";

    private RiggingConfig rig = null!;

    private string? FloatWood => Tree?.GetString(FloatKey);

    public override string PropertyName() => Code;

    public static void Begin(Entity vessel, RiggingConfig rig, string poleWood, string floatWood) =>
        Begin(vessel, DataVersion, tree =>
        {
            tree.SetString(FloatKey, floatWood);
            tree.SetString(RoughSeas.Shipyard.Framing.WoodKey, poleWood);
            tree.SetInt(rig.Stages[0].Group, 1);
        });

    public override VesselPart[] Built(VesselPart[] designed) =>
        base.Built(designed).Select(part => part.Name == rig.FloatPart && FloatWood is { } wood ? part.As(WoodPrefix + wood, 1) : part).ToArray();

    public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements)
    {
        base.OnTesselation(ref entityShape, shapePathForLogging, ref shapeIsCloned, ref willDeleteElements);
        if (entity.Api is ICoreClientAPI capi && FloatWood is { } wood)
        {
            LathTexture.Draw(capi, ref entityShape, ref shapeIsCloned, LathTexture.Float, wood, entity.Properties.Client.Textures, capi.EntityTextureAtlas);
        }
    }

    protected override FramingConfig Configure(JsonObject attributes) => rig = attributes.AsObject<RiggingConfig>(new RiggingConfig());

    protected override IEnumerable<string> Hidden() => framing.Built ? base.Hidden() : base.Hidden().Concat(rig.Rigged);

    protected override string FormKey() => $"{base.FormKey()}/{FloatWood}";
}

public class RiggingConfig : FramingConfig
{
    public string FloatPart = "";

    public int FloatLogs;

    public double FloatMetres;

    /// <summary>The shape's groups shown only once she is finished.</summary>
    public string[] Rigged = [];

    public static new RiggingConfig? Of(EntityProperties type) => Of<RiggingConfig>(type, EntityBehaviorRigging.Code);
}
