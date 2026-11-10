namespace GameCompat;

internal sealed class ReadChanges
{
    private const string FileName = "GameCompat/read-changes.txt";

    private readonly HashSet<(string Version, string Target, string Stamp)> entries;

    internal ReadChanges(IEnumerable<string> lines) =>
        entries = lines.Select(line => line.Trim().Split(' ', 4)).Where(parts => parts.Length >= 3).Select(parts => (parts[0], parts[1], parts[2])).ToHashSet();

    public static ReadChanges Load(string repository)
    {
        var path = Path.Combine(repository, FileName);
        return new ReadChanges(File.Exists(path) ? File.ReadAllLines(path) : []);
    }

    public bool Holds(string version, BehaviourChange change) => entries.Contains((version, change.Reliance.Target, change.Stamp));
}
