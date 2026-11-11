using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HarmonyLib;
using OpenTK.Graphics.OpenGL;
using Vintagestory.API.Client;
using Vintagestory.Client.NoObf;

namespace AgentBridge;

public sealed class WaterPasses : IDisposable
{
    private const string HarmonyId = "agentbridge.waterpasses";
    private const int LiquidPass = 4;
    private const int Ring = 8;

    private static readonly AccessTools.FieldRef<ChunkRenderer, MeshDataPoolManager[][]> PoolsByPass =
        AccessTools.FieldRefAccess<ChunkRenderer, MeshDataPoolManager[][]>("poolsByRenderPass");
    private static readonly GpuPass Depth = new();
    private static readonly GpuPass Surface = new();
    private static ChunkRenderer? renderer;

    private readonly Harmony harmony = new(HarmonyId);

    public WaterPasses()
    {
        Patch(nameof(ChunkRenderer.OnRenderBefore), nameof(BeginDepth), nameof(EndDepth));
        Patch("RenderOIT", nameof(BeginSurface), nameof(EndSurface));
    }

    public async Task<string> Measure(double seconds)
    {
        Depth.Reset();
        Surface.Reset();
        await Task.Delay(TimeSpan.FromSeconds(seconds));
        long memory = 0, rendered = 0, allocated = 0;
        foreach (var pool in renderer == null ? [] : PoolsByPass(renderer)[LiquidPass].Where(pool => pool != null)) pool.GetStats(ref memory, ref rendered, ref allocated);
        return JsonSerializer.Serialize(new
        {
            depthPassMs = Depth.MeanMs,
            surfacePassMs = Surface.MeanMs,
            frames = Surface.Samples,
            surfaceTriangles = Surface.MeanPrimitives,
            liquidPoolMb = memory >> 20
        }) + "\n";
    }

    public void Dispose() => harmony.UnpatchAll(HarmonyId);

    private void Patch(string method, string prefix, string postfix) =>
        harmony.Patch(AccessTools.Method(typeof(ChunkRenderer), method), new HarmonyMethod(typeof(WaterPasses), prefix), new HarmonyMethod(typeof(WaterPasses), postfix));

    private static void BeginDepth(ChunkRenderer __instance)
    {
        renderer = __instance;
        Depth.Begin();
    }

    private static void EndDepth() => Depth.End();

    private static void BeginSurface() => Surface.Begin();

    private static void EndSurface() => Surface.End();

    private sealed class GpuPass
    {
        private readonly int[] times = new int[Ring];
        private readonly int[] primitives = new int[Ring];
        private readonly bool[] issued = new bool[Ring];
        private int at;
        private long nanoseconds;
        private long drawn;

        public long Samples { get; private set; }

        public double MeanMs => Samples == 0 ? 0 : Math.Round(nanoseconds / 1e6 / Samples, 3);

        public long MeanPrimitives => Samples == 0 ? 0 : drawn / Samples;

        public void Reset() => (nanoseconds, drawn, Samples) = (0, 0, 0);

        public void Begin()
        {
            if (times[at] == 0) (times[at], primitives[at]) = (GL.GenQuery(), GL.GenQuery());
            if (issued[at])
            {
                GL.GetQueryObject(times[at], GetQueryObjectParam.QueryResult, out long elapsed);
                GL.GetQueryObject(primitives[at], GetQueryObjectParam.QueryResult, out long generated);
                nanoseconds += elapsed;
                drawn += generated;
                Samples++;
            }
            GL.BeginQuery(QueryTarget.TimeElapsed, times[at]);
            GL.BeginQuery(QueryTarget.PrimitivesGenerated, primitives[at]);
        }

        public void End()
        {
            GL.EndQuery(QueryTarget.PrimitivesGenerated);
            GL.EndQuery(QueryTarget.TimeElapsed);
            issued[at] = true;
            at = (at + 1) % Ring;
        }
    }
}
