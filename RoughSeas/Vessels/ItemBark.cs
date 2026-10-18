using Vintagestory.API.Common;

namespace RoughSeas.Vessels;

public class ItemBark : Item
{
    public const string ClassName = "roughseas:ItemBark";

    public override string GetHeldItemName(ItemStack itemStack) => Wood.NameOf(this) ?? base.GetHeldItemName(itemStack);
}
