using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace RoughSeas.Shipyard;

public abstract class CollectibleBehaviorBlows(CollectibleObject collectible) : CollectibleBehavior(collectible)
{
    private readonly Dictionary<long, IStruck> taken = [];

    protected abstract string Animation { get; }

    private string BlowsKey => $"roughseas:{Animation}/blows";

    protected abstract IStruck? Struck(IWorldAccessor world, BlockSelection? blockSel, EntitySelection? entitySel);

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection? blockSel, EntitySelection? entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (!firstEvent || Struck(byEntity.World, blockSel, entitySel) is not { } work || !work.TakeUp(byEntity)) return;

        taken[byEntity.EntityId] = work;
        byEntity.Attributes.SetInt(BlowsKey, 0);
        handling = EnumHandling.PreventDefault;
        handHandling = EnumHandHandling.PreventDefault;
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection? blockSel, EntitySelection? entitySel, ref EnumHandling handling)
    {
        if (!taken.TryGetValue(byEntity.EntityId, out var work) || !work.Equals(Struck(byEntity.World, blockSel, entitySel))) return false;

        handling = EnumHandling.PreventDefault;
        if (byEntity.World.Side != EnumAppSide.Server) return true;

        var blows = (int)(secondsUsed / work.SecondsPerBlow);
        var done = byEntity.Attributes.GetInt(BlowsKey);
        if (blows <= done) return true;

        byEntity.Attributes.SetInt(BlowsKey, blows);
        while (done++ < blows)
        {
            if (!work.Strike(byEntity, slot)) return false;
        }
        return true;
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection? blockSel, EntitySelection? entitySel, ref EnumHandling handling)
    {
        if (taken.Remove(byEntity.EntityId)) handling = EnumHandling.PreventDefault;
    }

    public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection? blockSel, EntitySelection? entitySel, EnumItemUseCancelReason cancelReason, ref EnumHandling handled)
    {
        taken.Remove(byEntity.EntityId);
        return base.OnHeldInteractCancel(secondsUsed, slot, byEntity, blockSel, entitySel, cancelReason, ref handled);
    }

    public override string? GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity forEntity, ref EnumHandling bhHandling)
    {
        // Named only while the hand is in use, so the name changes on release and vanilla stops the swing.
        if (forEntity is not EntityPlayer { Controls.HandUse: EnumHandInteract.HeldItemInteract, Player: { } player }
            || Struck(forEntity.World, player.CurrentBlockSelection, player.CurrentEntitySelection) == null) return null;

        bhHandling = EnumHandling.PreventDefault;
        return Animation;
    }
}
