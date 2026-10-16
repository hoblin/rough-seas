using System;
using System.Collections.Generic;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace RoughSeas.SeaAndWeather;

public static class VanillaWeatherPatches
{
    private const string HarmonyId = "roughseas.seaandweather";

    private static readonly Dictionary<EnumAppSide, SeaWeatherSystem> systems = [];
    private static Harmony? harmony;

    public static void Attach(SeaWeatherSystem system, EnumAppSide side)
    {
        lock (systems)
        {
            systems[side] = system;
            if (harmony != null) return;
            harmony = new Harmony(HarmonyId);
            harmony.Patch(AccessTools.Method(typeof(WeatherSimulationRegion), nameof(WeatherSimulationRegion.GetWindSpeed)),
                postfix: new HarmonyMethod(typeof(VanillaWeatherPatches), nameof(RegionWindSpeed)));
            harmony.Patch(AccessTools.Method(typeof(DefaultShaderUniforms), nameof(DefaultShaderUniforms.Update)),
                prefix: new HarmonyMethod(typeof(VanillaWeatherPatches), nameof(ShaderWindStrength)),
                postfix: new HarmonyMethod(typeof(VanillaWeatherPatches), nameof(ShaderWindRestore)));
            harmony.Patch(AccessTools.Method(typeof(CloudRenderer), nameof(CloudRenderer.CloudTick)),
                postfix: new HarmonyMethod(typeof(VanillaWeatherPatches), nameof(CloudDrift)));
            harmony.Patch(AccessTools.Method(typeof(FluffyClouds.CloudRendererMap), nameof(FluffyClouds.CloudRendererMap.CloudTick)),
                postfix: new HarmonyMethod(typeof(VanillaWeatherPatches), nameof(CloudDrift)));
        }
    }

    public static void Detach(EnumAppSide side)
    {
        lock (systems)
        {
            systems.Remove(side);
            if (systems.Count > 0 || harmony == null) return;
            harmony.UnpatchAll(HarmonyId);
            harmony = null;
        }
    }

    private static SeaWeatherSystem? On(EnumAppSide side) => systems.GetValueOrDefault(side);

    private static void RegionWindSpeed(double posY, ref double __result, WeatherSystemBase ___ws, int ___regionX, int ___regionZ)
    {
        var sea = On(___ws.api.Side);
        if (sea == null) return;
        var regionSize = ___ws.api.World.BlockAccessor.RegionSize;
        var wind = sea.Wind((___regionX + 0.5) * regionSize, (___regionZ + 0.5) * regionSize);
        var mps = Math.Sqrt(wind.X * wind.X + wind.Z * wind.Z);
        __result = VanillaWeather.Strength(mps, posY, ___ws.api.World.SeaLevel, sea.Config.Vanilla);
    }

    private static void ShaderWindStrength(out float __state)
    {
        // Foliage and wave shaders read X alone as the wind's strength; our wind may blow along Z.
        var wind = GlobalConstants.CurrentSurfaceWindSpeedClient;
        __state = wind.X;
        wind.X = MathF.Sqrt(wind.X * wind.X + wind.Z * wind.Z);
    }

    private static void ShaderWindRestore(float __state) => GlobalConstants.CurrentSurfaceWindSpeedClient.X = __state;

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
