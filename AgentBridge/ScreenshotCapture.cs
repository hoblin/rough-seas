using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace AgentBridge;

public class ScreenshotCapture : IRenderer
{
    private readonly ICoreClientAPI capi;
    private readonly Queue<TaskCompletionSource<byte[]>> pending = new();
    private const int SettleFrames = 2;
    private bool showGuisAfter;
    private int settling;
    private Burst? burst;

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
            HideGuis(withGuis);
            pending.Enqueue(shot);
        }, "agentbridge-screenshot");
        return shot.Task;
    }

    public Task<string> Series(string folder, int count, float seconds)
    {
        var series = new Burst(folder, count, seconds / count);
        capi.Event.EnqueueMainThreadTask(() =>
        {
            if (burst != null)
            {
                series.Done.SetException(new IOException("A burst is already being taken"));
                return;
            }
            HideGuis(false);
            burst = series;
        }, "agentbridge-screenshot");
        return series.Done.Task;
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (pending.Count == 0 && burst == null) return;
        // The frame that turns the GUI off has drawn the hand and the held item already.
        if (settling > 0)
        {
            settling--;
            return;
        }

        if (burst != null)
        {
            if (!burst.Take(Grab, deltaTime)) return;
            burst = null;
        }
        if (pending.Count > 0)
        {
            var path = Path.Combine(Path.GetTempPath(), "agentbridge-screenshot.png");
            using (var bitmap = Grab())
            {
                bitmap.Save(path);
            }
            var png = File.ReadAllBytes(path);
            while (pending.TryDequeue(out var shot)) shot.SetResult(png);
        }
        if (!showGuisAfter) return;

        capi.TriggerChatMessage(".gui on");
        showGuisAfter = false;
    }

    private BitmapRef Grab() => capi.Render.GrabScreenshot(capi.Render.FrameWidth, capi.Render.FrameHeight, false, true);

    private void HideGuis(bool withGuis)
    {
        if (withGuis || capi.HideGuis) return;
        capi.TriggerChatMessage(".gui off");
        showGuisAfter = true;
        settling = SettleFrames;
    }

    public void Dispose()
    {
        capi.Event.UnregisterRenderer(this, EnumRenderStage.Done);
    }

    private class Burst(string folder, int count, float interval)
    {
        private readonly List<(float At, BitmapRef Frame)> frames = [];
        private float elapsed;
        public readonly TaskCompletionSource<string> Done = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Take(Func<BitmapRef> grab, float deltaTime)
        {
            if (frames.Count > 0) elapsed += deltaTime;
            if (frames.Count > 0 && elapsed < frames.Count * interval) return false;

            frames.Add((elapsed, grab()));
            if (frames.Count < count) return false;

            try
            {
                Directory.CreateDirectory(folder);
                var lines = frames.Select((frame, index) =>
                {
                    var path = Path.Combine(folder, $"{index + 1:D2}.png");
                    frame.Frame.Save(path);
                    return $"{path} {frame.At.ToString("0.000", CultureInfo.InvariantCulture)}";
                }).ToList();
                Done.SetResult(string.Join("\n", lines) + "\n");
            }
            catch (Exception e)
            {
                Done.SetException(e);
            }
            finally
            {
                foreach (var frame in frames) frame.Frame.Dispose();
            }
            return true;
        }
    }
}
