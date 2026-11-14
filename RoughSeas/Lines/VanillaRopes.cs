using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Vintagestory.GameContent;

namespace RoughSeas.Lines;

[ReliesOn("Vintagestory.GameContent.ClothManager", "OnRenderFrame", "ticks and draws every rope in clothSystems on the client, whichever registered it")]
public static class VanillaRopes
{
    private static readonly AccessTools.FieldRef<ClothManager, Dictionary<int, ClothSystem>> Systems = AccessTools.FieldRefAccess<ClothManager, Dictionary<int, ClothSystem>>("clothSystems");
    private static int nextOwn = -1;

    public static IEnumerable<ClothSystem> Tied(ClothManager cloths) => Systems(cloths).Values.Where(rope => rope.ClothId > 0);

    public static void Own(ClothManager cloths, ClothSystem rope)
    {
        rope.ClothId = nextOwn--;
        Systems(cloths)[rope.ClothId] = rope;
    }
}
