using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace RoughSeas.Shipyard;

public class CollectibleBehaviorHew(CollectibleObject collectible) : CollectibleBehavior(collectible)
{
    public const string Code = "roughseas:hew";

    private const string StrokesKey = "roughseas:strokes";
    private const string HewingKey = "roughseas:hewing";
    /// <summary>
    /// Also the name of its shape animation, since vanilla stops a held use by the animation its code names. Its cycle
    /// in patches/player-poses.json is one stroke: 60 frames, dugout.json's <c>secondsPerStroke</c> of 2.
    /// </summary>
    public const string Animation = "roughseas-hew";

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (entitySel == null && blockSel != null && firstEvent && byEntity.Controls.ShiftKey
            && byEntity.World.BlockAccessor.GetBlock(blockSel.Position).GetBehavior<BlockBehaviorBlankLog>() is { } log && log.TryLay(byEntity, blockSel.Position))
        {
            byEntity.Attributes.SetBool(HewingKey, false);
            handling = EnumHandling.PreventDefault;
            handHandling = EnumHandHandling.PreventDefault;
            return;
        }
        if (!firstEvent || Blank(entitySel) is not { CanHew: true } blank) return;

        if (byEntity.World.Side == EnumAppSide.Server) blank.TakeUp(byEntity);
        byEntity.Attributes.SetBool(HewingKey, true);
        byEntity.Attributes.SetInt(StrokesKey, 0);
        handling = EnumHandling.PreventDefault;
        handHandling = EnumHandHandling.PreventDefault;
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    {
        if (!byEntity.Attributes.GetBool(HewingKey) || Blank(entitySel) is not { CanHew: true } blank) return false;

        handling = EnumHandling.PreventDefault;
        if (byEntity.World.Side != EnumAppSide.Server) return true;

        var strokes = (int)(secondsUsed / blank.SecondsPerStroke);
        var done = byEntity.Attributes.GetInt(StrokesKey);
        if (strokes <= done) return true;

        byEntity.Attributes.SetInt(StrokesKey, strokes);
        while (done++ < strokes)
        {
            if (!blank.Hew(byEntity, slot)) return false;
        }
        return true;
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    {
        if (Blank(entitySel) != null) handling = EnumHandling.PreventDefault;
    }

    public override string? GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity forEntity, ref EnumHandling bhHandling)
    {
        // Named only while the hand is in use, so the name changes on release and vanilla stops the swing.
        if (forEntity is not EntityPlayer { Controls.HandUse: EnumHandInteract.HeldItemInteract } player
            || Blank(player.Player?.CurrentEntitySelection) is not { CanHew: true }) return null;

        bhHandling = EnumHandling.PreventDefault;
        return Animation;
    }

    private static EntityBehaviorBurnout? Blank(EntitySelection? entitySel) => entitySel?.Entity?.GetBehavior<EntityBehaviorBurnout>();
}
