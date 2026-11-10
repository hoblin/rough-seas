using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using OpenTK.Graphics.OpenGL;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace AgentBridge.WaterMesh;

internal sealed class WaterClipmap : IDisposable
{
    private const int ClipmapFlag = 1 << 26;
    private const float StillSurface = 0.875f;
    private const string VertexMain = "void main(void)";
    private const string Uniforms = "uniform vec4 seaClipZone;\nuniform float seaClipY;\n";
    private const string Hidden =
        "vec3 seaClipFlat = xyz + origin;\n"
        + "if ((waterFlagsIn & 67108864) == 0 && unpackNormal(renderFlags).y > 0.5 && abs(seaClipFlat.y - seaClipY) < 0.6) {\n"
        + "vec2 seaCell = seaClipFlat.xz + vec2(((waterFlagsIn >> 10) & 255) > 0 ? 0.5 : -0.5, ((waterFlagsIn >> 18) & 255) > 0 ? 0.5 : -0.5);\n"
        + "if (seaCell.x > seaClipZone.x && seaCell.y > seaClipZone.y && seaCell.x < seaClipZone.z && seaCell.y < seaClipZone.w) gl_Position = vec4(2.0, 2.0, 2.0, 1.0);\n"
        + "}\n";
    private static readonly (float Half, float Spacing)[] Levels = [(8, 0.125f), (16, 0.25f), (32, 0.5f), (64, 1)];

    private static WaterClipmap? active;

    private readonly ICoreClientAPI capi;
    private MeshRef? mesh;
    private int atlas;

    public WaterClipmap(ICoreClientAPI capi) => this.capi = capi;

    public static bool On { get; set; }

    public void Attach(Harmony harmony)
    {
        active = this;
        harmony.Patch(AccessTools.Method(typeof(ShaderRegistry), "HandleIncludes"), postfix: new HarmonyMethod(typeof(WaterClipmap), nameof(Inject)));
        harmony.Patch(AccessTools.Method(typeof(MeshDataPoolManager), nameof(MeshDataPoolManager.Render)),
            new HarmonyMethod(typeof(WaterClipmap), nameof(SetZone)), new HarmonyMethod(typeof(WaterClipmap), nameof(Draw)));
    }

    public void Dispose()
    {
        if (mesh != null) capi.Render.DeleteMesh(mesh);
        active = null;
    }

    private static void Inject(ref string __result)
    {
        if (!__result.Contains("waterFlagsIn") || !__result.Contains(VertexMain) || __result.Contains("seaClipZone")) return;
        var end = __result.LastIndexOf('}');
        __result = __result.Insert(end, Hidden).Replace(VertexMain, Uniforms + VertexMain);
    }

    private static void SetZone(MeshDataPoolManager __instance, Vec3d playerpos)
    {
        if (active == null || WaterMeshBench.Renderer == null || !WaterMeshBench.LiquidPools.Contains(__instance)) return;
        var shader = active.capi.Render.CurrentActiveShader;
        var seaY = (float)(active.SurfaceY - playerpos.Y);
        var (x, z) = Offset(playerpos);
        var half = On ? Levels[^1].Half : 0;
        shader.Uniform("seaClipZone", new Vec4f(x - half, z - half, x + half, z + half));
        shader.Uniform("seaClipY", seaY);
    }

    private static void Draw(MeshDataPoolManager __instance, Vec3d playerpos, string originUniformName)
    {
        if (!On || active == null || WaterMeshBench.Renderer == null) return;
        active.Ensure();
        if (active.mesh == null || WaterMeshBench.LiquidPools[active.atlas] != __instance) return;
        var (x, z) = Offset(playerpos);
        active.capi.Render.CurrentActiveShader.Uniform(originUniformName, new Vec3f(x, (float)(active.SurfaceY - playerpos.Y), z));
        active.capi.Render.RenderMesh(active.mesh);
    }

