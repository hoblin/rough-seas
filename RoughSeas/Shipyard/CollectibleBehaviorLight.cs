using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace RoughSeas.Shipyard;

/// <summary>
/// Held on a vessel's laid fire, lights it as vanilla lights a firepit: held its seconds, with the sparks, sound and
/// odds of what is held.
/// </summary>
[ReliesOn("Vintagestory.GameContent.BlockEntityFirepit", "GetIgnitableState", "a fire takes 3 seconds to light, the time copied here")]
[ReliesOn("Vintagestory.GameContent.BlockBehaviorCanIgnite", "OnHeldInteractStep", "the client's slack and the moment sparks start, copied here")]
public abstract class CollectibleBehaviorLight(CollectibleObject collectible) : CollectibleBehavior(collectible)
{
    /// <summary>BEFirepit.GetIgnitableState.</summary>
    private const float IgniteSeconds = 3;
    /// <summary>BlockBehaviorCanIgnite.OnHeldInteractStep.</summary>
    private const float ClientSlackSeconds = 0.2f;
    /// <summary>ItemFirestarter.OnHeldInteractStep, as BlockBehaviorCanIgnite's.</summary>
    private const float SparksAfterSeconds = 0.25f;
    private const float SparkFramesPerSecond = 30;

    private static readonly AssetLocation FireBlock = new("game:fire");

    protected abstract float Sparks { get; }

    public static bool CanLight(EntitySelection? entitySel) => Blank(entitySel) is { CanLight: true };

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (!firstEvent || !CanLight(entitySel)) return;

        handling = EnumHandling.PreventDefault;
        handHandling = EnumHandHandling.PreventDefault;
        Begin(byEntity);
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    {
        if (!CanLight(entitySel)) return false;

        handling = EnumHandling.PreventDefault;
        if (byEntity.World is not IClientWorldAccessor world) return true;

        if (secondsUsed > SparksAfterSeconds && (int)(SparkFramesPerSecond * secondsUsed) % 2 == 1 && world.GetBlock(FireBlock)?.ParticleProperties is { Length: > 0 } kinds)
        {
            var sparks = kinds[^1].Clone();
            sparks.basePos = entitySel.Position.Clone().Add(entitySel.HitPosition);
            sparks.Quantity.avg = Sparks;
            world.SpawnParticles(sparks, (byEntity as EntityPlayer)?.Player);
        }
        return secondsUsed <= IgniteSeconds + ClientSlackSeconds;
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    {
        End(byEntity);
        if (Blank(entitySel) is not { CanLight: true } blank) return;

        handling = EnumHandling.PreventDefault;
        if (byEntity.World.Side != EnumAppSide.Server || secondsUsed < IgniteSeconds) return;

        if (Catches(byEntity, slot)) blank.Light(byEntity);
        else blank.LogMisfire(byEntity, secondsUsed);
    }

    public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason, ref EnumHandling handled)
    {
        End(byEntity);
        return true;
    }

    protected abstract void Begin(EntityAgent byEntity);

    protected virtual void End(EntityAgent byEntity) { }

    protected virtual bool Catches(EntityAgent byEntity, ItemSlot slot) => true;

    private static EntityBehaviorBurnout? Blank(EntitySelection? entitySel) => entitySel?.Entity?.GetBehavior<EntityBehaviorBurnout>();
}
