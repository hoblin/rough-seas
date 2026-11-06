using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace RoughSeas.Shipyard;

public abstract class CollectibleBehaviorBlows(CollectibleObject collectible) : CollectibleBehavior(collectible)
{
    protected abstract string Animation { get; }

    private string StrikingKey => $"roughseas:{Animation}";

    private string BlowsKey => $"roughseas:{Animation}/blows";

    protected abstract IStruck? Struck(IWorldAccessor world, BlockSelection? blockSel, EntitySelection? entitySel);

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection? blockSel, EntitySelection? entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (!firstEvent || Struck(byEntity.World, blockSel, entitySel) is not { } work || !work.TakeUp(byEntity)) return;

        byEntity.Attributes.SetBool(StrikingKey, true);
        byEntity.Attributes.SetInt(BlowsKey, 0);
        handling = EnumHandling.PreventDefault;
        handHandling = EnumHandHandling.PreventDefault;
    }

    protected void Unstruck(EntityAgent byEntity) => byEntity.Attributes.SetBool(StrikingKey, false);

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection? blockSel, EntitySelection? entitySel, ref EnumHandling handling)
    {
        if (!byEntity.Attributes.GetBool(StrikingKey) || Struck(byEntity.World, blockSel, entitySel) is not { } work) return false;

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
        if (Struck(byEntity.World, blockSel, entitySel) != null) handling = EnumHandling.PreventDefault;
    }

    public override string? GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity forEntity, ref EnumHandling bhHandling)
    {
        if (forEntity is not EntityPlayer { Controls.HandUse: EnumHandInteract.HeldItemInteract, Player: { } player }
            || Struck(forEntity.World, player.CurrentBlockSelection, player.CurrentEntitySelection) == null) return null;

        bhHandling = EnumHandling.PreventDefault;
        return Animation;
    }
}
