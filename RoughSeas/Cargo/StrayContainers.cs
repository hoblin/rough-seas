using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace RoughSeas.Cargo;

/// <summary>
/// Accessories saved in slots a vessel no longer has, from a version before her holds: a stack moves to the slot that
/// takes it now, and a container is emptied into her holds, its contents and then the container itself.
/// </summary>
public static class StrayContainers
{
    private const string AccessoriesKey = "wearablesInv";
    private const string AccessoriesBehaviour = "rideableaccessories";

    [ReliesOn("Vintagestory.GameContent.EntityBehaviorAttachable", "Initialize", "loads the tree wearablesInv into as many slots as its config's wearableSlots lists, each taking its forCategoryCodes")]
    [ReliesOn("Vintagestory.API.Common.InventoryGeneric", "FromTreeAttributes", "reads only as many saved slots as it has, and drops the rest")]
    public static void Stow(Entity vessel, EntityProperties properties, Action<ItemStack> stow, CargoModSystem cargo)
    {
        if (vessel.WatchedAttributes.GetTreeAttribute(AccessoriesKey) is not { } saved) return;
        var slots = Slots(properties);
        var stacks = saved.GetTreeAttribute("slots");
        if (slots == null || stacks == null) return;

        var kept = new TreeAttribute();
        var stray = new List<ItemStack>();
        foreach (var key in stacks.Select(entry => entry.Key).ToArray())
        {
            if (stacks.GetItemstack(key) is not { } stack || !stack.ResolveBlockOrItem(vessel.World)) continue;
            int at = int.Parse(key);
            if (at < slots.Length && slots[at].Contains(Category(stack))) kept.SetItemstack(key, stack);
            else stray.Add(stack);
        }
        if (stray.Count == 0 && saved.GetInt("qslots") == slots.Length) return;

        foreach (var stack in stray)
        {
            int berth = Enumerable.Range(0, slots.Length).FirstOrDefault(at => slots[at].Contains(Category(stack)) && !kept.HasAttribute(at.ToString()), -1);
            if (berth >= 0)
            {
                kept.SetItemstack(berth.ToString(), stack);
                cargo.Log(vessel, "{0} moved to the accessory slot that takes it now", stack.Collectible.Code);
                continue;
            }

            if (stack.Collectible.GetCollectibleInterface<IHeldBag>() is { } bag)
            {
                var contents = bag.GetContents(stack, vessel.World).Where(content => content != null).ToArray();
                foreach (var content in contents) stow(content);
                bag.Clear(stack);
                cargo.Log(vessel, "{0} emptied into her hold: {1} stacks", stack.Collectible.Code, contents.Length);
            }
            stow(stack);
            cargo.Log(vessel, "{0} stowed in her hold: its slot is gone", stack.Collectible.Code);
        }

        saved["slots"] = kept;
        saved.SetInt("qslots", slots.Length);
        vessel.WatchedAttributes.MarkPathDirty(AccessoriesKey);
    }

    private static string[][]? Slots(EntityProperties properties) =>
        properties.Server?.BehaviorsAsJsonObj?
            .FirstOrDefault(behaviour => behaviour["code"].AsString() == AccessoriesBehaviour)?["wearableSlots"].AsArray()
            .Select(slot => slot["forCategoryCodes"].AsArray<string>() ?? [])
            .ToArray();

    private static string? Category(ItemStack stack) => IAttachableToEntity.FromCollectible(stack.Collectible)?.GetCategoryCode(stack);
}
