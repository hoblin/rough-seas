namespace GameCompat;

internal sealed class CompatibilityReport(string version)
{
    private readonly List<(string Section, string Reason)> reasons = [];

    public string Version { get; } = version;

    public IReadOnlyList<BehaviourChange> ToRead { get; set; } = [];

    public bool NoHardFailures => reasons.Count == 0;

    public bool Compatible => NoHardFailures && ToRead.Count == 0;

    public void Add(string section, IEnumerable<string> found) => reasons.AddRange(found.Select(reason => (section, reason)));

    public void Print()
    {
        Console.WriteLine($"{Version}: {(!NoHardFailures ? "incompatible" : ToRead.Count > 0 ? "needs reading" : "compatible")}");
        foreach (var group in reasons.GroupBy(reason => reason.Section))
        {
            Console.WriteLine($"  {group.Key}:");
            foreach (var (_, reason) in group) Console.WriteLine($"    {reason}");
        }

        foreach (var change in ToRead) Print(change);
    }

    private void Print(BehaviourChange change)
    {
        Console.WriteLine($"{Version} {change.Reliance.Target} {change.Stamp}");
        Console.WriteLine($"    we rely on: {change.Reliance.What}");
        if (change.Gone) Console.WriteLine("    the method does not exist in this version");
        Console.WriteLine(change.SourceDiff);
    }
}
