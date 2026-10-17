using System;
using HarmonyLib;
using RoughSeas.SeaAndWeather;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace RoughSeas.Storms;

/// <summary>
/// Draws the <see cref="SeaSurface"/> in vanilla's water shaders, patched as text at anchors in 1.22.7's
/// <c>vertexwarp.vsh</c>, <c>chunkliquid.vsh</c> and <c>chunkliquid.fsh</c>. When a game update moves an anchor,
/// <see cref="Attach"/> returns false and the sea is drawn as vanilla's.
/// </summary>
public static class SurfaceShader
{
    private const string HarmonyId = "roughseas.storms.surface";
    private const string LiquidPass = "chunkliquid";
    private const string VanillaWarp = "vec4 applyLiquidWarping(bool windAffected, vec4 worldPos, float div)";
    private const string RenamedWarp = "vec4 applyVanillaLiquidWarping(bool windAffected, vec4 worldPos, float div)";
    private const string AfterWarp = "vec4 applyVertexWarping(";
    private const string FaceNormal = "fragNormal = unpackNormal(renderFlags);";
    private const string TiltedNormal =
        "fragNormal = seaTilt(unpackNormal(renderFlags)); seaFoamCrest = seaCrest; seaFoamFold = seaFold; seaFoamOcean = ((waterFlagsIn >> 2) & 0xff) * OneOver255;";
    private const string FragmentMain = "void main()";
    private const string BeforeFog = "texColor = applyFog(texColor, fogAmount);";
    private const string Foamed =
        "if (!isLava) { float seaFoamAmount = seaFoamAt(fragWorldPos.xz); "
        + "vec3 seaFoamColor = mix(texColor.rgb, vec3(0.88, 0.92, 0.92) * clamp(bright * 1.3, 0.0, 1.0) * mix(1.0, shadowBright, 0.5), 0.75); "
        + "texColor.rgb = mix(texColor.rgb, seaFoamColor, seaFoamAmount); "
        + "texColor.a = mix(texColor.a, 0.95, seaFoamAmount * 0.8); }\n";
    private const string WavesUniform = "seaWaves";
    private const string FoamUniform = "seaFoam";
    private const double NoCaps = 10;
    private static readonly AssetLocation VertexSource = new("roughseas", "shaderincludes/seasurface.vsh");
    private static readonly AssetLocation FragmentSource = new("roughseas", "shaderincludes/seafoam.fsh");

    private static Harmony? harmony;
    private static SeaSurface? surface;
    private static SeaWeatherSystem? weather;
    private static ICoreClientAPI? capi;
    private static float[] waves = [];
    private static string vertexCode = "";
    private static string fragmentCode = "";

    public static bool Waves { get; set; } = true;

    public static bool Foam { get; set; } = true;

    public static bool Attach(SeaSurface sea, SeaWeatherSystem weather, ICoreClientAPI api)
    {
        surface = sea;
        SurfaceShader.weather = weather;
        capi = api;
        waves = new float[sea.Slots * 4];
        ReadSources(api);
        harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.Method(typeof(ShaderRegistry), "HandleIncludes"), postfix: new HarmonyMethod(typeof(SurfaceShader), nameof(Inject)));
        harmony.Patch(AccessTools.Method(typeof(ShaderProgramBase), nameof(ShaderProgramBase.Use)), postfix: new HarmonyMethod(typeof(SurfaceShader), nameof(Bind)));
        harmony.Patch(AccessTools.Method(typeof(ClientMain), nameof(ClientMain.EyesInWaterDepth)), postfix: new HarmonyMethod(typeof(SurfaceShader), nameof(EyesUnderSurface)));
        return Compile(api);
    }

    public static bool Reload()
    {
        if (capi == null) return false;
        capi.Assets.Reload(new AssetLocation(VertexSource.Domain, "shaderincludes/"));
        ReadSources(capi);
        return Compile(capi);
    }

    private static bool Compile(ICoreClientAPI api) =>
        api.Shader.ReloadShaders()
        && ShaderPrograms.Chunkliquid.uniformLocations.ContainsKey(WavesUniform)
        && ShaderPrograms.Chunkliquid.uniformLocations.ContainsKey(FoamUniform);

    public static void Detach()
    {
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
        surface = null;
        weather = null;
        capi = null;
    }

    private static void ReadSources(ICoreClientAPI api)
    {
        vertexCode = api.Assets.Get(VertexSource).ToText();
        fragmentCode = api.Assets.Get(FragmentSource).ToText();
    }

    private static void EyesUnderSurface(ClientMain __instance, ref float __result)
    {
        if (__result <= 0 || surface == null || weather == null || surface.Calm || __result > surface.SurfHeight + 1) return;
        var eye = __instance.MainCamera.CameraEyePos;
        var openSea = SeaSurface.OpenSea(weather.Oceanity(eye.X, eye.Z));
        __result = (float)Math.Max(0, __result + openSea * surface.HeightAt(eye.X, eye.Z));
    }

    private static void Inject(ShaderProgram program, ref string __result)
    {
        if (__result.Contains(VanillaWarp) && __result.Contains(AfterWarp) && !__result.Contains(RenamedWarp))
        {
            __result = __result.Replace(VanillaWarp, RenamedWarp).Replace(AfterWarp, vertexCode + AfterWarp);
        }
        if (program.PassName != LiquidPass) return;
        if (__result.Contains(FaceNormal)) __result = __result.Replace(FaceNormal, TiltedNormal);
        if (__result.Contains(BeforeFog) && !__result.Contains(fragmentCode))
        {
            __result = __result.Replace(FragmentMain, fragmentCode + FragmentMain).Replace(BeforeFog, Foamed + BeforeFog);
        }
    }

    private static void Bind(ShaderProgramBase __instance)
    {
        if (surface == null || capi == null || !__instance.uniformLocations.ContainsKey(WavesUniform)) return;
        var camera = capi.World.Player.Entity.CameraPos;
        if (Waves) surface.WriteUniforms(camera.X, camera.Z, waves);
        else Array.Clear(waves);
        __instance.Uniforms4(WavesUniform, surface.Slots, waves);
        __instance.Uniform("seaSteepness", (float)surface.Steepness);
        if (!__instance.uniformLocations.ContainsKey(FoamUniform)) return;
        __instance.Uniform(FoamUniform, Foam
            ? new Vec4f((float)Math.Min(surface.CapsFold, NoCaps), (float)surface.Foam.Streaks, (float)surface.Foam.Spray, (float)surface.SurfHeight)
            : new Vec4f((float)NoCaps, 0, 0, 0));
        __instance.Uniform("seaWind", new Vec2f((float)Math.Cos(surface.TowardRadians), (float)Math.Sin(surface.TowardRadians)));
    }
}
