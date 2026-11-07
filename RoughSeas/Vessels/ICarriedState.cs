using Vintagestory.API.Common;

namespace RoughSeas.Vessels;

/// <summary>A vessel's state that stays with her on the item she is picked up as, and comes back when she is placed again.</summary>
public interface ICarriedState
{
    void Carry(ItemStack stack);

    void TakeFrom(ItemStack stack);
}
