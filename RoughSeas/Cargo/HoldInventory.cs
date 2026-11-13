using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Cargo;

/// <summary>
/// A hold's slots: as many as its config gives, and more while they hold what an old save stowed in it; each extra
/// slot goes once it is empty.
/// </summary>
public sealed class HoldInventory(int size, string inventoryId, ICoreAPI api) : InventoryGeneric(size, inventoryId, api)
{
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

    public bool Settle()
    {
        int kept = slots.Length;
        while (kept > size && slots[kept - 1].Empty) kept--;
        if (kept == slots.Length) return false;

        slots = slots[..kept];
        return true;
    }

    private void Resize(int count)
    {
        if (count > slots.Length) AddSlots(count - slots.Length);
        else slots = slots[..count];
    }
}
