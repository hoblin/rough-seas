using System;
using System.Diagnostics;
using System.Threading;
using Vintagestory.API.Server;

namespace RoughSeas.SeaAndWeather;

public class TickBudget
{
    [ThreadStatic] private static int depth;

    private long spent;
    private long calls;
    private long spentAtTick;
    private long worstTick;
    private long ticks;
    private long spentAtReport;
    private long callsAtReport;
    private long reportedAt = Stopwatch.GetTimestamp();
    private bool enabled;

    public void CountTicks(ICoreServerAPI sapi)
    {
        enabled = true;
        sapi.Event.RegisterGameTickListener(_ => Tick(), 0);
    }

    /// <summary>Times the code until disposed; scopes nested on one thread count once, as the outermost.</summary>
    public Scope Measure() => new(enabled ? this : null);

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

    private void Add(long elapsed)
    {
        Interlocked.Add(ref spent, elapsed);
        Interlocked.Increment(ref calls);
    }

    private static double Milliseconds(long timestamps) => timestamps * 1000.0 / Stopwatch.Frequency;

    public readonly struct Scope : IDisposable
    {
        private readonly TickBudget? budget;
        private readonly bool outermost;
        private readonly long began;

        internal Scope(TickBudget? budget)
        {
            this.budget = budget;
            if (budget == null) return;
            outermost = depth++ == 0;
            if (outermost) began = Stopwatch.GetTimestamp();
        }

        public void Dispose()
        {
            if (budget == null) return;
            depth--;
            if (outermost) budget.Add(Stopwatch.GetTimestamp() - began);
        }
    }
}
