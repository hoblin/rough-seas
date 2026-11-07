using Newtonsoft.Json.Linq;

namespace RoughSeas.Tests.Shipyard;

public class BurnTimeTests
{
    private const double FirewoodSeconds = 24;
    private const int FirewoodPerLog = 4;
    private const int FirewoodPerHardLog = 3;
    private const int LathsPerPole = 16;

    private static readonly JToken Pole = Load("resources", "pole.json");
    private static readonly JToken Lath = Load("resources", "lath.json");
    private static readonly int PolesPerRow = Load("blocks", "wedge.json")["attributes"]!["splits"]!.First!.Value<int>("count");

    public static TheoryData<int> Lengths => [2, 3, 4, 5];

    [Theory]
    [MemberData(nameof(Lengths))]
    public void ThePolesOfARowBurnAsLongAsTheFirewoodOfItsLogs(int logs)
    {
        Assert.Equal(logs * FirewoodPerLog * FirewoodSeconds, PolesPerRow * Burn(Pole, $"*-{logs}"));
        Assert.Equal(logs * FirewoodPerHardLog * FirewoodSeconds, PolesPerRow * Burn(Pole, $"@pole-(kapok|purpleheart|ebony)-{logs}"));
    }

    [Theory]
    [MemberData(nameof(Lengths))]
    public void TheLathsOfAPoleBurnAsLongAsThePole(int metres)
    {
        Assert.Equal(Burn(Pole, $"*-{metres}"), LathsPerPole * Burn(Lath, $"*-{metres}-*"));
        Assert.Equal(Burn(Pole, $"@pole-(kapok|purpleheart|ebony)-{metres}"), LathsPerPole * Burn(Lath, $"@lath-(kapok|purpleheart|ebony)-{metres}-.*"));
    }

    private static double Burn(JToken item, string type) => item["combustiblePropsByType"]![type]!.Value<double>("burnDuration");

    private static JToken Load(string folder, string file) => JToken.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, folder, file)));
}
