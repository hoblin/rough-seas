using ProperVersion;

namespace RoughSeas.Compat;

public class TestedVersions
{
    public string From = "";
    public string To = "";

    public bool Covers(string version) =>
        SemVer.TryParse(version, out var tried) && tried >= SemVer.Parse(From) && tried <= SemVer.Parse(To);

    public override string ToString() => From == To ? From : $"{From} to {To}";
}

public class TestedConfig
{
    public TestedVersions Tested = new();
}
