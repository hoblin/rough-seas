using System.Collections.Generic;
using ProperVersion;

namespace RoughSeas.Compat.ExtremeWeather;

public class CompatConfig
{
    public TestedVersions Tested = new();
    public Dictionary<string, object> Settings = [];
    public TornadoConfig Tornadoes = new();
}

public class TestedVersions
{
    public string From = "";
    public string To = "";

    public bool Covers(string version) =>
        SemVer.TryParse(version, out var tried) && tried >= SemVer.Parse(From) && tried <= SemVer.Parse(To);

    public override string ToString() => From == To ? From : $"{From} to {To}";
}

public class TornadoConfig
{
    public string StormCode = "";
    public double MinPresence;
    public double CheckSeconds;
}
