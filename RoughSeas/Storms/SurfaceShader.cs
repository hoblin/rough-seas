using System;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using OpenTK.Graphics.OpenGL;
using RoughSeas.SeaAndWeather;
using RoughSeas.Surf;
using RoughSeas.Water;
using Vintagestory.Client;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace RoughSeas.Storms;

/// <summary>
/// Draws the <see cref="SeaSurface"/> in vanilla's water shaders, patched as text at anchors in 1.22.7's
/// <c>vertexwarp.vsh</c>, <c>chunkliquid.vsh</c> and <c>chunkliquid.fsh</c>, and wet ground in <c>chunkopaque.fsh</c> and
/// <c>chunktopsoil.fsh</c>. When a game update moves a water anchor, <see cref="Attach"/> takes the patches out again,
/// returns false, and the sea is drawn as vanilla's; a moved wet-ground anchor leaves that ground dry.
/// </summary>
public static class SurfaceShader
{
    private const string HarmonyId = "roughseas.storms.surface";
    private const string LiquidPass = "chunkliquid";
    private const string VanillaWarp = "vec4 applyLiquidWarping(bool windAffected, vec4 worldPos, float div)";
    private const string RenamedWarp = "vec4 applyVanillaLiquidWarping(bool windAffected, vec4 worldPos, float div)";
    private const string RippleKept = "float seaRippleKept = 1.0;\n";
    private const string WaterRipple = "worldPos.y += waterWaveIntensity * gnoise(noisepos) / div;";
    private const string WindRipple = "worldPos.y += windWaveIntensity * gnoise(noisepos * 3.5) / (div * 4);";
    private const string AfterWarp = "vec4 applyVertexWarping(";
    private const string FaceNormal = "fragNormal = unpackNormal(renderFlags);";
    private const string TiltedNormal =
        "fragNormal = seaTilt(unpackNormal(renderFlags)); seaFoamCrest = seaCrest; seaFoamFold = seaFold; "
        + "seaFoamShare = seaShare; seaFoamFace = seaFaceFoam; seaFoamWorld = worldPos.xz + playerpos.xz; seaDebug = seaDebugValue;";
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
    private const string WetUniform = "seaWet";
    private const string BeforeOpaqueLight = "outColor = applyFogAndShadowFromBrightness(texColor,";
    private const string BeforeTopsoilLight = "outColor = applyFogAndShadowWithNormal(outColor,";
    private static readonly (string Pass, string BeforeLight, string Shaded)[] WetPasses =
    [
        ("chunkopaque", BeforeOpaqueLight, "texColor"),
        ("chunktopsoil", BeforeTopsoilLight, "outColor"),
    ];
    private static readonly AssetLocation WetSource = new("roughseas", "shaderincludes/seawet.fsh");
    private static readonly string[] ShoreSamplers = ["seaBed", "seaBedAux", "seaLagA", "seaLagB", "seaFace", "seaGainA", "seaGainB"];
    private const double NoCaps = 10;
    private static readonly AssetLocation VertexSource = new("roughseas", "shaderincludes/seasurface.vsh");
    private static readonly AssetLocation FragmentSource = new("roughseas", "shaderincludes/seafoam.fsh");

    private static Harmony? harmony;
    private static SeaSurface? surface;
    private static SurfaceField? field;
    private static LoadedTexture? heightMap;
    private static ICoreClientAPI? capi;
    private static int heightMapUnit;
    private static int[] shoreTextures = [];
    private static SurfField? shore;
    private static float[] uniforms = [];
    private static float[] shortUniforms = [];
    private static readonly HeightColours colours = new();
    private static bool rippleTamed;
    private static string vertexCode = "";
    private static string fragmentCode = "";
    private static string wetCode = "";
    private static double lensDepthRatio;
    private static double wetReflectance;
    private static double foamDecaySeconds;
    private static double wallSlope;
    private static bool normalTilted;

    public static bool DrawWaves { get; set; } = true;

    public static bool DrawFoam { get; set; } = true;

    public static bool DrawShore { get; set; } = true;

    public static bool DrawReflection { get; set; } = true;

    public static bool DrawLag { get; set; } = true;

    public static bool DrawEnergy { get; set; } = true;

