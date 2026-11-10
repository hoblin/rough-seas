using System;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace RoughSeas.Storms;

/// <summary>A debug view: the water painted from purple at the zone's lowest point to red at its highest, each block's top face outlined. The range is the CPU sea's, so a saturated colour is a height the CPU sea doesn't have.</summary>
public sealed class HeightColours
{
    private const double ReachMetres = 32;
    private const string Uniform = "seaHeightColours";
    private const string Block = "seaBlockCell";
    private const string VertexOutput = "flat out int waterFlags;";
    private const string VertexFilled = "uv = uvIn;";
    private const string BeforeOutput = "OIT(texColor, glowLevel);";
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

    private double lowest;
    private double highest;

    public bool On { get; set; }

    public static string Inject(string source, string fragmentMain)
    {
        if (source.Contains(VertexOutput) && !source.Contains(Block))
        {
            return source.Replace(VertexOutput, VertexOutput + "\nout vec2 " + Block + ";").Replace(VertexFilled, VertexFilled + " " + Block + " = xyz.xz;");
        }
        return source.Contains(BeforeOutput) && !source.Contains(Uniform)
            ? source.Replace(fragmentMain, Palette + fragmentMain).Replace(BeforeOutput, Painted + BeforeOutput)
            : source;
    }

    public void Refresh(SeaSurface surface, SurfaceField field, double x, double z)
    {
        if (!On) return;
        (lowest, highest) = (double.MaxValue, double.MinValue);
        for (var dz = -ReachMetres; dz <= ReachMetres; dz++)
        {
            for (var dx = -ReachMetres; dx <= ReachMetres; dx++)
            {
                var height = surface.HeightAt(x + dx, z + dz, field.Factor(x + dx, z + dz));
                lowest = Math.Min(lowest, height);
                highest = Math.Max(highest, height);
            }
        }
    }

    public void Bind(IShaderProgram program, double stillSurfaceY)
    {
        if (!program.HasUniform(Uniform)) return;
        program.Uniform(Uniform, new Vec4f((float)(stillSurfaceY + lowest), (float)(stillSurfaceY + highest), On ? 1 : 0, 0));
    }
}
