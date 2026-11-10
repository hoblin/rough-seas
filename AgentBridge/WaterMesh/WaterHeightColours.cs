using System;
using HarmonyLib;
using RoughSeas.Storms;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace AgentBridge.WaterMesh;

internal sealed class WaterHeightColours
{
    private const string BeforeOutput = "OIT(texColor, glowLevel);";
    private const string FragmentMain = "void main()";
    private const string Uniform = "seaHeightColours";
    private const double ReachMetres = 32;
    private const double StepMetres = 1;
    private const double RefreshSeconds = 0.1;
    private const float StillSurface = 0.875f;
    private const string Palette =
        "uniform vec4 seaHeightColours;\nin vec2 seaBlockCell;\n"
        + "vec3 seaHeightColour(float share) {\n"
        + "vec3 k = mod(vec3(5.0, 3.0, 1.0) + (1.0 - share) * 0.78 * 6.0, 6.0);\n"
        + "return vec3(1.0) - max(min(k, min(4.0 - k, vec3(1.0))), vec3(0.0));\n"
        + "}\n";
    private const string Painted =
        "if (seaHeightColours.z > 0.5 && !isLava && fragNormal.y > 0.5) {\n"
        + "texColor = vec4(seaHeightColour(clamp((fWorldPos.y - seaHeightColours.x) / max(seaHeightColours.y - seaHeightColours.x, 0.01), 0.0, 1.0)), 1.0);\n"
        + "vec2 seaEdge = abs(fract(seaBlockCell + 0.5) - 0.5) / max(fwidth(seaBlockCell), vec2(0.0001));\n"
        + "texColor.rgb *= smoothstep(0.5, 1.5, min(seaEdge.x, seaEdge.y));\n"
        + "}\n";
    private const string RippleLine = "vec4 warped = applyVanillaLiquidWarping(windAffected, worldPos, div);";
    private const string SteepLine = "float steepness = min(seaSteepness, cusp);";
    private const string VertexOutput = "flat out int waterFlags;";
    private const string VertexFilled = "uv = uvIn;";

    private static WaterHeightColours? active;

    private readonly ICoreClientAPI capi;
    private double lowest;
    private double highest;
    private long nextRefresh;

    public WaterHeightColours(ICoreClientAPI capi) => this.capi = capi;

    public static bool On { get; set; }

    public void Attach(Harmony harmony)
    {
        active = this;
        harmony.Patch(AccessTools.Method(typeof(ShaderRegistry), "HandleIncludes"), postfix: new HarmonyMethod(typeof(WaterHeightColours), nameof(Inject)) { priority = Priority.Last });
        harmony.Patch(AccessTools.Method(typeof(MeshDataPoolManager), nameof(MeshDataPoolManager.Render)), new HarmonyMethod(typeof(WaterHeightColours), nameof(Set)));
    }

    public static float Ripple { get; set; } = 1;

    public static float Shape { get; set; } = 1;

    public static float Face { get; set; } = 1;

    public static float Lag { get; set; } = 1;

    public static float Refraction { get; set; } = 1;

    private static void Inject(ShaderProgram program, ref string __result)
    {
        if (__result.Contains(RippleLine) && !__result.Contains("seaTestRipple"))
        {
            __result = __result.Replace(RippleLine, "vec4 warped = seaTestRipple > 0.5 ? applyVanillaLiquidWarping(windAffected, worldPos, div) : worldPos;")
                .Replace(SteepLine, "float steepness = min(seaSteepness, cusp) * seaTestShape;")
                .Replace("float faceReflects = faceLength > 0.0 ? face.w : 0.0;", "float faceReflects = faceLength > 0.0 ? face.w * seaTestFace : 0.0;")
                .Replace("lagA[slot] * seaLagValidA[slot]", "seaTestLag * lagA[slot] * seaLagValidA[slot]")
                .Replace("lagB[slot - 4] * seaLagValidB[slot - 4]", "seaTestLag * lagB[slot - 4] * seaLagValidB[slot - 4]")
                .Replace("(cosine > 0.0 ? seaRefraction(deepNumber, bed.x, cosine) : 1.0)", "(cosine > 0.0 && seaTestRefraction > 0.5 ? seaRefraction(deepNumber, bed.x, cosine) : 1.0)")
                .Replace("uniform float seaFaceSlope;", "uniform float seaFaceSlope;\nuniform float seaTestRipple;\nuniform float seaTestShape;\nuniform float seaTestFace;\nuniform float seaTestLag;\nuniform float seaTestRefraction;");
        }
        if (program.PassName != "chunkliquid") return;
        if (__result.Contains(VertexOutput) && !__result.Contains("seaBlockCell"))
        {
            __result = __result.Replace(VertexOutput, VertexOutput + "\nout vec2 seaBlockCell;").Replace(VertexFilled, VertexFilled + " seaBlockCell = xyz.xz;");
        }
        if (!__result.Contains(BeforeOutput) || __result.Contains(Uniform)) return;
        __result = __result.Replace(FragmentMain, Palette + FragmentMain).Replace(BeforeOutput, Painted + BeforeOutput);
    }

    private static void Set(Vec3d playerpos)
    {
        if (active?.capi.Render.CurrentActiveShader is not { } shader) return;
        if (shader.HasUniform("seaTestRipple"))
        {
            shader.Uniform("seaTestRipple", Ripple);
            shader.Uniform("seaTestShape", Shape);
            shader.Uniform("seaTestFace", Face);
            shader.Uniform("seaTestLag", Lag);
            shader.Uniform("seaTestRefraction", Refraction);
        }
        if (!shader.HasUniform(Uniform)) return;
        active.Refresh(playerpos);
        var surface = active.capi.World.SeaLevel - 1 + StillSurface - playerpos.Y;
        shader.Uniform(Uniform, new Vec4f((float)(surface + active.lowest), (float)(surface + active.highest), On ? 1 : 0, 0));
    }

    private void Refresh(Vec3d playerpos)
    {
        var now = capi.World.ElapsedMilliseconds;
        if (!On || now < nextRefresh || capi.ModLoader.GetModSystem<StormsModSystem>()?.DrawnSea is not { Swell: true } sea) return;
        nextRefresh = now + (long)(RefreshSeconds * 1000);
        (lowest, highest) = (double.MaxValue, double.MinValue);
        for (var z = -ReachMetres; z <= ReachMetres; z += StepMetres)
        {
            for (var x = -ReachMetres; x <= ReachMetres; x += StepMetres)
            {
                var height = sea.HeightAt(playerpos.X + x, playerpos.Z + z);
                lowest = Math.Min(lowest, height);
                highest = Math.Max(highest, height);
            }
        }
    }
}
