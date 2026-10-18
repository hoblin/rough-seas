using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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

    private static readonly FieldInfo WorldgenRainfall = AccessTools.Field(typeof(ClimateCondition), nameof(ClimateCondition.WorldgenRainfall));
    private static readonly FieldInfo Rainfall = AccessTools.Field(typeof(ClimateCondition), nameof(ClimateCondition.Rainfall));
    private static readonly FieldInfo EventConfig = AccessTools.Field(typeof(WeatherEvent), nameof(WeatherEvent.config));
    private static readonly FieldInfo PatternWeight = AccessTools.Field(typeof(ConditionalPatternConfig), nameof(ConditionalPatternConfig.Weight));
    private static readonly FieldInfo HereChance = AccessTools.Field(typeof(WeatherEvent), "hereChance");
    private static readonly ConcurrentDictionary<EnumAppSide, SeaWeatherSystem> systems = [];
    private static readonly ConditionalWeakTable<WeatherDataReaderBase, StrongBox<double>> stormPresence = [];
    private static readonly object patchLock = new();
    private static Harmony? harmony;

    public static void Attach(SeaWeatherSystem system, EnumAppSide side)
    {
        systems[side] = system;
        lock (patchLock)
        {
            if (harmony != null) return;
            harmony = new Harmony(HarmonyId);
            Transpiler(AccessTools.Method(typeof(WeatherSimulationRegion), nameof(WeatherSimulationRegion.RandomWeatherEvent)), nameof(CurrentConditions));
            Postfix(AccessTools.Method(typeof(WeatherSystemBase), nameof(WeatherSystemBase.GetPrecipitation),
                [typeof(double), typeof(double), typeof(double), typeof(double)]), nameof(Precipitation));
            Postfix(AccessTools.Method(typeof(WeatherSystemBase), nameof(WeatherSystemBase.GetPrecipitation),
                [typeof(BlockPos), typeof(double), typeof(ClimateCondition)]), nameof(PrecipitationAt));
            var loadLerp = AccessTools.Method(typeof(WeatherDataReaderBase), nameof(WeatherDataReaderBase.LoadLerp),
                [typeof(Vec3d), typeof(bool), typeof(float), typeof(float), typeof(float)]);
            Postfix(loadLerp, nameof(UnloadedRainCloudness));
            Postfix(loadLerp, nameof(StormUnderCloud));
            Postfix(AccessTools.Method(typeof(WeatherDataReaderBase), "pgetBlendedCloudBrightness"), nameof(StormCloudBrightness));
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

    public static double CloudBrightness(double brightness, double stormPresence, VanillaConfig vanilla) =>
        GameMath.Lerp(brightness, vanilla.StormCloudBrightness, Math.Sqrt(stormPresence));

    private static HarmonyMethod Yielding(string patch) => new(typeof(VanillaWeatherPatches), patch) { priority = Priority.Low };

    private static void Postfix(MethodBase target, string patch) => harmony!.Patch(target, postfix: Yielding(patch));

    private static void Transpiler(MethodBase target, string patch) => harmony!.Patch(target, transpiler: Yielding(patch));

    private static void WindAsStrength(MethodBase target, string prefix, string postfix) =>
        harmony!.Patch(target, prefix: Yielding(prefix), postfix: Yielding(postfix));

    private static SeaWeatherSystem? On(EnumAppSide side) => systems.GetValueOrDefault(side);

    private static IEnumerable<CodeInstruction> CurrentConditions(IEnumerable<CodeInstruction> instructions)
    {
        var original = instructions.ToList();
        var matcher = new CodeMatcher(original)
            .MatchStartForward(new CodeMatch(instruction => instruction.LoadsField(WorldgenRainfall)));
        if (matcher.IsValid)
        {
            matcher.SetOperandAndAdvance(Rainfall)
                .MatchStartForward(new CodeMatch(instruction => instruction.LoadsField(EventConfig)), new CodeMatch(instruction => instruction.LoadsField(PatternWeight)));
        }
        var log = systems.Values.FirstOrDefault();
        if (matcher.IsInvalid)
        {
            log?.Mod.Logger.Error("Vanilla's weather event roll changed; it is left as it is, reading worldgen rainfall and picking by bare weights");
            return original;
        }
        log?.Log("vanilla's weather event roll reads the current rain and picks by each event's conditions");
        return matcher.RemoveInstruction().SetOperandAndAdvance(HereChance).InstructionEnumeration();
    }

    private static void Precipitation(double posX, double posZ, double totalDays, ref float __result, WeatherSystemBase __instance) =>
        Rain(posX, posZ, totalDays, ref __result, __instance);

    private static void PrecipitationAt(BlockPos pos, double totalDays, ref float __result, WeatherSystemBase __instance) =>
        Rain(pos.X + 0.5, pos.Z + 0.5, totalDays, ref __result, __instance);

    private static void Rain(double x, double z, double totalDays, ref float result, WeatherSystemBase weather)
    {
        if (weather.OverridePrecipitation != null || On(weather.api.Side)?.Source.RainCloudness(x, z, totalDays) is not { } cloudness) return;
        result = Math.Max(0, cloudness - 0.5f);
    }

    private static void UnloadedRainCloudness(Vec3d pos, bool useArgValues, float lerpRainCloudOverlay, float lerpRainOverlay,
        WeatherDataReaderBase __instance, WeatherSystemBase ___ws, IMapRegion ___hereMapRegion)
    {
        var sea = On(___ws.api.Side);
        if (sea == null || ___hereMapRegion?.ClimateMap != null || ___ws.OverridePrecipitation != null) return;
        if (useArgValues)
        {
            (__instance.lerpRainCloudOverlay, __instance.lerpRainOverlay) = (lerpRainCloudOverlay, lerpRainOverlay);
            return;
        }
        if (sea.Source.RainCloudness(pos.X, pos.Z, ___ws.api.World.Calendar.TotalDays) is not { } cloudness) return;
        __instance.lerpRainCloudOverlay = GameMath.Clamp(cloudness, 0, 1);
        __instance.lerpRainOverlay = GameMath.Clamp(cloudness - 0.5f, 0, 1);
    }

    private static void StormUnderCloud(Vec3d pos, WeatherDataReaderBase __instance, WeatherSystemBase ___ws)
    {
        if (__instance is not WeatherDataReaderPreLoad) return;
        var sea = On(___ws.api.Side);
        var presence = sea != null && ___ws.OverridePrecipitation == null && __instance.lerpRainCloudOverlay > 0 ? sea.Source.StormPresence(pos.X, pos.Z) : 0;
        stormPresence.GetOrCreateValue(__instance).Value = presence;
    }

    // Patched on the protected getter: the renderers' calls to its public one-line wrapper are inlined past any patch.
    private static void StormCloudBrightness(ref double __result, WeatherDataReaderBase __instance)
    {
        var sea = On(EnumAppSide.Client);
        if (sea == null || !stormPresence.TryGetValue(__instance, out var presence)) return;
        __result = CloudBrightness(__result, presence.Value, sea.Config.Vanilla);
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
        var wind = sea.Source.Wind(player.Pos.X, player.Pos.Z);
        var perMps = (float)sea.Config.Vanilla.CloudSpeedPerMps;
        ___cloudSpeedX = (float)wind.X * perMps;
        ___cloudSpeedZ = (float)wind.Z * perMps;
    }
}
