using System.Collections.Generic;
using HarmonyLib;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace RoughSeas.Storms;

/// <summary>
/// Stores the rope manager's next rope id on save and reads it back on load, which vanilla swaps, and never hands out
/// an id a loaded rope holds: an old world's counter starts low.
/// </summary>
public static class RopeIds
{
    private const string HarmonyId = "roughseas.storms.ropeids";
    private const string SaveKey = "nextClothId";

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
        if (___sapi.WorldManager.SaveGame.GetData(SaveKey) is { } data) ___nextClothId = SerializerUtil.Deserialize<int>(data);
        return false;
    }

    private static void Skip(ref int ___nextClothId, Dictionary<int, ClothSystem> ___clothSystems)
    {
        while (___clothSystems.ContainsKey(___nextClothId)) ___nextClothId++;
    }
}
