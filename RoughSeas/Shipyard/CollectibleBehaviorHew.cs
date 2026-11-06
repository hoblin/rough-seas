using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace RoughSeas.Shipyard;

public class CollectibleBehaviorHew(CollectibleObject collectible) : CollectibleBehaviorBlows(collectible)
{
    public const string Code = "roughseas:hew";

    /// <summary>
    /// Also the name of its shape animation, since vanilla stops a held use by the animation its code names. Its cycle
    /// in patches/player-poses.json is one stroke: 60 frames, dugout.json's <c>secondsPerStroke</c> of 2.
    /// </summary>
    public const string Swing = "roughseas-hew";

    protected override string Animation => Swing;

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection? blockSel, EntitySelection? entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (entitySel == null && blockSel != null && firstEvent && byEntity.Controls.ShiftKey
            && byEntity.World.BlockAccessor.GetBlock(blockSel.Position).GetBehavior<BlockBehaviorBlankLog>() is { } log && log.TryLay(byEntity, blockSel.Position))
        {
            Unstruck(byEntity);
            handling = EnumHandling.PreventDefault;
            handHandling = EnumHandHandling.PreventDefault;
            return;
        }
        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling);
    }

    protected override IStruck? Struck(IWorldAccessor world, BlockSelection? blockSel, EntitySelection? entitySel) =>
        entitySel?.Entity?.GetBehavior<EntityBehaviorBurnout>() is { CanHew: true } blank ? blank : null;
}