    public static HeightColours Colours => colours;

    public static bool Attach(SeaSurface waves, SurfaceField heights, double lensDepth, double wetShade, double foamDecay, double steepestShore, TessellationConfig tessellation, Action<string> log, ICoreClientAPI api)
    {
        (surface, field, capi, lensDepthRatio, wetReflectance, foamDecaySeconds, wallSlope) = (waves, heights, api, lensDepth, wetShade, foamDecay, steepestShore);
        heightMap = new LoadedTexture(api);
        heightMapUnit = GL.GetInteger(GetPName.MaxCombinedTextureImageUnits) - 1;
        shoreTextures = ShoreSamplers.Select(_ => GL.GenTexture()).ToArray();
        uniforms = new float[waves.Trains.Slots * 4];
        shortUniforms = new float[waves.Trains.ShortSlots * 4];
        ReadSources(api);
        harmony = new Harmony(HarmonyId);
        SurfaceTessellation.Attach(harmony, tessellation, log);
        harmony.Patch(AccessTools.Method(typeof(ShaderRegistry), "HandleIncludes"), postfix: new HarmonyMethod(typeof(SurfaceShader), nameof(Inject)));
        harmony.Patch(AccessTools.Method(typeof(ShaderProgram), nameof(ShaderProgram.Compile)), postfix: new HarmonyMethod(typeof(SurfaceShader), nameof(LocateSamplers)));
        harmony.Patch(AccessTools.Method(typeof(ShaderProgramBase), nameof(ShaderProgramBase.Use)), postfix: new HarmonyMethod(typeof(SurfaceShader), nameof(Bind)));
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
        SurfaceTessellation.Detach();
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
        heightMap?.Dispose();
        foreach (var texture in shoreTextures) GL.DeleteTexture(texture);
        shoreTextures = [];
        (surface, field, heightMap, capi, shore) = (null, null, null, null, null);
    }

    public static void Upload()
    {
        if (capi == null || field == null || heightMap == null || field.Across == 0) return;
        heightMap.Width = heightMap.Height = field.Across;
        capi.Render.LoadOrUpdateTextureFromRgba(field.Pixels(), true, 1, ref heightMap);
        if (surface != null && capi.World.Player?.Entity is { } player) colours.Refresh(surface, field, player.CameraPos.X, player.CameraPos.Z);
    }

