using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace RoughSeas.Shipyard;

/// <summary>What a vessel being built, or laths laid out for one, takes from the hand: held on it, it lays the next piece.</summary>
public class CollectibleBehaviorFraming(CollectibleObject collectible) : CollectibleBehavior(collectible)
{
    public const string Code = "roughseas:framing";

    private readonly HeldWork work = new();

    /// <summary>Whether the held item does work on what is selected, refused or not.</summary>
    public static bool WorksOn(ItemSlot slot, EntityAgent byEntity, BlockSelection? blockSel, EntitySelection? entitySel) => Hull(byEntity, blockSel, entitySel)?.WorkWith(slot) != null;

    /// <summary>Whether the entity selected is a vessel built piece by piece, being built or finished.</summary>
    public static bool Builds(EntitySelection? entitySel) => entitySel?.Entity?.SidedProperties?.Behaviors.OfType<IWorkedHull>().Any() == true;

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (Hull(byEntity, blockSel, entitySel)?.WorkWith(slot) is not { } job) return;

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
        if (Hull(byEntity, blockSel, entitySel)?.WorkWith(slot) is not { Refusal: null } job) return false;

        handling = EnumHandling.PreventSubsequent;
        return work.Step(secondsUsed, job.Seconds);
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    {
        if (Hull(byEntity, blockSel, entitySel) is not { } hull || hull.WorkWith(slot) is not { Refusal: null } job)
        {
            work.Stop(byEntity);
            return;
        }

        handling = EnumHandling.PreventSubsequent;
        if (work.Stop(byEntity, secondsUsed, job.Seconds) && byEntity.World.Side == EnumAppSide.Server) hull.Lay(byEntity, slot);
    }

    public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason, ref EnumHandling handling)
    {
        work.Stop(byEntity);
        return true;
    }

    private static IWorkedHull? Hull(EntityAgent byEntity, BlockSelection? blockSel, EntitySelection? entitySel) =>
        entitySel?.Entity?.SidedProperties?.Behaviors.OfType<IWorkedHull>().FirstOrDefault()
        ?? (blockSel == null ? null : byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as IWorkedHull);
}
