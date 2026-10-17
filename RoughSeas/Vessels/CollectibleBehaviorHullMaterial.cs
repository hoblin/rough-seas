using Vintagestory.API.Common;

namespace RoughSeas.Vessels;

/// <summary>
/// Holds back what the item would do in the hand (eat it) when it is aimed at a hull that is mended or greased with it,
/// so the click reaches the vessel.
/// </summary>
public class CollectibleBehaviorHullMaterial(CollectibleObject collObj) : CollectibleBehavior(collObj)
{
    public const string Code = "roughseas:hullmaterial";

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (entitySel?.Entity?.GetBehavior<EntityBehaviorVesselIntegrity>()?.MendsWith(slot) == true) handling = EnumHandling.PreventDefault;
    }
}
