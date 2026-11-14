using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Vintagestory.GameContent;

namespace RoughSeas.Lines;

[ReliesOn("Vintagestory.GameContent.ClothManager", "OnRenderFrame", "ticks and draws every rope in clothSystems on the client, whichever registered it")]
[ReliesOn("Vintagestory.GameContent.ClothSystem", "updateFixedStep", "is the only place a rope's points move")]
public static class VanillaRopes
{
    private const string HarmonyId = "roughseas.lines.ropes";

    private static readonly AccessTools.FieldRef<ClothManager, Dictionary<int, ClothSystem>> Systems = AccessTools.FieldRefAccess<ClothManager, Dictionary<int, ClothSystem>>("clothSystems");
    private static readonly object patchLock = new();
    private static int nextOwn = -1;
    private static int attached;
    private static Harmony? harmony;

    public static IEnumerable<ClothSystem> Tied(ClothManager cloths) => Systems(cloths).Values.Where(rope => rope.ClothId > 0);

    public static void Own(ClothManager cloths, ClothSystem rope)
    {
        rope.ClothId = nextOwn--;
        Systems(cloths)[rope.ClothId] = rope;
    }

    public static void Attach()
    {
        lock (patchLock)
        {
            if (attached++ > 0) return;
            harmony = new Harmony(HarmonyId);
            harmony.Patch(AccessTools.Method(typeof(ClothSystem), nameof(ClothSystem.updateFixedStep)),
                prefix: new HarmonyMethod(typeof(VanillaRopes), nameof(Stepped)));
        }
    }

    public static void Detach()
    {
        lock (patchLock)
        {
            if (--attached > 0 || harmony == null) return;
            harmony.UnpatchAll(HarmonyId);
            harmony = null;
        }
    }

    private static bool Stepped(ClothSystem __instance) => __instance.ClothId >= 0;
}
