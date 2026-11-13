using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Cargo;

/// <summary>
/// A hold's slots: as many as its config gives, and more while they hold what an old save stowed in it; each extra
/// slot goes once it is empty.
/// </summary>
public sealed class HoldInventory : InventoryGeneric
{
    private readonly int size;

    public HoldInventory(int size, string inventoryId, ICoreAPI api) : base(size, inventoryId, api)
    {
        this.size = size;
        SlotModified += _ => Settle();
    }

    public override void FromTreeAttributes(ITreeAttribute treeAttribute)
    {
        Resize(Math.Max(size, treeAttribute?.GetInt("qslots") ?? 0));
        base.FromTreeAttributes(treeAttribute);
    }

    public ItemSlot Spare()
    {
        AddSlots(1);
        return slots[^1];
    }

    public void Settle()
    {
        if (slots.Length > size) slots = [.. slots[..size], .. slots[size..].Where(slot => !slot.Empty)];
    }

    private void Resize(int count)
    {
        if (count > slots.Length) AddSlots(count - slots.Length);
        else slots = slots[..count];
    }
}
