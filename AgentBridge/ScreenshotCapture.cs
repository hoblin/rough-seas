using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Vintagestory.API.Client;

namespace AgentBridge;

public class ScreenshotCapture : IRenderer
{
    private readonly ICoreClientAPI capi;
    private readonly Queue<TaskCompletionSource<byte[]>> pending = new();
    private const int SettleFrames = 2;
    private bool showGuisAfter;
    private int settling;

    public ScreenshotCapture(ICoreClientAPI capi)
    {
        this.capi = capi;
        capi.Event.RegisterRenderer(this, EnumRenderStage.Done, "agentbridge-screenshot");
    }

    public double RenderOrder => 1;
    public int RenderRange => 0;

    public Task<byte[]> Next(bool withGuis)
    {
        var shot = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        capi.Event.EnqueueMainThreadTask(() =>
        {
            if (!withGuis && !capi.HideGuis)
            {
                capi.TriggerChatMessage(".gui off");
                showGuisAfter = true;
                settling = SettleFrames;
            }
            pending.Enqueue(shot);
        }, "agentbridge-screenshot");
        return shot.Task;
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (pending.Count == 0) return;
        // The frame that turns the GUI off has drawn the hand and the held item already.
        if (settling-- > 0) return;

        var path = Path.Combine(Path.GetTempPath(), "agentbridge-screenshot.png");
        using (var bitmap = capi.Render.GrabScreenshot(capi.Render.FrameWidth, capi.Render.FrameHeight, false, true))
        {
            bitmap.Save(path);
        }
        var png = File.ReadAllBytes(path);
        while (pending.TryDequeue(out var shot)) shot.SetResult(png);
        if (!showGuisAfter) return;

        capi.TriggerChatMessage(".gui on");
        showGuisAfter = false;
    }

    public void Dispose()
    {
        capi.Event.UnregisterRenderer(this, EnumRenderStage.Done);
    }
}
