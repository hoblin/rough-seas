using System.Diagnostics;
using System.Threading;
using Vintagestory.API.Server;

namespace RoughSeas.SeaAndWeather;

public class TickBudget
{
    private long spent;
    private long calls;
    private long ticks;
    private long since = Stopwatch.GetTimestamp();

    public bool Enabled { get; private set; }

    public void CountTicks(ICoreServerAPI sapi)
    {
        Enabled = true;
        sapi.Event.RegisterGameTickListener(_ => Interlocked.Increment(ref ticks), 0);
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
        var spentMs = Interlocked.Exchange(ref spent, 0) * 1000.0 / Stopwatch.Frequency;
        var callCount = Interlocked.Exchange(ref calls, 0);
        var tickCount = Interlocked.Exchange(ref ticks, 0);
        var seconds = (now - Interlocked.Exchange(ref since, now)) / (double)Stopwatch.Frequency;
        var perTick = tickCount > 0 ? spentMs / tickCount : 0;
        return $"Over {seconds:0.0} s: {tickCount} server ticks, {callCount} calls, {spentMs:0.00} ms in sea and weather, {perTick:0.000} ms per tick";
    }
}
