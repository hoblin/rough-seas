using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace RoughSeas.Shipyard;

/// <summary>
/// A firestarter or a torch held on a vessel's laid fire lights it as vanilla lights a firepit: held its seconds,
/// a firestarter taking on one hold in four, with vanilla's animation, sound and sparks.
/// </summary>
public class CollectibleBehaviorKindle(CollectibleObject collectible) : CollectibleBehavior(collectible)
{
    public const string Code = "roughseas:kindle";

    /// <summary>BEFirepit.GetIgnitableState.</summary>
    private const float IgniteSeconds = 3;
    private const float ClientSlackSeconds = 0.2f;
    /// <summary>ItemFirestarter.OnHeldInteractStop.</summary>
    private const double FirestarterCatches = 0.25;
    private const float SparksAfterSeconds = 0.25f;
    private const float SparksPerSecond = 30;
    private const int DrillSoundDelayMs = 500;
    private const string DrillSoundKey = "firestartersound";

    private static readonly AssetLocation FireBlock = new("game:fire");
    private static readonly AssetLocation DrillSound = new("game:sounds/player/handdrill");
    private static readonly AssetLocation TorchSound = new("game:sounds/torch-ignite");

    private bool Drilled => collectible is ItemFirestarter;

    private string? IgniteAnimation => collectible.Attributes?["igniteAnimation"].AsString();

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (!firstEvent || Blank(entitySel) is not { CanLight: true }) return;

        handling = EnumHandling.PreventDefault;
        handHandling = EnumHandHandling.PreventDefault;
        var player = (byEntity as EntityPlayer)?.Player;
        if (!Drilled)
        {
            byEntity.World.PlaySoundAt(TorchSound, byEntity, player, false, 16);
            return;
        }
        if (IgniteAnimation is { } animation) byEntity.AnimManager.StartAnimation(animation);
        if (byEntity.Api is not ICoreClientAPI capi) return;

        StopDrillSound(capi);
        capi.ObjectCache[DrillSoundKey] = capi.Event.RegisterCallback(_ => byEntity.World.PlaySoundAt(DrillSound, byEntity, player, false, 16), DrillSoundDelayMs);
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    {
        if (Blank(entitySel) is not { CanLight: true }) return false;

        handling = EnumHandling.PreventDefault;
        if (byEntity.World is not IClientWorldAccessor world) return true;

        if (secondsUsed > SparksAfterSeconds && (int)(SparksPerSecond * secondsUsed) % 2 == 1 && world.GetBlock(FireBlock)?.ParticleProperties is { Length: > 0 } kinds)
        {
            var sparks = kinds[^1].Clone();
            sparks.basePos = entitySel.Position.Clone().Add(entitySel.HitPosition);
            sparks.Quantity.avg = Drilled ? 0.3f : 0.5f;
            world.SpawnParticles(sparks, (byEntity as EntityPlayer)?.Player);
        }
        return secondsUsed <= IgniteSeconds + ClientSlackSeconds;
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    {
        Quit(byEntity);
        if (Blank(entitySel) is not { CanLight: true } blank) return;

        handling = EnumHandling.PreventDefault;
        if (byEntity.World.Side != EnumAppSide.Server || secondsUsed < IgniteSeconds) return;

        if (Drilled && byEntity.World.Rand.NextDouble() >= FirestarterCatches)
        {
            blank.Fails(byEntity, secondsUsed);
            return;
        }
        if (Drilled) collectible.DamageItem(byEntity.World, byEntity, slot);
        blank.Light(byEntity);
    }

    public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason, ref EnumHandling handled)
    {
        Quit(byEntity);
        return true;
    }

    public static bool Lights(EntitySelection? entitySel) => Blank(entitySel) is { CanLight: true };

    private void Quit(EntityAgent byEntity)
    {
        if (!Drilled) return;

        if (IgniteAnimation is { } animation) byEntity.AnimManager.StopAnimation(animation);
        if (byEntity.Api is ICoreClientAPI capi) StopDrillSound(capi);
    }

    private static void StopDrillSound(ICoreClientAPI capi) => capi.Event.UnregisterCallback(ObjectCacheUtil.TryGet<long>(capi, DrillSoundKey));

    private static EntityBehaviorBurnout? Blank(EntitySelection? entitySel) => entitySel?.Entity?.GetBehavior<EntityBehaviorBurnout>();
}