    private static (float X, float Z) Offset(Vec3d playerpos) =>
        ((float)(Math.Floor(playerpos.X) - playerpos.X), (float)(Math.Floor(playerpos.Z) - playerpos.Z));

    private double SurfaceY => capi.World.SeaLevel - 1 + StillSurface;

    private void Ensure()
    {
        if (mesh != null) return;
        if (TopFaceSplit.SampledTile is not { } tile) return;
        atlas = tile.atlasNumber;
        mesh = capi.Render.UploadMesh(Build(tile));
        var vao = (VAO)mesh;
        GL.BindVertexArray(vao.VaoId);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vao.customDataIntVboId);
        GL.VertexAttribIPointer(6, 1, VertexAttribIntegerType.Int, 8, (IntPtr)4);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.BindVertexArray(0);
    }

    private static MeshData Build(TextureAtlasPosition tile)
    {
        var data = new MeshData(120000, 700000, false, true, true, true)
        {
            CustomFloats = new CustomMeshDataPartFloat(240000) { InterleaveOffsets = [0], InterleaveSizes = [2], InterleaveStride = 8 },
            CustomInts = new CustomMeshDataPartInt(240000) { InterleaveOffsets = [0, 4], InterleaveSizes = [1, 1], InterleaveStride = 8, Conversion = DataConversion.Integer },
        };
        var vertices = new Dictionary<(int, int), int>();
        var u = (tile.x1 + tile.x2) / 2;
        var v = (tile.y1 + tile.y2) / 2;
        var flags = BlockFacing.AllVertexFlagsNormals[4] | int.MinValue;
        const int waterFlags = 3 | (255 << 2) | (128 << 10) | (128 << 18) | ClipmapFlag;

        int Vertex(float x, float z)
        {
            var key = ((int)Math.Round(x * 8), (int)Math.Round(z * 8));
            if (vertices.TryGetValue(key, out var index)) return index;
            index = data.VerticesCount;
            data.AddVertexWithFlags(x, 0, z, u, v, TopFaceSplit.SampledLight, flags);
            data.CustomInts.Add(0);
            data.CustomInts.Add(waterFlags);
            data.CustomFloats.Add(0);
            data.CustomFloats.Add(0);
            return vertices[key] = index;
        }

        void Triangle(int a, int b, int c)
        {
            data.AddIndex(a);
            data.AddIndex(b);
            data.AddIndex(c);
        }

        var inner = 0f;
        foreach (var (half, spacing) in Levels)
        {
            for (var z = -half; z < half; z += spacing)
            {
                for (var x = -half; x < half; x += spacing)
                {
                    var (xEnd, zEnd) = (x + spacing, z + spacing);
                    if (x >= -inner && xEnd <= inner && z >= -inner && zEnd <= inner) continue;
                    var fineSpacing = spacing / 2;
                    var ring = new List<int> { Vertex(x, z) };
                    void Edge(float fromX, float fromZ, float toX, float toZ)
                    {
                        var midX = (fromX + toX) / 2;
                        var midZ = (fromZ + toZ) / 2;
                        if (inner > 0 && Math.Max(Math.Abs(midX), Math.Abs(midZ)) <= inner) ring.Add(Vertex(midX, midZ));
                        ring.Add(Vertex(toX, toZ));
                    }
                    Edge(x, z, x, zEnd);
                    Edge(x, zEnd, xEnd, zEnd);
                    Edge(xEnd, zEnd, xEnd, z);
                    Edge(xEnd, z, x, z);
                    ring.RemoveAt(ring.Count - 1);
                    if (ring.Count == 4)
                    {
                        Triangle(ring[0], ring[1], ring[2]);
                        Triangle(ring[0], ring[2], ring[3]);
                        continue;
                    }
                    var centre = Vertex(x + fineSpacing, z + fineSpacing);
                    for (var i = 0; i < ring.Count; i++) Triangle(centre, ring[i], ring[(i + 1) % ring.Count]);
                }
            }
            inner = half;
        }
        return data;
    }
}
