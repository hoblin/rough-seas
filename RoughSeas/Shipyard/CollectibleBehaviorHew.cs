using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace RoughSeas.Shipyard;

public class CollectibleBehaviorHew(CollectibleObject collectible) : CollectibleBehavior(collectible)
{
    public const string Code = "roughseas:hew";

    private const string StrokesKey = "roughseas:strokes";
    private const string Animation = "axechop";

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (!firstEvent || Blank(entitySel) is not { CanHew: true }) return;

        byEntity.Attributes.SetInt(StrokesKey, 0);
        handling = EnumHandling.PreventDefault;
        handHandling = EnumHandHandling.PreventDefault;
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    {
        if (Blank(entitySel) is not { CanHew: true } blank) return false;

        handling = EnumHandling.PreventDefault;
        if (byEntity.World.Side != EnumAppSide.Server) return true;

        var strokes = (int)(secondsUsed / blank.SecondsPerStroke);
        var done = byEntity.Attributes.GetInt(StrokesKey);
        if (strokes <= done) return true;

        byEntity.Attributes.SetInt(StrokesKey, strokes);
        return blank.Hew(byEntity, slot);
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    {
        if (Blank(entitySel) != null) handling = EnumHandling.PreventDefault;
    }

    public override string? GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity forEntity, ref EnumHandling bhHandling)
    {
        if (Blank((forEntity as EntityPlayer)?.Player?.CurrentEntitySelection) is not { CanHew: true }) return null;

        bhHandling = EnumHandling.PreventDefault;
        return Animation;
    }

    private static EntityBehaviorBurnout? Blank(EntitySelection? entitySel) => entitySel?.Entity?.GetBehavior<EntityBehaviorBurnout>();
}
