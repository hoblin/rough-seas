using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace RoughSeas.Shipyard;

/// <summary>A lath, a rope or a pot of pitch held on a canoe being framed lays its next piece.</summary>
public class CollectibleBehaviorFraming(CollectibleObject collectible) : CollectibleBehavior(collectible)
{
    public const string Code = "roughseas:framing";

    private readonly HeldWork work = new();

    /// <summary>Whether the held item does work on the vessel selected, refused or not.</summary>
    public static bool WorksOn(ItemSlot slot, EntitySelection? entitySel) => Hull(entitySel)?.WorkWith(slot) != null;

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (Hull(entitySel)?.WorkWith(slot) is not { } job) return;

        handling = EnumHandling.PreventSubsequent;
        handHandling = EnumHandHandling.PreventDefault;
        if (job.Refusal != null)
        {
            (byEntity.Api as ICoreClientAPI)?.TriggerIngameError(this, job.Refusal, Lang.Get("roughseas:" + job.Refusal));
            handHandling = EnumHandHandling.PreventDefaultAction;
            return;
        }
        work.Start(byEntity);
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    {
        if (Hull(entitySel)?.WorkWith(slot) is not { Refusal: null } job) return false;

        handling = EnumHandling.PreventSubsequent;
        return work.Step(secondsUsed, job.Stage.Seconds);
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    {
        if (Hull(entitySel) is not { } hull || hull.WorkWith(slot) is not { Refusal: null } job)
        {
            work.Stop(byEntity);
            return;
        }

        handling = EnumHandling.PreventSubsequent;
        if (work.Stop(byEntity, secondsUsed, job.Stage.Seconds) && byEntity.World.Side == EnumAppSide.Server) hull.Lay(byEntity, slot);
    }

    public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason, ref EnumHandling handling)
    {
        work.Stop(byEntity);
        return true;
    }

    private static EntityBehaviorFraming? Hull(EntitySelection? entitySel) => entitySel?.Entity?.GetBehavior<EntityBehaviorFraming>();
}
