using System;
using System.Globalization;
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
/// <see cref="Attach"/> takes the patches out again, returns false, and the sea is drawn as vanilla's.
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
        "fragNormal = seaTilt(unpackNormal(renderFlags)); seaFoamCrest = seaCrest; seaFoamFold = seaFold; "
        + "seaFoamShare = seaShare; seaFoamWorld = worldPos.xz + playerpos.xz;";
    private const string FragmentMain = "void main()";
    private const string BeforeFog = "texColor = applyFog(texColor, fogAmount);";
    private const string Foamed =
        "if (!isLava && fragNormal.y > 0.5) { float seaFoamAmount = seaFoamAt(); "
        + "vec3 seaFoamColor = mix(texColor.rgb, vec3(0.88, 0.92, 0.92) * clamp(bright * 1.3, 0.0, 1.0) * mix(1.0, shadowBright, 0.5), 0.75); "
        + "texColor.rgb = mix(texColor.rgb, seaFoamColor, seaFoamAmount); "
        + "texColor.a = mix(texColor.a, 0.95, seaFoamAmount * 0.8); }\n";
    private const string WavesUniform = "seaWaves";
    private const string FoamUniform = "seaFoam";
    private const string HeightMapSampler = "seaHeightMap";
    private const int HeightMapUnit = 7;
    private const double NoCaps = 10;
    private static readonly AssetLocation VertexSource = new("roughseas", "shaderincludes/seasurface.vsh");
    private static readonly AssetLocation FragmentSource = new("roughseas", "shaderincludes/seafoam.fsh");

    private static Harmony? harmony;
    private static SeaSurface? surface;
    private static SurfaceField? field;
    private static LoadedTexture? heightMap;
    private static ICoreClientAPI? capi;
    private static float[] uniforms = [];
    private static string vertexCode = "";
    private static string fragmentCode = "";
    private static bool normalTilted;

    public static bool DrawWaves { get; set; } = true;

    public static bool DrawFoam { get; set; } = true;

    public static bool Attach(SeaSurface waves, SurfaceField heights, ICoreClientAPI api)
    {
        (surface, field, capi) = (waves, heights, api);
        heightMap = new LoadedTexture(api);
        uniforms = new float[waves.Slots * 4];
        ReadSources(api);
        harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.Method(typeof(ShaderRegistry), "HandleIncludes"), postfix: new HarmonyMethod(typeof(SurfaceShader), nameof(Inject)));
        harmony.Patch(AccessTools.Method(typeof(ShaderProgramBase), nameof(ShaderProgramBase.Use)), postfix: new HarmonyMethod(typeof(SurfaceShader), nameof(Bind)));
        harmony.Patch(AccessTools.Method(typeof(ClientMain), nameof(ClientMain.EyesInWaterDepth)), postfix: new HarmonyMethod(typeof(SurfaceShader), nameof(EyesUnderSurface)));
        if (Compile(api)) return true;

        Detach();
        api.Shader.ReloadShaders();
        return false;
    }

    public static bool Reload()
    {
        if (capi == null) return false;
        capi.Assets.Reload(new AssetLocation(VertexSource.Domain, "shaderincludes/"));
        ReadSources(capi);
        return Compile(capi);
    }

    public static void Detach()
    {
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
        heightMap?.Dispose();
        (surface, field, heightMap, capi) = (null, null, null, null);
    }

    public static void Upload()
    {
        if (capi == null || field == null || heightMap == null || field.Across == 0) return;
        heightMap.Width = heightMap.Height = field.Across;
        capi.Render.LoadOrUpdateTextureFromRgba(field.Pixels(), true, 1, ref heightMap);
    }

    private static bool Compile(ICoreClientAPI api)
    {
        normalTilted = false;
        api.Shader.ReloadShaders();
        return normalTilted && Active(ShaderPrograms.Chunkliquid, WavesUniform) && Active(ShaderPrograms.Chunkliquid, FoamUniform);
    }

    private static bool Active(ShaderProgramBase program, string uniform) =>
        program.uniformLocations.TryGetValue(uniform, out var location) && location >= 0;

    private static void ReadSources(ICoreClientAPI api)
    {
        vertexCode = api.Assets.Get(VertexSource).ToText()
            .Replace("SEA_SLOTS", surface!.Slots.ToString(CultureInfo.InvariantCulture))
            .Replace("SEA_TRAIL_SECONDS", SeaSurface.TrailSeconds.ToString("0.0###", CultureInfo.InvariantCulture));
        fragmentCode = api.Assets.Get(FragmentSource).ToText();
    }

    private static void EyesUnderSurface(ClientMain __instance, ref float __result)
    {
        if (__result <= 0 || surface == null || field == null || surface.Calm || __result > surface.SeaHeight + 1) return;
        var eye = __instance.MainCamera.CameraEyePos;
        var share = field.Factor(eye.X, eye.Z);
        __result = (float)Math.Max(0, __result + surface.HeightAt(eye.X, eye.Z, share));
    }

    private static void Inject(ShaderProgram program, ref string __result)
    {
        if (__result.Contains(VanillaWarp) && __result.Contains(AfterWarp) && !__result.Contains(RenamedWarp))
        {
            __result = __result.Replace(VanillaWarp, RenamedWarp).Replace(AfterWarp, vertexCode + AfterWarp);
        }
        if (program.PassName != LiquidPass) return;
        if (__result.Contains(FaceNormal))
        {
            __result = __result.Replace(FaceNormal, TiltedNormal);
            normalTilted = true;
        }
        if (__result.Contains(BeforeFog) && !__result.Contains(fragmentCode))
        {
            __result = __result.Replace(FragmentMain, fragmentCode + FragmentMain).Replace(BeforeFog, Foamed + BeforeFog);
        }
    }

    private static void Bind(ShaderProgramBase __instance)
    {
        if (surface == null || !Active(__instance, WavesUniform) || capi?.World.Player?.Entity is not { } player) return;
        if (DrawWaves) surface.WriteUniforms(player.CameraPos.X, player.CameraPos.Z, uniforms);
        else Array.Clear(uniforms);
        __instance.Uniforms4(WavesUniform, surface.Slots, uniforms);
        __instance.Uniform("seaSteepness", (float)surface.Steepness);
        if (field != null && heightMap is { TextureId: > 0 })
        {
            __instance.BindTexture2D(HeightMapSampler, heightMap.TextureId, HeightMapUnit);
            __instance.Uniform("seaHeightArea", new Vec4f((float)(field.OriginX - player.CameraPos.X), (float)(field.OriginZ - player.CameraPos.Z),
                (float)field.SizeBlocks, (float)field.SizeBlocks));
        }
        if (!Active(__instance, FoamUniform)) return;
        __instance.Uniform(FoamUniform, DrawFoam
            ? new Vec4f((float)Math.Min(surface.CapsFold, NoCaps), (float)surface.Foam.Streaks, (float)surface.Foam.Spray, (float)surface.SurfDepth)
            : new Vec4f((float)NoCaps, 0, 0, 0));
        __instance.Uniform("seaWind", new Vec2f((float)Math.Cos(surface.TowardRadians), (float)Math.Sin(surface.TowardRadians)));
    }
}
