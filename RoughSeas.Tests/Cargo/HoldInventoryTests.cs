using RoughSeas.Cargo;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Tests.Cargo;

public class HoldInventoryTests
{
    [Fact]
    public void KeepsItsOwnSizeWhenNothingIsStowedBeyondIt()
    {
        var hold = Hold(2);
        hold.Settle();

        Assert.Equal(2, hold.Count);
    }

    [Fact]
    public void GivesASpareSlotBeyondItsSize()
    {
        var hold = Hold(2);
        hold.Spare().Itemstack = Ingot();

        Assert.Equal(3, hold.Count);
    }

    [Fact]
    public void DropsEachSpareSlotOnceItIsEmptyWhereverItLies()
    {
        var hold = Hold(2);
        hold.Spare().Itemstack = Ingot();
        hold.Spare().Itemstack = Ingot();
        hold[2].Itemstack = null;
        hold.Settle();

        Assert.Equal(3, hold.Count);
        Assert.False(hold[2].Empty);
    }

    [Fact]
    public void NeverDropsASlotOfItsOwnSize()
    {
        var hold = Hold(2);
        hold.Spare();
        hold.Settle();

        Assert.Equal(2, hold.Count);
        Assert.True(hold[0].Empty && hold[1].Empty);
    }

    [Fact]
    public void LoadsAsManySlotsAsWereSaved()
    {
        var saved = Hold(2);
        saved.Spare().Itemstack = Ingot();
        var tree = new TreeAttribute();
        saved.ToTreeAttributes(tree);

        var loaded = Hold(2);
        loaded.FromTreeAttributes(tree);

        Assert.Equal(3, loaded.Count);
    }

    [Fact]
    public void DropsASpareSlotOnceAWholeStackIsTakenFromIt()
    {
        var hold = Hold(2);
        hold.Spare().Itemstack = Ingot();
        var hand = new DummySlot();

        var op = Click(EnumMouseButton.Left);
        hold[2].ActivateSlot(hand, ref op);

        Assert.Equal(2, hold.Count);
        Assert.Equal("game:ingot-copper", hand.Itemstack?.Collectible.Code.ToString());
    }

    [Fact]
    public void DropsASpareSlotOnceItsLastItemIsTakenByHalves()
    {
        var hold = Hold(2);
        hold.Spare().Itemstack = new ItemStack(Ingot().Collectible);
        var hand = new DummySlot();

        var op = Click(EnumMouseButton.Right);
        hold[2].ActivateSlot(hand, ref op);

        Assert.Equal(2, hold.Count);
        Assert.Equal(1, hand.StackSize);
    }

    private static ItemStackMoveOperation Click(EnumMouseButton button) => new(null, button, 0, EnumMergePriority.AutoMerge);

    private static HoldInventory Hold(int size) => new(size, "roughseashold0-1", null!);

    private static ItemStack Ingot() => new(new Item { Code = new AssetLocation("game:ingot-copper"), MaxStackSize = 16 });
}
