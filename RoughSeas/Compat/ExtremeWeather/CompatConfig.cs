using System.Collections.Generic;

namespace RoughSeas.Compat.ExtremeWeather;

public class CompatConfig : TestedConfig
{
    public Dictionary<string, object> Settings = [];
    public TornadoConfig Tornadoes = new();
}

public class TornadoConfig
{
    public string StormCode = "";
    public double MinPresence;
    public double CheckSeconds;
}
