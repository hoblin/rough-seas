using Newtonsoft.Json.Linq;
using RoughSeas.SeaAndWeather;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.Common;

namespace RoughSeas.Tests.SeaAndWeather;

public class WindOnCoordinatesTests
{
    private static readonly string[] Points = ["n", "nne", "ne", "ene", "e", "ese", "se", "sse", "s", "ssw", "sw", "wsw", "w", "wnw", "nw", "nnw"];

    static WindOnCoordinatesTests()
    {
        var assets = new AssetManager(AppContext.BaseDirectory, EnumAppSide.Client) { Origins = [new PathOrigin("roughseas", AppContext.BaseDirectory)] };
        foreach (var locale in new[] { "en", "ru" })
        {
            var translations = new TranslationService(locale, new NullLogger(), assets);
            translations.Load();
            Lang.AvailableLanguages[locale] = translations;
        }
    }

    private static SeaState WindFrom(double degrees, double mps, int beaufort)
    {
        var (towardX, towardZ) = Compass.Toward((degrees + 180) * Math.PI / 180);
        return new SeaState(towardX * mps, towardZ * mps, beaufort, 0, 0, 0, null);
    }

    [Theory]
    [InlineData(255, "wsw")]
    [InlineData(358, "n")]
    [InlineData(90, "e")]
    [InlineData(180, "s")]
    public void NamesThePointTheWindBlowsFrom(double fromDegrees, string point)
    {
        Assert.Equal(point, WindOnCoordinates.Point(WindFrom(fromDegrees, 1, 0).WindFromDegrees));
    }

    [Theory]
    [InlineData("en", 0, 0.2, 0, "N 0 Bft · 0 m/s")]
    [InlineData("en", 90, 1.4, 1, "E 1 Bft · 1 m/s")]
    [InlineData("en", 180, 2.6, 2, "S 2 Bft · 3 m/s")]
    [InlineData("en", 225, 9.7, 5, "SW 5 Bft · 10 m/s")]
    [InlineData("en", 255, 30.4, 11, "WSW 11 Bft · 30 m/s")]
    [InlineData("en", 338, 35.8, 12, "NNW 12 Bft · 36 m/s")]
    [InlineData("ru", 0, 0.2, 0, "С 0 баллов · 0 м/с")]
    [InlineData("ru", 90, 1.4, 1, "В 1 балл · 1 м/с")]
    [InlineData("ru", 180, 2.6, 2, "Ю 2 балла · 3 м/с")]
    [InlineData("ru", 225, 9.7, 5, "ЮЗ 5 баллов · 10 м/с")]
    [InlineData("ru", 255, 30.4, 11, "ЗЮЗ 11 баллов · 30 м/с")]
    [InlineData("ru", 338, 35.8, 12, "ССЗ 12 баллов · 36 м/с")]
    public void SpeaksTheWindLine(string locale, double fromDegrees, double mps, int beaufort, string line)
    {
        Lang.ChangeLanguage(locale);
        Assert.Equal(line, WindOnCoordinates.Describe(WindFrom(fromDegrees, mps, beaufort)));
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
