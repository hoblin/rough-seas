using Newtonsoft.Json.Linq;
using RoughSeas.SeaAndWeather;

namespace RoughSeas.Tests.SeaAndWeather;

public class WindOnCoordinatesTests
{
    private static readonly string[] Points = ["n", "nne", "ne", "ene", "e", "ese", "se", "sse", "s", "ssw", "sw", "wsw", "w", "wnw", "nw", "nnw"];

    [Theory]
    [InlineData(255, "wsw")]
    [InlineData(358, "n")]
    [InlineData(90, "e")]
    [InlineData(180, "s")]
    public void NamesThePointTheWindBlowsFrom(double fromDegrees, string point)
    {
        var (towardX, towardZ) = Compass.Toward((fromDegrees + 180) * Math.PI / 180);
        Assert.Equal(point, WindOnCoordinates.Point(new SeaState(towardX, towardZ, 0, 0, 0, 0, null).WindFromDegrees));
    }

    public static TheoryData<string> Locales() => new(Directory.GetFiles("lang", "*.json").Select(Path.GetFileNameWithoutExtension)!);

    [Theory]
    [MemberData(nameof(Locales))]
    public void SpeaksTheWindLineInEveryLocale(string locale)
    {
        var lang = JObject.Parse(File.ReadAllText(Path.Combine("lang", locale + ".json")));
        Assert.All(Points.Select(point => "wind-from-" + point).Append("wind-line"), key => Assert.False(string.IsNullOrEmpty((string?)lang[key]), key));
    }
}
