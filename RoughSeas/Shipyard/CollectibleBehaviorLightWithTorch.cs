using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace RoughSeas.Shipyard;

/// <summary>A torch, or anything else vanilla lights fires with by <c>BlockBehaviorCanIgnite</c>: it always takes.</summary>
public class CollectibleBehaviorLightWithTorch(CollectibleObject collectible) : CollectibleBehaviorLight(collectible)
{
    public const string Code = "roughseas:lightwithtorch";

    private static readonly AssetLocation IgniteSound = new("game:sounds/torch-ignite");

    /// <summary>BlockBehaviorCanIgnite.OnHeldInteractStep.</summary>
    protected override float Sparks => 0.5f;

    protected override void Begin(EntityAgent byEntity) =>
        byEntity.World.PlaySoundAt(IgniteSound, byEntity, (byEntity as EntityPlayer)?.Player, false, 16);
}
