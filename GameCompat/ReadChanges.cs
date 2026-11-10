namespace GameCompat;

internal sealed class ReadChanges
{
    private const string FileName = "GameCompat/read-changes.txt";

    private readonly HashSet<(string Version, string Target, string Stamp)> entries;

    private ReadChanges(HashSet<(string, string, string)> entries) => this.entries = entries;

    public static ReadChanges Load(string repository)
    {
        var path = Path.Combine(repository, FileName);
        var lines = File.Exists(path) ? File.ReadAllLines(path) : [];
        return new ReadChanges(lines.Select(line => line.Split(' ', 4)).Where(parts => parts.Length >= 3).Select(parts => (parts[0], parts[1], parts[2])).ToHashSet());
    }

    public bool Holds(string version, BehaviourChange change) => entries.Contains((version, change.Reliance.Target, change.Stamp));
}
