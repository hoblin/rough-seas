using System.Collections.Generic;
using HarmonyLib;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace RoughSeas.Storms;

/// <summary>
/// Keeps the rope manager's next rope id across saves, which vanilla swaps (it stores on load and reads back on save,
/// so every save rewound it), and never hands out an id a loaded rope holds. A world saved without our key had its
/// counter rewound at every save, so its ropes hold small ids, some in regions not loaded yet: it starts past them.
/// </summary>
[ReliesOn("Vintagestory.GameContent.ClothManager", "Event_GameWorldSave", "the rope id counter's save and load are swapped, the bug these prefixes replace")]
[ReliesOn("Vintagestory.GameContent.ClothManager", "Event_SaveGameLoaded", "the rope id counter's save and load are swapped, the bug these prefixes replace")]
[ReliesOn("Vintagestory.GameContent.ClothManager", "RegisterCloth", "takes a new rope's id from nextClothId and stores it in clothSystems by that id")]
public static class RopeIds
{
    private const string HarmonyId = "roughseas.storms.ropeids";
    private const string SaveKey = "roughseas:nextClothId";
    private const int PastRewoundIds = 1 << 20;

    private static Harmony? harmony;

    public static void Attach()
    {
        if (harmony != null) return;

        harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.Method(typeof(ClothManager), "Event_GameWorldSave"), prefix: new HarmonyMethod(typeof(RopeIds), nameof(Store)));
        harmony.Patch(AccessTools.Method(typeof(ClothManager), "Event_SaveGameLoaded"), prefix: new HarmonyMethod(typeof(RopeIds), nameof(Load)));
        harmony.Patch(AccessTools.Method(typeof(ClothManager), nameof(ClothManager.RegisterCloth)), prefix: new HarmonyMethod(typeof(RopeIds), nameof(Skip)));
    }

    public static void Detach()
    {
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
    }

    private static bool Store(ICoreServerAPI ___sapi, int ___nextClothId)
    {
        ___sapi.WorldManager.SaveGame.StoreData(SaveKey, SerializerUtil.Serialize(___nextClothId));
        return false;
    }

    private static bool Load(ICoreServerAPI ___sapi, ref int ___nextClothId)
    {
        ___nextClothId = ___sapi.WorldManager.SaveGame.GetData(SaveKey) is { } data ? SerializerUtil.Deserialize<int>(data) : PastRewoundIds;
        return false;
    }

    private static void Skip(ref int ___nextClothId, Dictionary<int, ClothSystem> ___clothSystems)
    {
        while (___clothSystems.ContainsKey(___nextClothId)) ___nextClothId++;
    }
}
