using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RoughSeas.Vessels;

public class ItemVessel : ItemBoat
{
    public const string ClassName = "roughseas:ItemVessel";

    private ItemStack? placing;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        if (api is ICoreServerAPI sapi) sapi.Event.OnEntitySpawn += OnEntitySpawn;
    }

    public override string GetHeldItemName(ItemStack itemStack) => Wood.NameOf(this) ?? base.GetHeldItemName(itemStack);

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
    {
        placing = slot.Itemstack?.Clone();
        try
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
        }
        finally
        {
            placing = null;
        }
    }

    private void OnEntitySpawn(Entity entity)
    {
        // SpawnEntity raises this before it returns, so while placing is set the spawned vessel is the one placed.
        if (placing == null || entity.Code != Code) return;

        entity.GetBehavior<EntityBehaviorVesselIntegrity>()?.TakeConditionFrom(placing);
    }
}
