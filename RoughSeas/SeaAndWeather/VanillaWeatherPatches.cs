using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.SeaAndWeather;

public static class VanillaWeatherPatches
{
    private const string HarmonyId = "roughseas.seaandweather";

    private static readonly ConcurrentDictionary<EnumAppSide, SeaWeatherSystem> systems = [];
    private static readonly object patchLock = new();
    private static Harmony? harmony;

    public static void Attach(SeaWeatherSystem system, EnumAppSide side)
    {
        systems[side] = system;
        lock (patchLock)
        {
            if (harmony != null) return;
            harmony = new Harmony(HarmonyId);
            Postfix(AccessTools.Method(typeof(WeatherSimulationRegion), nameof(WeatherSimulationRegion.GetWindSpeed)), nameof(RegionWindSpeed));
            Prefix(AccessTools.Method(typeof(WeatherSystemBase), nameof(WeatherSystemBase.GetRainCloudness)), nameof(RainCloudness));
            Prefix(AccessTools.Method(typeof(WeatherSimulationRegion), nameof(WeatherSimulationRegion.RandomWeatherEvent)), nameof(NoWeatherEvent));
            Postfix(AccessTools.Method(typeof(WeatherDataReaderBase), nameof(WeatherDataReaderBase.LoadLerp),
                [typeof(Vec3d), typeof(bool), typeof(float), typeof(float), typeof(float)]), nameof(UnloadedRainCloudness));
            Postfix(AccessTools.Method(typeof(CloudRenderer), nameof(CloudRenderer.CloudTick)), nameof(CloudDrift));
            Postfix(AccessTools.Method(typeof(FluffyClouds.CloudRendererMap), nameof(FluffyClouds.CloudRendererMap.CloudTick)), nameof(CloudDrift));
            WindAsStrength(AccessTools.Method(typeof(DefaultShaderUniforms), nameof(DefaultShaderUniforms.Update)), nameof(SurfaceWindStrength), nameof(SurfaceWindRestore));
            WindAsStrength(AccessTools.Method(typeof(EntityBoat), nameof(EntityBoat.OnRenderFrame)), nameof(WindStrength), nameof(WindRestore));
        }
    }

    public static void Detach(EnumAppSide side)
    {
        systems.TryRemove(side, out _);
        lock (patchLock)
        {
            if (!systems.IsEmpty || harmony == null) return;
            harmony.UnpatchAll(HarmonyId);
            harmony = null;
        }
    }

    private static void Postfix(System.Reflection.MethodBase target, string patch) =>
        harmony!.Patch(target, postfix: new HarmonyMethod(typeof(VanillaWeatherPatches), patch));

    private static void Prefix(System.Reflection.MethodBase target, string patch) =>
        harmony!.Patch(target, prefix: new HarmonyMethod(typeof(VanillaWeatherPatches), patch));

    private static void WindAsStrength(System.Reflection.MethodBase target, string prefix, string postfix) =>
        harmony!.Patch(target,
            prefix: new HarmonyMethod(typeof(VanillaWeatherPatches), prefix),
            postfix: new HarmonyMethod(typeof(VanillaWeatherPatches), postfix));

    private static SeaWeatherSystem? On(EnumAppSide side) => systems.GetValueOrDefault(side);

    private static void RegionWindSpeed(double posY, ref double __result, WeatherSystemBase ___ws, int ___regionX, int ___regionZ)
    {
        var sea = On(___ws.api.Side);
        if (sea == null) return;
        using var _ = sea.Budget.Measure();
        var regionSize = ___ws.api.World.BlockAccessor.RegionSize;
        var (x, z) = ___ws.api is ICoreClientAPI { World.Player.Entity: { } player }
            ? (player.Pos.X, player.Pos.Z)
            : ((___regionX + 0.5) * regionSize, (___regionZ + 0.5) * regionSize);
        __result = VanillaWeather.Strength(sea.Wind(x, z).Speed, posY, ___ws.api.World.SeaLevel, sea.Config.Vanilla);
    }

    private static bool RainCloudness(double posX, double posZ, double totalDays, ref float __result, WeatherSystemBase __instance)
    {
        var sea = On(__instance.api.Side);
        if (sea == null || __instance.OverridePrecipitation != null) return true;
        __result = sea.RainCloudness(posX, posZ, totalDays);
        return false;
    }

    private static bool NoWeatherEvent(WeatherSimulationRegion __instance, ref WeatherEvent __result)
    {
        var calm = Array.Find(__instance.WeatherEvents, weatherEvent => weatherEvent.config.Code == VanillaWeather.CalmEvent);
        if (calm == null) return true;
        __result = calm;
        return false;
    }

    private static void UnloadedRainCloudness(Vec3d pos, bool useArgValues, WeatherDataReaderBase __instance, WeatherSystemBase ___ws, IMapRegion ___hereMapRegion)
    {
        var sea = On(___ws.api.Side);
        if (sea == null || useArgValues || ___hereMapRegion?.ClimateMap != null || ___ws.OverridePrecipitation != null) return;
        var cloudness = sea.RainCloudness(pos.X, pos.Z, ___ws.api.World.Calendar.TotalDays);
        __instance.lerpRainCloudOverlay = GameMath.Clamp(cloudness, 0, 1);
        __instance.lerpRainOverlay = GameMath.Clamp(cloudness - 0.5f, 0, 1);
    }

    // These vanilla readers take the wind's X alone as its strength; our wind may blow along Z.
    private static void SurfaceWindStrength(out float __state) => __state = Magnitude(GlobalConstants.CurrentSurfaceWindSpeedClient);

    private static void SurfaceWindRestore(float __state) => GlobalConstants.CurrentSurfaceWindSpeedClient.X = __state;

    private static void WindStrength(out float __state) => __state = Magnitude(GlobalConstants.CurrentWindSpeedClient);

    private static void WindRestore(float __state) => GlobalConstants.CurrentWindSpeedClient.X = __state;

    private static float Magnitude(Vec3f wind)
    {
        var x = wind.X;
        wind.X = MathF.Sqrt(wind.X * wind.X + wind.Z * wind.Z);
        return x;
    }

    private static void CloudDrift(ICoreClientAPI ___capi, ref float ___cloudSpeedX, ref float ___cloudSpeedZ)
    {
        var sea = On(EnumAppSide.Client);
        var player = ___capi.World.Player?.Entity;
        if (sea == null || player == null) return;
        var wind = sea.Wind(player.Pos.X, player.Pos.Z);
        var perMps = (float)sea.Config.Vanilla.CloudSpeedPerMps;
        ___cloudSpeedX = (float)wind.X * perMps;
        ___cloudSpeedZ = (float)wind.Z * perMps;
    }
}
