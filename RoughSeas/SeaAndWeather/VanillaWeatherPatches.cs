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

    private static void WindAsStrength(System.Reflection.MethodBase target, string prefix, string postfix) =>
        harmony!.Patch(target,
            prefix: new HarmonyMethod(typeof(VanillaWeatherPatches), prefix),
            postfix: new HarmonyMethod(typeof(VanillaWeatherPatches), postfix));

    private static SeaWeatherSystem? On(EnumAppSide side) => systems.GetValueOrDefault(side);

    private static void RegionWindSpeed(double posY, ref double __result, WeatherSystemBase ___ws, int ___regionX, int ___regionZ)
    {
        var sea = On(___ws.api.Side);
        if (sea == null) return;
        var began = sea.Budget.Begin();
        var regionSize = ___ws.api.World.BlockAccessor.RegionSize;
        var wind = sea.Wind((___regionX + 0.5) * regionSize, (___regionZ + 0.5) * regionSize);
        __result = VanillaWeather.Strength(wind.Speed, posY, ___ws.api.World.SeaLevel, sea.Config.Vanilla);
        sea.Budget.End(began);
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
