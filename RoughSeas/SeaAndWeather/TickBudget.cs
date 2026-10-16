using System;
using System.Diagnostics;
using System.Threading;
using Vintagestory.API.Server;

namespace RoughSeas.SeaAndWeather;

public class TickBudget
{
    private long spent;
    private long calls;
    private long spentAtTick;
    private long worstTick;
    private long ticks;
    private long spentAtReport;
    private long callsAtReport;
    private long reportedAt = Stopwatch.GetTimestamp();

    public bool Enabled { get; private set; }

    public void CountTicks(ICoreServerAPI sapi)
    {
        Enabled = true;
        sapi.Event.RegisterGameTickListener(_ => Tick(), 0);
    }

    public long Begin() => Enabled ? Stopwatch.GetTimestamp() : 0;

    public void End(long began)
    {
        if (!Enabled) return;
        Interlocked.Add(ref spent, Stopwatch.GetTimestamp() - began);
        Interlocked.Increment(ref calls);
    }

    public string Report()
    {
        var now = Stopwatch.GetTimestamp();
        var total = Interlocked.Read(ref spent);
        var totalCalls = Interlocked.Read(ref calls);
        var spentMs = Milliseconds(total - spentAtReport);
        var seconds = (now - reportedAt) / (double)Stopwatch.Frequency;
        var report = $"Over {seconds:0.0} s: {ticks} server ticks, {totalCalls - callsAtReport} calls, {spentMs:0.00} ms in sea and weather, "
            + $"{(ticks > 0 ? spentMs / ticks : 0):0.000} ms per tick, worst tick {Milliseconds(worstTick):0.000} ms";
        (spentAtReport, callsAtReport, reportedAt, ticks, worstTick) = (total, totalCalls, now, 0, 0);
        return report;
    }

    private void Tick()
    {
        var total = Interlocked.Read(ref spent);
        worstTick = Math.Max(worstTick, total - spentAtTick);
        spentAtTick = total;
        ticks++;
    }

    private static double Milliseconds(long timestamps) => timestamps * 1000.0 / Stopwatch.Frequency;
}
