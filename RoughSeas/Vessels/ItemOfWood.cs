using Vintagestory.API.Common;

namespace RoughSeas.Vessels;

public class ItemOfWood : Item
{
    public const string ClassName = "roughseas:ItemOfWood";

    public override string GetHeldItemName(ItemStack itemStack) => Wood.NameOf(this) ?? base.GetHeldItemName(itemStack);
}
