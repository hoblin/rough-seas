using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using OpenTK.Graphics.OpenGL;
using Vintagestory.API.Client;
using Vintagestory.Client.NoObf;

namespace AgentBridge.WaterMesh;

public sealed class WaterMeshBench : IDisposable
{
    private const string HarmonyId = "agentbridge.watermesh";
    private const int LiquidPass = 4;
    private const int Ring = 8;

    private static readonly AccessTools.FieldRef<ChunkRenderer, MeshDataPoolManager[][]> PoolsByPass =
        AccessTools.FieldRefAccess<ChunkRenderer, MeshDataPoolManager[][]>("poolsByRenderPass");

    private static readonly GpuPass Depth = new();
    private static readonly GpuPass Surface = new();
    private static long tesselateTicks;
    private static long tesselated;

    [ThreadStatic]
    private static long tesselateStart;

    private readonly Harmony harmony = new(HarmonyId);
    private readonly ICoreClientAPI capi;
    private readonly WaterClipmap clipmap;

    public WaterMeshBench(ICoreClientAPI capi)
    {
        this.capi = capi;
        clipmap = new WaterClipmap(capi);
        Patch(typeof(ChunkRenderer), nameof(ChunkRenderer.OnRenderBefore), nameof(BeginDepth), nameof(EndDepth));
        Patch(typeof(ChunkRenderer), "RenderOIT", nameof(BeginSurface), nameof(EndSurface));
        Patch(typeof(ChunkTesselator), nameof(ChunkTesselator.NowProcessChunk), nameof(BeginTesselate), nameof(EndTesselate));
        TopFaceSplit.Attach(harmony);
        WaterTessellation.Attach(harmony);
        clipmap.Attach(harmony);
        new WaterHeightColours(capi).Attach(harmony);
    }

    internal static ChunkRenderer? Renderer { get; private set; }

    internal static MeshDataPoolManager[] LiquidPools => PoolsByPass(Renderer!)[LiquidPass];

    public string Set(string way, int amount)
    {
        if (way == "colours")
        {
            WaterHeightColours.On = amount != 0;
            return $"height colours {WaterHeightColours.On}\n";
        }
        if (way == "ripple")
        {
            WaterHeightColours.Ripple = amount;
            return $"ripple {amount}\n";
        }
        if (way == "face")
        {
            WaterHeightColours.Face = amount;
            return $"face {amount}\n";
        }
        if (way == "lag")
        {
            WaterHeightColours.Lag = amount;
            return $"lag {amount}\n";
        }
        if (way == "refraction")
        {
            WaterHeightColours.Refraction = amount;
            return $"refraction {amount}\n";
        }
        if (way == "shape")
        {
            WaterHeightColours.Shape = amount / 100f;
            return $"shape {amount}%\n";
        }
        var redraw = TopFaceSplit.Cells != 1;
        var reload = WaterTessellation.MaxLevel != 1 || WaterClipmap.On;
        TopFaceSplit.Cells = 1;
        WaterTessellation.MaxLevel = 1;
        WaterClipmap.On = false;
        switch (way)
        {
            case "off":
                break;
            case "split":
                TopFaceSplit.Cells = amount;
                redraw = true;
                break;
            case "tess":
            case "tessq":
                WaterTessellation.MaxLevel = amount;
                WaterTessellation.Stepped = way == "tessq";
                reload = true;
                break;
            case "clipmap":
                WaterClipmap.On = true;
                reload = true;
                break;
            default:
                throw new ArgumentException("way: off, split N, tess N, clipmap");
        }
        if (reload) capi.Shader.ReloadShaders();
        if (redraw) AccessTools.Method(typeof(ClientMain), "RedrawAllBlocks").Invoke(capi.World, null);
        return $"{way} {amount}\n";
    }

    public async Task<string> Measure(double seconds)
    {
        Depth.Reset();
        Surface.Reset();
        var ticksBefore = Interlocked.Read(ref tesselateTicks);
        var countBefore = Interlocked.Read(ref tesselated);
        await Task.Delay(TimeSpan.FromSeconds(seconds));
        long memory = 0, rendered = 0, allocated = 0;
        foreach (var pool in Renderer == null ? [] : LiquidPools.Where(pool => pool != null)) pool.GetStats(ref memory, ref rendered, ref allocated);
        var chunks = Interlocked.Read(ref tesselated) - countBefore;
        var ticks = Interlocked.Read(ref tesselateTicks) - ticksBefore;
        return JsonSerializer.Serialize(new
        {
            depthPassMs = Depth.MeanMs,
            surfacePassMs = Surface.MeanMs,
            gpuSamples = Surface.Samples,
            surfaceTris = Surface.MeanPrimitives,
            liquidPoolMb = memory >> 20,
            liquidTrisRendered = rendered,
            liquidTrisAllocated = allocated,
            chunksTesselated = chunks,
            msPerChunk = chunks > 0 ? Math.Round(ticks * 1000.0 / Stopwatch.Frequency / chunks, 3) : 0
        }) + "\n";
    }

    public void Dispose()
    {
        harmony.UnpatchAll(HarmonyId);
        clipmap.Dispose();
    }

    private void Patch(Type type, string method, string prefix, string postfix) =>
        harmony.Patch(AccessTools.Method(type, method), new HarmonyMethod(typeof(WaterMeshBench), prefix), new HarmonyMethod(typeof(WaterMeshBench), postfix));

    private static void BeginDepth(ChunkRenderer __instance)
    {
        Renderer = __instance;
        WaterTessellation.PreparePools();
        Depth.Begin();
    }

    private static void EndDepth() => Depth.End();

    private static void BeginSurface()
    {
        WaterTessellation.PreparePools();
        Surface.Begin();
    }

    private static void EndSurface() => Surface.End();

    private static void BeginTesselate() => tesselateStart = Stopwatch.GetTimestamp();

    private static void EndTesselate()
    {
        Interlocked.Add(ref tesselateTicks, Stopwatch.GetTimestamp() - tesselateStart);
        Interlocked.Increment(ref tesselated);
    }

    private sealed class GpuPass
    {
        private readonly int[] queries = new int[Ring];
        private readonly int[] primitiveQueries = new int[Ring];
        private readonly bool[] issued = new bool[Ring];
        private int at;
        private long nanoseconds;
        private long primitives;

        public long Samples { get; private set; }

        public double MeanMs => Samples == 0 ? 0 : Math.Round(nanoseconds / 1e6 / Samples, 3);

        public long MeanPrimitives => Samples == 0 ? 0 : primitives / Samples;

        public void Reset() => (nanoseconds, primitives, Samples) = (0, 0, 0);

        public void Begin()
        {
            if (queries[at] == 0) queries[at] = GL.GenQuery();
            if (primitiveQueries[at] == 0) primitiveQueries[at] = GL.GenQuery();
            if (issued[at])
            {
                GL.GetQueryObject(queries[at], GetQueryObjectParam.QueryResult, out long elapsed);
                GL.GetQueryObject(primitiveQueries[at], GetQueryObjectParam.QueryResult, out long generated);
                nanoseconds += elapsed;
                primitives += generated;
                Samples++;
            }
            GL.BeginQuery(QueryTarget.TimeElapsed, queries[at]);
            GL.BeginQuery(QueryTarget.PrimitivesGenerated, primitiveQueries[at]);
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