    public static void Upload(SurfField built)
    {
        if (shoreTextures.Length == 0) return;
        float[][] layers = [built.Bed, built.Aux, built.Lags[0], built.Lags[1], built.Face, built.Gains[0], built.Gains[1]];
        for (var i = 0; i < layers.Length; i++)
        {
            GL.BindTexture(TextureTarget.Texture2D, shoreTextures[i]);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba32f, built.Size, built.Size, 0, PixelFormat.Rgba, PixelType.Float, layers[i]);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        }
        GL.BindTexture(TextureTarget.Texture2D, 0);
        shore = built;
    }

    public static string Samplers() => string.Join("\n", AccessTools.StaticFieldRefAccess<ShaderProgram[]>(typeof(ShaderRegistry), "shaderPrograms")
        .Where(program => program?.textureLocations.Count > 0)
        .Select(program => program.PassName + ": "
            + string.Join(", ", program.textureLocations.OrderBy(sampler => sampler.Value).Select(sampler => $"{sampler.Key} {sampler.Value}"))
            + (Active(program, HeightMapSampler) ? $"; {HeightMapSampler} {heightMapUnit}" : "")));

    private static bool Compile(ICoreClientAPI api)
    {
        (normalTilted, rippleTamed) = (false, false);
        api.Shader.ReloadShaders();
        return normalTilted && rippleTamed && Active(ShaderPrograms.Chunkliquid, WavesUniform) && Active(ShaderPrograms.Chunkliquid, FoamUniform);
    }

    private static bool Active(ShaderProgramBase program, string uniform) =>
        program.uniformLocations.TryGetValue(uniform, out var location) && location >= 0;

    private static void ReadSources(ICoreClientAPI api)
    {
        vertexCode = api.Assets.Get(VertexSource).ToText()
            .Replace("SEA_SLOTS", surface!.Trains.Slots.ToString(CultureInfo.InvariantCulture))
            .Replace("SEA_SHORT", surface.Trains.ShortSlots.ToString(CultureInfo.InvariantCulture))
            .Replace("SEA_TRAIL_SECONDS", SeaSurface.TrailSeconds.ToString("0.0###", CultureInfo.InvariantCulture))
            .Replace("SEA_GRAVITY", WaveTheory.Gravity.ToString("0.0###", CultureInfo.InvariantCulture))
            .Replace("SEA_BORE_AMPLITUDE", WaveTheory.BoreAmplitude.ToString("0.0######", CultureInfo.InvariantCulture))
            .Replace("SEA_TWO_PERCENT", WaveTheory.TwoPercentRatio.ToString("0.0######", CultureInfo.InvariantCulture));
        fragmentCode = api.Assets.Get(FragmentSource).ToText();
        wetCode = api.Assets.Get(WetSource).ToText();
    }

    [ReliesOn("Vintagestory.Client.NoObf.ShaderRegistry", "HandleIncludes", "returns the shader's whole source with its includes in place, which the anchors are matched against")]
    [ReliesOnText("shaderincludes/vertexwarp.vsh", VanillaWarp)]
    [ReliesOnText("shaderincludes/vertexwarp.vsh", AfterWarp)]
    [ReliesOnText("shaders/chunkliquid.vsh", FaceNormal)]
    [ReliesOnText("shaders/chunkliquid.fsh", FragmentMain)]
    [ReliesOnText("shaders/chunkliquid.fsh", BeforeFog)]
    [ReliesOnText("shaders/chunkopaque.fsh", FragmentMain)]
    [ReliesOnText("shaders/chunkopaque.fsh", BeforeOpaqueLight)]
    [ReliesOnText("shaders/chunktopsoil.fsh", FragmentMain)]
    [ReliesOnText("shaders/chunktopsoil.fsh", BeforeTopsoilLight)]
    private static void Inject(ShaderProgram program, ref string __result)
    {
        if (__result.Contains(VanillaWarp) && __result.Contains(AfterWarp) && !__result.Contains(RenamedWarp))
        {
            rippleTamed |= __result.Contains(WaterRipple) && __result.Contains(WindRipple);
            __result = __result.Replace(VanillaWarp, RippleKept + RenamedWarp).Replace(AfterWarp, vertexCode + AfterWarp)
                .Replace(WaterRipple, "worldPos.y += seaRippleKept * waterWaveIntensity * gnoise(noisepos) / div;")
                .Replace(WindRipple, "worldPos.y += seaRippleKept * windWaveIntensity * gnoise(noisepos * 3.5) / (div * 4);");
        }
        foreach (var (pass, beforeLight, shaded) in WetPasses)
        {
            if (program.PassName != pass || !__result.Contains(beforeLight) || __result.Contains(wetCode)) continue;
            __result = __result.Replace(FragmentMain, wetCode + FragmentMain).Replace(beforeLight, $"if (normal.y > 0.5) {shaded}.rgb *= seaWetShade(worldPos);\n" + beforeLight);
        }
        if (program.PassName != LiquidPass) return;
        __result = HeightColours.Inject(__result, FragmentMain);
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

    [ReliesOn("Vintagestory.Client.NoObf.ShaderProgram", "collectUniformNames", "numbers samplers by a regex over the source, which a uniform with a precision qualifier escapes")]
    private static void LocateSamplers(ShaderProgram __instance)
    {
        foreach (var sampler in ShoreSamplers.Prepend(HeightMapSampler))
        {
            var location = ScreenManager.Platform.GetUniformLocation(__instance, sampler);
            if (location >= 0) __instance.uniformLocations[sampler] = location;
        }
    }

    [ReliesOn("Vintagestory.Client.NoObf.ShaderProgramBase", "Use", "binds vanilla's samplers first, on units below the last ones ours take")]
    private static void Bind(ShaderProgramBase __instance)
    {
        if (surface == null || capi?.World.Player?.Entity is not { } player) return;
        if (Active(__instance, WavesUniform))
        {
            if (DrawWaves) surface.Trains.WriteUniforms(player.CameraPos.X, player.CameraPos.Z, uniforms);
            else Array.Clear(uniforms);
            __instance.Uniforms4(WavesUniform, surface.Trains.Slots, uniforms);
            __instance.Uniform("seaSteepness", (float)surface.Trains.Steepness);
            if (DrawWaves) surface.Trains.WriteShortUniforms(player.CameraPos.X, player.CameraPos.Z, shortUniforms);
            else Array.Clear(shortUniforms);
            __instance.Uniforms4("seaShort", surface.Trains.ShortSlots, shortUniforms);
            __instance.Uniform("seaMesh", new Vec2f(SurfaceTessellation.ReachMetres, SurfaceTessellation.FinestSpacing));
            __instance.Uniform("seaTerms", new Vec3f(DrawReflection ? 1 : 0, DrawLag ? 1 : 0, DrawEnergy ? 1 : 0));
        }
        colours.Bind(__instance, capi.World.SeaLevel - 1 + WaterColumn.StillLevel - player.CameraPos.Y);
        if (field != null && heightMap is { TextureId: > 0 } && Active(__instance, HeightMapSampler))
        {
            __instance.BindTexture2D(HeightMapSampler, heightMap.TextureId, heightMapUnit);
            __instance.Uniform("seaHeightArea", new Vec4f((float)(field.OriginX - player.CameraPos.X), (float)(field.OriginZ - player.CameraPos.Z),
                (float)field.SizeBlocks, (float)field.SizeBlocks));
        }
        if (shore != null && ShoreSamplers.Any(sampler => Active(__instance, sampler)))
        {
            for (var i = 0; i < ShoreSamplers.Length; i++)
            {
                if (Active(__instance, ShoreSamplers[i])) __instance.BindTexture2D(ShoreSamplers[i], shoreTextures[i], heightMapUnit - 1 - i);
            }
            __instance.Uniform("seaBedArea", new Vec4f((float)(shore.OriginX - player.CameraPos.X), (float)(shore.OriginZ - player.CameraPos.Z), shore.Size,
                (float)player.CameraPos.Y));
        }
        if (Active(__instance, "seaShore"))
        {
            __instance.Uniform("seaShore", new Vec4f((float)surface.Shape.BreakingDepthRatio, surface.Trains.Dominant, shore != null && DrawShore ? 1 : 0, (float)surface.Shape.CrestRatio));
            __instance.Uniform("seaPlunging", new Vec2f((float)surface.Shape.PlungingFrom, (float)surface.Shape.PlungingTo));
            __instance.Uniform("seaFoamDecay", DrawFoam ? (float)foamDecaySeconds : 0);
            __instance.Uniform("seaFaceSlope", (float)(shore?.FaceSlope ?? 0));
            __instance.Uniform("seaBedEdge", (float)(shore?.EdgeFadeBlocks ?? 1));
            __instance.Uniform("seaBoreSlope", (float)surface.BoreFrontSlope);
            __instance.Uniform("seaWallSlope", (float)wallSlope);
            var vectors = surface.Trains.Vectors();
            float Valid(int slot) => shore != null && slot < vectors.Length && slot < shore.Numbers.Length && shore.Numbers[slot] == vectors[slot] ? 1 : 0;
            __instance.Uniform("seaLagValidA", new Vec4f(Valid(0), Valid(1), Valid(2), Valid(3)));
            __instance.Uniform("seaLagValidB", new Vec4f(Valid(4), Valid(5), Valid(6), Valid(7)));
        }
        if (Active(__instance, WetUniform))
        {
            __instance.Uniform(WetUniform, new Vec4f((float)surface.Trains.DominantPeriod, (float)lensDepthRatio, (float)wetReflectance, shore != null && DrawShore ? 1 : 0));
        }
        if (!Active(__instance, FoamUniform)) return;
        __instance.Uniform(FoamUniform, DrawFoam
            ? new Vec4f((float)Math.Min(surface.CapsFold, NoCaps), (float)surface.Foam.Streaks, (float)surface.Foam.Spray, (float)surface.SurfDepth)
            : new Vec4f((float)NoCaps, 0, 0, 0));
        __instance.Uniform("seaWavesToward", new Vec2f((float)Math.Cos(surface.Trains.TowardRadians), (float)Math.Sin(surface.Trains.TowardRadians)));
    }
}
