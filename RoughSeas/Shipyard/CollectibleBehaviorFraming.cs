using System.Collections.Generic;
using System.Linq;
using RoughSeas.Vessels;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace RoughSeas.Shipyard;

/// <summary>What a vessel being built or mended, or a pile of laths that starts one, takes from the hand: held on it, it lays the next piece or the patch.</summary>
public class CollectibleBehaviorFraming(CollectibleObject collectible) : CollectibleBehavior(collectible)
{
    public const string Code = "roughseas:framing";

    /// <summary>Vanilla's work with both hands at something on the ground, as it scrapes a hide there.</summary>
    public const string WorkAnimation = "knifescrape";

    private readonly HeldWork work = new();

    /// <summary>Whether the held item does work on what is selected, refused or not.</summary>
    public static bool WorksOn(ItemSlot slot, EntityAgent byEntity, BlockSelection? blockSel, EntitySelection? entitySel) => Work(slot, byEntity, blockSel, entitySel) != null;

    /// <summary>Whether the entity selected is a vessel built piece by piece, being built or finished.</summary>
    public static bool Builds(EntitySelection? entitySel) => entitySel?.Entity?.SidedProperties?.Behaviors.OfType<IWorkedHull>().Any() == true;

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (Work(slot, byEntity, blockSel, entitySel) is not { Job: var job }) return;

        handling = EnumHandling.PreventSubsequent;
        handHandling = EnumHandHandling.PreventDefault;
        if (job.Refusal != null)
        {
            if (firstEvent) byEntity.Api.ModLoader.GetModSystem<ShipyardModSystem>().Log("{0} refused at {1}: {2}", byEntity.GetName(), byEntity.Pos.AsBlockPos, job.Refusal);
            (byEntity.Api as ICoreClientAPI)?.TriggerIngameError(this, job.Refusal, Lang.Get("roughseas:" + job.Refusal, job.RefusalArgs));
            handHandling = EnumHandHandling.PreventDefaultAction;
            return;
        }
        work.Start(byEntity, job.ShowsBar);
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    {
        if (Work(slot, byEntity, blockSel, entitySel) is not { Job.Refusal: null } held) return false;

        handling = EnumHandling.PreventSubsequent;
        held.Hull.Working(byEntity);
        return work.Step(secondsUsed, held.Job.Seconds);
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    {
        if (Work(slot, byEntity, blockSel, entitySel) is not { Job.Refusal: null } held)
        {
            work.Stop(byEntity);
            return;
        }

        handling = EnumHandling.PreventSubsequent;
        if (work.Stop(byEntity, secondsUsed, held.Job.Seconds) && byEntity.World.Side == EnumAppSide.Server) held.Hull.Lay(byEntity, slot);
    }

    public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason, ref EnumHandling handling)
    {
        work.Stop(byEntity);
        return true;
    }

    public override string? GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity forEntity, ref EnumHandling bhHandling)
    {
        if (forEntity is not EntityPlayer { Controls.HandUse: EnumHandInteract.HeldItemInteract, Player: { } player } worker
            || Work(activeHotbarSlot, worker, player.CurrentBlockSelection, player.CurrentEntitySelection) is not { Job.Refusal: null }) return null;

        bhHandling = EnumHandling.PreventDefault;
        return WorkAnimation;
    }

    private static Held? Work(ItemSlot slot, EntityAgent byEntity, BlockSelection? blockSel, EntitySelection? entitySel) =>
        Hulls(byEntity, blockSel, entitySel).Select(hull => hull.WorkWith(slot) is { } job ? new Held(hull, job) : null).FirstOrDefault(held => held != null);

    private static IEnumerable<IWorkedHull> Hulls(EntityAgent byEntity, BlockSelection? blockSel, EntitySelection? entitySel)
    {
        if (entitySel?.Entity is { SidedProperties.Behaviors: { } behaviors } vessel && !(byEntity.Controls.ShiftKey && PickedUp(vessel)))
        {
            foreach (var built in behaviors.OfType<IWorkedHull>()) yield return built;
            foreach (var mended in behaviors.OfType<IMendedHull>()) yield return new Mending(mended, byEntity, entitySel.HitPosition);
        }
        if (blockSel != null && byEntity.World.BlockAccessor.GetBlock(blockSel.Position).GetInterface<IWorkedBlock>(byEntity.World, blockSel.Position)?.HullAt(byEntity.World, blockSel.Position) is { } block)
        {
            yield return block;
        }
    }

    private static bool PickedUp(Entity vessel) => vessel.Properties.Attributes?["rightClickPickup"].AsBool() == true;

    private sealed record Held(IWorkedHull Hull, HullWork Job);

    private sealed class Mending(IMendedHull hull, EntityAgent mender, Vec3d hit) : IWorkedHull
    {
        public HullWork? WorkWith(ItemSlot slot) =>
            hull.MendWith(slot, mender, hit) is { } mend ? new HullWork(mend.Seconds, mend.Refusal) { RefusalArgs = mend.RefusalArgs } : null;

        public void Lay(EntityAgent byEntity, ItemSlot slot) => hull.Mend(byEntity, slot, hit);
    }
}
