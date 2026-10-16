using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace RoughSeas.Storms;

public class BlockBehaviorBailer(Block block) : BlockBehavior(block)
{
    public const string Code = "roughseas:bailer";

    private const string ScoopsKey = "roughseas:scoops";
    private static readonly AssetLocation PourSound = new("game:sounds/effect/water-pour");

    private double secondsPerScoop;

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);
        secondsPerScoop = properties["secondsPerScoop"].AsDouble(1);
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (!firstEvent || !Empty(slot) || Bilge(byEntity, entitySel) is not { Litres: > 0 }) return;

        byEntity.Attributes.SetInt(ScoopsKey, 0);
        handling = EnumHandling.PreventDefault;
        handHandling = EnumHandHandling.PreventDefault;
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    {
        if (Bilge(byEntity, entitySel) is not { Litres: > 0 } bilge) return false;

        handling = EnumHandling.PreventDefault;
        if (byEntity.World.Side != EnumAppSide.Server) return true;

        var scoops = (int)(secondsUsed / secondsPerScoop);
        var done = byEntity.Attributes.GetInt(ScoopsKey);
        if (scoops <= done) return true;

        byEntity.Attributes.SetInt(ScoopsKey, scoops);
        bilge.Bail(((BlockLiquidContainerBase)block).CapacityLitres * (scoops - done));
        byEntity.World.PlaySoundAt(PourSound, byEntity, null, true, 16);
        return bilge.Litres > 0;
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    {
        if (Bilge(byEntity, entitySel) != null) handling = EnumHandling.PreventDefault;
    }

    private bool Empty(ItemSlot slot) =>
        block is BlockLiquidContainerBase container && slot.Itemstack is { } stack && container.GetContent(stack) == null;

    private static EntityBehaviorBilge? Bilge(EntityAgent byEntity, EntitySelection? entitySel) =>
        entitySel?.Entity?.GetBehavior<EntityBehaviorBilge>() ?? byEntity.MountedOn?.Entity?.GetBehavior<EntityBehaviorBilge>();
}
