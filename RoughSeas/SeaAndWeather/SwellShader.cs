using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.Client.NoObf;

namespace RoughSeas.SeaAndWeather;

public static class SwellShader
{
    private const string HarmonyId = "roughseas.swell";
    private const string VanillaWarp = "vec4 applyLiquidWarping(bool windAffected, vec4 worldPos, float div)";
    private const string RenamedWarp = "vec4 applyVanillaLiquidWarping(bool windAffected, vec4 worldPos, float div)";
    private const string NextFunction = "vec4 applyVertexWarping(";
    private const string WavesUniform = "seaSwell";
    private const int MaxWaves = 8;
    private static readonly AssetLocation Source = new("roughseas", "shaderincludes/seaswell.vsh");

    private static readonly float[] waves = new float[MaxWaves * 4];
    private static Harmony? harmony;
    private static SeaWeatherSystem? sea;
    private static ICoreClientAPI? capi;
    private static string swellCode = "";
    private static bool drawn = true;

    public static void Attach(SeaWeatherSystem system, ICoreClientAPI api)
    {
        sea = system;
        capi = api;
        swellCode = api.Assets.Get(Source).ToText();
        harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.Method(typeof(ShaderRegistry), "HandleIncludes"), postfix: new HarmonyMethod(typeof(SwellShader), nameof(Inject)));
        harmony.Patch(AccessTools.Method(typeof(ShaderProgramBase), nameof(ShaderProgramBase.Use)), postfix: new HarmonyMethod(typeof(SwellShader), nameof(Bind)));
        if (!api.Shader.ReloadShaders()) system.Mod.Logger.Error("Shaders failed to compile with the sea swell; see the client log");
    }

    public static bool Draw(bool swell)
    {
        drawn = swell;
        return capi!.Shader.ReloadShaders();
    }

    public static void Detach()
    {
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
        sea = null;
        capi = null;
    }

    private static void Inject(ref string __result)
    {
        if (!drawn || __result.Contains(RenamedWarp) || !__result.Contains(VanillaWarp) || !__result.Contains(NextFunction)) return;
        __result = __result.Replace(VanillaWarp, RenamedWarp).Replace(NextFunction, swellCode + NextFunction);
    }

    private static void Bind(ShaderProgramBase __instance)
    {
        if (sea == null || capi == null || !__instance.uniformLocations.ContainsKey(WavesUniform)) return;
        var origin = capi.Render.ShaderUniforms.PlayerPos;
        sea.Swell.Advance(capi.InWorldEllapsedMilliseconds / 1000.0);
        sea.Swell.ShaderWaves(origin.X, origin.Z, waves);
        __instance.Uniforms4(WavesUniform, MaxWaves, waves);
        __instance.Uniform("seaSwellSteepness", (float)sea.Surface.Steepness);
    }
}
