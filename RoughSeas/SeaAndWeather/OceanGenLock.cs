using System;
using System.Threading;
using HarmonyLib;
using Vintagestory.ServerMods;

namespace RoughSeas.SeaAndWeather;

public static class OceanGenLock
{
    private const string HarmonyId = "roughseas.seaandweather.oceangen";

    private static readonly object patchLock = new();
    private static Harmony? harmony;
    private static MapLayerBase? locked;

    public static void Attach(MapLayerBase layer)
    {
        lock (patchLock)
        {
            locked = layer;
            if (harmony != null) return;
            harmony = new Harmony(HarmonyId);
            harmony.Patch(AccessTools.Method(layer.GetType(), nameof(MapLayerBase.GenLayer)),
                prefix: new HarmonyMethod(typeof(OceanGenLock), nameof(Enter)) { priority = Priority.Low },
                finalizer: new HarmonyMethod(typeof(OceanGenLock), nameof(Exit)) { priority = Priority.Low });
        }
    }

    public static void Detach()
    {
        lock (patchLock)
        {
            harmony?.UnpatchAll(HarmonyId);
            harmony = null;
            locked = null;
        }
    }

    private static void Enter(MapLayerBase __instance, out bool __state)
    {
        __state = ReferenceEquals(__instance, locked);
        if (__state) Monitor.Enter(__instance);
    }

    private static Exception? Exit(MapLayerBase __instance, bool __state, Exception? __exception)
    {
        if (__state) Monitor.Exit(__instance);
        return __exception;
    }
}
