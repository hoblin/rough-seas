using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Vintagestory.API.Client;

namespace AgentBridge;

public sealed class FrameTimer : IRenderer
{
    private readonly ICoreClientAPI capi;
    private readonly object framesLock = new();
    private readonly SemaphoreSlim oneAtATime = new(1, 1);
    private List<float>? frames;

    public FrameTimer(ICoreClientAPI capi)
    {
        this.capi = capi;
        capi.Event.RegisterRenderer(this, EnumRenderStage.Done, "agentbridge-frametimer");
    }

    public double RenderOrder => 1;

    public int RenderRange => 0;

    public async Task<string> Measure(double seconds)
    {
        float[] measured;
        await oneAtATime.WaitAsync();
        try
        {
            lock (framesLock) frames = [];
            await Task.Delay(TimeSpan.FromSeconds(seconds));
            lock (framesLock)
            {
                measured = frames!.ToArray();
                frames = null;
            }
        }
        finally
        {
            oneAtATime.Release();
        }
        if (measured.Length == 0) return "{\"frames\":0}\n";

        Array.Sort(measured);
        return JsonSerializer.Serialize(new
        {
            frames = measured.Length,
            fps = Math.Round(measured.Length / measured.Sum(), 1),
            meanMs = Math.Round(measured.Average() * 1000, 2),
            p95Ms = Math.Round(measured[(int)(measured.Length * 0.95)] * 1000, 2)
        }) + "\n";
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        lock (framesLock) frames?.Add(deltaTime);
    }

    public void Dispose() => capi.Event.UnregisterRenderer(this, EnumRenderStage.Done);
}
