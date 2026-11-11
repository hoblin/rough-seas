using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Util;

namespace RoughSeas.Shipyard;

/// <summary>A firestarter: drilled with vanilla's animation and sound, and taking on one hold in four.</summary>
[ReliesOn("Vintagestory.GameContent.ItemFirestarter", "OnHeldInteractStart", "the drill sound's delay and its ObjectCache key, which its cancel stops")]
public class CollectibleBehaviorLightWithFirestarter(CollectibleObject collectible) : CollectibleBehaviorLight(collectible)
{
    public const string Code = "roughseas:lightwithfirestarter";

    /// <summary>ItemFirestarter.OnHeldInteractStop.</summary>
    private const double Takes = 0.25;
    /// <summary>ItemFirestarter.OnHeldInteractStart.</summary>
    private const int DrillSoundDelayMs = 500;
    /// <summary>ItemFirestarter's own key, so that its cancel stops our sound too.</summary>
    private const string DrillSoundKey = "firestartersound";

    private static readonly AssetLocation DrillSound = new("game:sounds/player/handdrill");

    /// <summary>ItemFirestarter.OnHeldInteractStep.</summary>
    protected override float Sparks => 0.3f;

    private string? Animation => collectible.Attributes?["igniteAnimation"].AsString();

    protected override void Begin(EntityAgent byEntity)
    {
        if (Animation is { } animation) byEntity.AnimManager.StartAnimation(animation);
        if (byEntity.Api is not ICoreClientAPI capi) return;

        StopDrillSound(capi);
        capi.ObjectCache[DrillSoundKey] = capi.Event.RegisterCallback(
            _ => byEntity.World.PlaySoundAt(DrillSound, byEntity, (byEntity as EntityPlayer)?.Player, false, 16), DrillSoundDelayMs);
    }

    protected override void End(EntityAgent byEntity)
    {
        if (Animation is { } animation) byEntity.AnimManager.StopAnimation(animation);
        if (byEntity.Api is ICoreClientAPI capi) StopDrillSound(capi);
    }

    protected override bool Catches(EntityAgent byEntity, ItemSlot slot)
    {
        if (byEntity.World.Rand.NextDouble() >= Takes) return false;

        collectible.DamageItem(byEntity.World, byEntity, slot);
        return true;
    }

    private static void StopDrillSound(ICoreClientAPI capi) => capi.Event.UnregisterCallback(ObjectCacheUtil.TryGet<long>(capi, DrillSoundKey));
}
