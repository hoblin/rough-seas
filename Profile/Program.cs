using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: bin/profile report TRACE.nettrace [TOP]");
    return 1;
}

var top = args.Length > 1 ? int.Parse(args[1]) : 30;
using var log = new TraceLog(TraceLog.CreateFromEventPipeDataFile(args[0]));
var report = new Report(args.Length > 2 ? args[2] : null);

foreach (var data in log.Events)
{
    if (data.ProviderName == "Microsoft-DotNETCore-SampleProfiler" && data.EventName == "Thread/Sample")
        report.Sample(data.ThreadID, data.TimeStampRelativeMSec, Frames(data.CallStack()));
    else if (data is GCAllocationTickTraceData tick)
        report.Allocation(tick.TypeName, tick.AllocationAmount64, Frames(data.CallStack()));
}

report.Print(top);
return 0;

static string[] Frames(TraceCallStack? stack)
{
    var frames = new List<string>();
    for (var frame = stack; frame != null; frame = frame.Caller)
        frames.Add(frame.CodeAddress.FullMethodName is { Length: > 0 } name ? name : frame.CodeAddress.ModuleName + "!?");
    return [.. frames];
}

internal class Report(string? focus)
{
    private const string Ours = "RoughSeas.";

    private readonly Dictionary<string, int> under = [];
    private int focused;

    private readonly Dictionary<int, int> samplesByThread = [];
    private readonly Dictionary<string, int> threadSides = [];
    private readonly Dictionary<string, int> entries = [];
    private readonly Dictionary<string, int> inclusive = [];
    private readonly Dictionary<string, int> leaves = [];
    private readonly Dictionary<string, long> bytesByType = [];
    private readonly Dictionary<string, long> bytesBySite = [];
    private long allBytes;
    private long ourBytes;
    private int ourSamples;
    private double first = double.MaxValue;
    private double last;

    public void Sample(int thread, double at, string[] frames)
    {
        first = Math.Min(first, at);
        last = Math.Max(last, at);
        samplesByThread[thread] = samplesByThread.GetValueOrDefault(thread) + 1;
        Focus(frames);
        var outermost = Array.FindLastIndex(frames, IsOurs);
        if (outermost < 0) return;
        ourSamples++;
        Add(threadSides, Side(frames));
        Add(entries, $"{Side(frames)}  {Caller(frames, outermost)} → {frames[outermost]}");
        foreach (var method in frames.Where(IsOurs).Distinct()) Add(inclusive, method);
        var innermost = Array.FindIndex(frames, IsOurs);
        Add(leaves, innermost == 0 ? frames[0] : $"{frames[0]}  ← {frames[innermost]}");
    }

    private void Focus(string[] frames)
    {
        if (focus == null) return;
        var at = Array.FindLastIndex(frames, frame => frame.Contains(focus, StringComparison.Ordinal));
        if (at < 0) return;
        focused++;
        Add(under, at == 0 ? "(self)" : frames[at - 1]);
    }

    public void Allocation(string type, long bytes, string[] frames)
    {
        allBytes += bytes;
        var innermost = Array.FindIndex(frames, IsOurs);
        if (innermost < 0) return;
        ourBytes += bytes;
        bytesByType[type] = bytesByType.GetValueOrDefault(type) + bytes;
        var site = $"{frames[innermost]}  ({type})";
        bytesBySite[site] = bytesBySite.GetValueOrDefault(site) + bytes;
    }

    public void Print(int top)
    {
        var seconds = (last - first) / 1000;
        var period = SamplePeriodMs(seconds);
        string Rate(int samples) => $"{samples * period / seconds,8:0.00} ms/s";
        string Bytes(long bytes) => $"{bytes / seconds / 1024,10:0.0} KB/s";

        Console.WriteLine($"{seconds:0.0} s traced, {samplesByThread.Count} threads, a sample every {period:0.00} ms of a thread");
        Console.WriteLine($"Our code on a stack: {Rate(ourSamples)} of thread time");
        if (focus != null)
        {
            Console.WriteLine($"{focus}: {Rate(focused)} inclusive");
            Section($"Directly under {focus}", under, Rate, top);
            return;
        }
        Section("By side", threadSides, Rate, top);
        Section("Entries: where the engine calls into us (inclusive)", entries, Rate, top);
        Section("Our methods, inclusive", inclusive, Rate, top);
        Section("Leaf frames under our code (self time; ← our innermost frame)", leaves, Rate, top);
        Console.WriteLine();
        Console.WriteLine($"Allocations: {Bytes(allBytes)} in the process, {Bytes(ourBytes)} from our code (sampled per ~100 KB)");
        Section("Our allocations by type", bytesByType, Bytes, top);
        Section("Our allocations by our innermost frame", bytesBySite, Bytes, top);
    }

    private double SamplePeriodMs(double seconds) =>
        samplesByThread.Count == 0 ? 0 : seconds * 1000 / samplesByThread.Values.Max();

    private static void Section<T>(string title, Dictionary<string, T> counts, Func<T, string> format, int top)
    {
        Console.WriteLine();
        Console.WriteLine(title);
        foreach (var (key, value) in counts.OrderByDescending(pair => pair.Value).Take(top))
            Console.WriteLine($"  {format(value)}  {key}");
    }

    private static void Add(Dictionary<string, int> counts, string key) => counts[key] = counts.GetValueOrDefault(key) + 1;

    private static bool IsOurs(string frame) => frame.StartsWith(Ours, StringComparison.Ordinal);

    private static string Caller(string[] frames, int outermost) =>
        outermost + 1 < frames.Length ? frames[outermost + 1] : "(root)";

    private static string Side(string[] frames) =>
        frames.Any(frame => frame.StartsWith("Vintagestory.Server.", StringComparison.Ordinal)) ? "server"
        : frames.Any(frame => frame.StartsWith("Vintagestory.Client.", StringComparison.Ordinal)) ? "client"
        : "other";
}
