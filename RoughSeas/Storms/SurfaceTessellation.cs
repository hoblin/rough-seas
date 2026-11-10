using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using OpenTK.Graphics.OpenGL;
using Vintagestory.API.Client;
using Vintagestory.Client;
using Vintagestory.Client.NoObf;

namespace RoughSeas.Storms;

public static class SurfaceTessellation
{
    private const int LiquidPass = 4;
    private const int PatchVertices = 3;
    private const int LowestVersion = 40;
    private const string VertexMain = "void main(void)";

    private static readonly Regex Attribute = new(@"layout\(location\s*=\s*\d+\)\s*in\s+(\w+)\s+(\w+)\s*;", RegexOptions.Compiled);
    private static readonly string[] LiquidShaders = ["chunkliquid.vsh", "chunkliquiddepth.vsh"];
    private static readonly AccessTools.FieldRef<Shader, string> Filename = AccessTools.FieldRefAccess<Shader, string>("Filename");
    private static readonly AccessTools.FieldRef<ChunkRenderer, MeshDataPoolManager[][]> PoolsByPass = AccessTools.FieldRefAccess<ChunkRenderer, MeshDataPoolManager[][]>("poolsByRenderPass");
    private static readonly AccessTools.FieldRef<MeshDataPoolManager, List<MeshDataPool>> Pools = AccessTools.FieldRefAccess<MeshDataPoolManager, List<MeshDataPool>>("pools");
    private static readonly AccessTools.FieldRef<MeshDataPool, MeshRef> ModelRef = AccessTools.FieldRefAccess<MeshDataPool, MeshRef>("modelRef");
    private static readonly Dictionary<Shader, (int Control, int Evaluation)> Stages = [];

    private static TessellationConfig config = new();
    private static ChunkRenderer? renderer;
    private static bool supported;

    public static bool On { get; set; } = true;

    public static bool Drawn => supported && On;

    public static float ReachMetres => (float)config.ReachMetres;

    public static float FinestSpacing => Drawn ? 1f / config.MaxLevel : 1;

    public static void Attach(Harmony harmony, TessellationConfig settings, Action<string> log)
    {
        config = settings;
        supported = GL.GetInteger(GetPName.MajorVersion) * 10 + GL.GetInteger(GetPName.MinorVersion) >= LowestVersion;
        log(supported ? "surface: tessellating the water near the player" : "surface: OpenGL below 4.0, the water keeps its one quad a block");
        if (!supported) return;
        var type = typeof(SurfaceTessellation);
        harmony.Patch(AccessTools.Method(typeof(ClientPlatformWindows), nameof(ClientPlatformWindows.CompileShader)), new HarmonyMethod(type, nameof(Compile)));
        harmony.Patch(AccessTools.Method(typeof(ClientPlatformWindows), nameof(ClientPlatformWindows.CreateShaderProgram)), new HarmonyMethod(type, nameof(Link)));
        harmony.Patch(AccessTools.Method(typeof(ChunkRenderer), nameof(ChunkRenderer.OnRenderBefore)), new HarmonyMethod(type, nameof(PreparePools)));
        harmony.Patch(AccessTools.Method(typeof(ChunkRenderer), "RenderOIT"), new HarmonyMethod(type, nameof(PreparePools)));
    }

    public static void Detach()
    {
        if (renderer != null) SetDrawMode(renderer, PrimitiveType.Triangles);
        (renderer, supported) = (null, false);
        Stages.Clear();
    }

    private static void PreparePools(ChunkRenderer __instance)
    {
        renderer = __instance;
        SetDrawMode(__instance, Drawn ? PrimitiveType.Patches : PrimitiveType.Triangles);
        if (Drawn) GL.PatchParameter(PatchParameterInt.PatchVertices, PatchVertices);
    }

    private static void SetDrawMode(ChunkRenderer chunks, PrimitiveType mode)
    {
        foreach (var manager in PoolsByPass(chunks)[LiquidPass].Where(manager => manager != null))
        {
            foreach (var pool in Pools(manager)) ((VAO)ModelRef(pool)).drawMode = mode;
        }
    }

    private static bool Compile(Shader shader, ref bool __result)
    {
        if (!Drawn || shader.shaderType != EnumShaderType.VertexShader || !LiquidShaders.Contains(Filename(shader))) return true;

        var attributes = Attribute.Matches(shader.Code).Select(match => (Type: match.Groups[1].Value, Name: match.Groups[2].Value)).ToArray();
        var head = "#version 400 core\n" + shader.PrefixCode + "\n";
        shader.ShaderId = Build(ShaderType.VertexShader, VertexStage(shader, attributes, head));
        var control = Build(ShaderType.TessControlShader, ControlStage(attributes, head));
        var evaluation = Build(ShaderType.TessEvaluationShader, EvaluationStage(shader, attributes, head));
        Stages[shader] = (control, evaluation);
        __result = shader.ShaderId != 0 && control != 0 && evaluation != 0;
        return false;
    }

    private static bool Link(ShaderProgram program, ref bool __result)
    {
        if (!Drawn || program.VertexShader == null || !Stages.TryGetValue(program.VertexShader, out var stages)) return true;

        var id = program.ProgramId = GL.CreateProgram();
        GL.AttachShader(id, program.VertexShader.ShaderId);
        GL.AttachShader(id, stages.Control);
        GL.AttachShader(id, stages.Evaluation);
        GL.AttachShader(id, program.FragmentShader.ShaderId);
        GL.LinkProgram(id);
        GL.GetProgram(id, GetProgramParameterName.LinkStatus, out int linked);
        if (linked != 1) ScreenManager.Platform.Logger.Error("Link error in the tessellated water for pass {0}: {1}", program.PassName, GL.GetProgramInfoLog(id));
        __result = linked == 1;
        return false;
    }

