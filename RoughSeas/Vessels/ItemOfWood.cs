using System.Linq;
using Vintagestory.API.Common;

namespace RoughSeas.Vessels;

public class ItemOfWood : Item
{
    public const string ClassName = "roughseas:ItemOfWood";

    public override string GetHeldItemName(ItemStack itemStack) => Wood.NameOf(this) ?? base.GetHeldItemName(itemStack);

    public override void OnCreatedByCrafting(ItemSlot[] allInputslots, ItemSlot outputSlot, IRecipeBase byRecipe)
    {
        base.OnCreatedByCrafting(allInputslots, outputSlot, byRecipe);
        if (outputSlot.Itemstack is not { } made || made.Collectible.GetTransitionableProperties(api.World, made, null)?.Any(props => props.Type == EnumTransitionType.Dry) != true) return;

        var dried = allInputslots.Where(slot => !slot.Empty)
            .Select(slot => slot.Itemstack.Collectible.UpdateAndGetTransitionState(api.World, new DummySlot(slot.Itemstack.Clone()), EnumTransitionType.Dry))
            .OfType<TransitionState>()
            .Select(state => state.TransitionedHours)
            .DefaultIfEmpty()
            .Max();
        if (dried > 0) made.Collectible.SetTransitionState(made, EnumTransitionType.Dry, dried);
    }
}