    private static int Build(ShaderType type, string source)
    {
        var id = GL.CreateShader(type);
        GL.ShaderSource(id, source);
        GL.CompileShader(id);
        GL.GetShader(id, ShaderParameter.CompileStatus, out int compiled);
        if (compiled == 1) return id;
        ScreenManager.Platform.Logger.Error("Shader compile error in the tessellated water, {0}: {1}", type, GL.GetShaderInfoLog(id));
        return 0;
    }

    private static string VertexStage(Shader shader, (string Type, string Name)[] attributes, string head)
    {
        var text = new StringBuilder(head).Append("#extension GL_ARB_explicit_attrib_location: enable\n");
        foreach (Match match in Attribute.Matches(shader.Code)) text.Append(match.Value).Append('\n');
        foreach (var (type, name) in attributes) text.Append($"out {type} vs_{name};\n");
        text.Append("void main(void) {\n");
        foreach (var (_, name) in attributes) text.Append($"vs_{name} = {name};\n");
        return text.Append("gl_Position = vec4(xyz, 1.0);\n}\n").ToString();
    }

    private static string ControlStage((string Type, string Name)[] attributes, string head)
    {
        var text = new StringBuilder(head).Append("layout(vertices = 3) out;\nuniform vec3 origin;\n");
        foreach (var (type, name) in attributes) text.Append($"in {type} vs_{name}[];\nout {type} tc_{name}[];\n");
        var reach = config.ReachMetres.ToString("0.0", CultureInfo.InvariantCulture);
        var finest = config.MaxLevel.ToString("0.0", CultureInfo.InvariantCulture);
        text.Append($"float seaEdgeLevel(vec3 a, vec3 b) {{ return pow(2.0, floor(log2(clamp({reach} / max(length((a + b) * 0.5 + origin), 0.001), 1.0, {finest})))); }}\n");
        text.Append("void main(void) {\n");
        foreach (var (_, name) in attributes) text.Append($"tc_{name}[gl_InvocationID] = vs_{name}[gl_InvocationID];\n");
        text.Append("gl_out[gl_InvocationID].gl_Position = gl_in[gl_InvocationID].gl_Position;\n");
        text.Append("if (gl_InvocationID == 0) {\n");
        text.Append("gl_TessLevelOuter[0] = seaEdgeLevel(vs_xyz[1], vs_xyz[2]); gl_TessLevelOuter[1] = seaEdgeLevel(vs_xyz[2], vs_xyz[0]); gl_TessLevelOuter[2] = seaEdgeLevel(vs_xyz[0], vs_xyz[1]);\n");
        text.Append("gl_TessLevelInner[0] = max(gl_TessLevelOuter[0], max(gl_TessLevelOuter[1], gl_TessLevelOuter[2]));\n");
        return text.Append("}\n}\n").ToString();
    }

    private static string EvaluationStage(Shader shader, (string Type, string Name)[] attributes, string head)
    {
        var body = Regex.Replace(shader.Code, @"#version.*\n", "");
        body = Regex.Replace(body, @"#extension.*\n", "");
        body = Attribute.Replace(body, match => $"{match.Groups[1].Value} {match.Groups[2].Value};");
        body = body.Replace(VertexMain, "void seaVertexMain(void)");

        var text = new StringBuilder(head).Append("layout(triangles, equal_spacing, ccw) in;\n");
        foreach (var (type, name) in attributes) text.Append($"in {type} tc_{name}[];\n");
        text.Append(body).Append('\n').Append("void main(void) {\nvec3 b = gl_TessCoord;\n");
        foreach (var (_, name) in attributes.Where(attribute => attribute.Type != "int")) text.Append($"{name} = tc_{name}[0] * b.x + tc_{name}[1] * b.y + tc_{name}[2] * b.z;\n");
        foreach (var (_, name) in attributes.Where(attribute => attribute.Type == "int" && attribute.Name != "waterFlagsIn")) text.Append($"{name} = tc_{name}[0];\n");
        text.Append("int nearest = b.x >= b.y && b.x >= b.z ? tc_waterFlagsIn[0] : b.y >= b.z ? tc_waterFlagsIn[1] : tc_waterFlagsIn[2];\n");
        text.Append("vec3 ocean = vec3((tc_waterFlagsIn[0] >> 2) & 255, (tc_waterFlagsIn[1] >> 2) & 255, (tc_waterFlagsIn[2] >> 2) & 255);\n");
        text.Append("vec3 across = vec3((tc_waterFlagsIn[0] >> 10) & 255, (tc_waterFlagsIn[1] >> 10) & 255, (tc_waterFlagsIn[2] >> 10) & 255);\n");
        text.Append("vec3 along = vec3((tc_waterFlagsIn[0] >> 18) & 255, (tc_waterFlagsIn[1] >> 18) & 255, (tc_waterFlagsIn[2] >> 18) & 255);\n");
        text.Append("waterFlagsIn = (nearest & ~((255 << 2) | (255 << 10) | (255 << 18))) | (int(dot(ocean, b) + 0.5) << 2) | (int(dot(across, b) + 0.5) << 10) | (int(dot(along, b) + 0.5) << 18);\n");
        return text.Append("seaVertexMain();\n}\n").ToString();
    }
}
