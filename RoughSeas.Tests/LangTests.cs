using Newtonsoft.Json.Linq;

namespace RoughSeas.Tests;

public class LangTests
{
    private static readonly string LangFolder = Path.Combine(Repository.Mod, "assets", "roughseas", "lang");
    private static readonly JObject English = Load("en");

    private static IEnumerable<JObject> Vessels() =>
        Directory.EnumerateFiles(Path.Combine(Repository.Mod, "assets", "roughseas", "entities", "vessel"), "*.json")
            .Select(file => JObject.Parse(File.ReadAllText(file)));

    private static JObject Load(string locale) => JObject.Parse(File.ReadAllText(Path.Combine(LangFolder, $"{locale}.json")));

    public static TheoryData<string> Locales()
    {
        var locales = new TheoryData<string>();
        foreach (var file in Directory.EnumerateFiles(LangFolder, "*.json")) locales.Add(Path.GetFileNameWithoutExtension(file));
        return locales;
    }

    [Theory]
    [MemberData(nameof(Locales))]
    public void EveryLocaleTranslatesEveryEnglishText(string locale)
    {
        var lang = Load(locale);

        Assert.All(English.Properties(), english => Assert.False(string.IsNullOrWhiteSpace((string?)lang[english.Name]), $"{locale} lacks {english.Name}"));
    }

    [Fact]
    public void EveryBuildStageHasItsProgressLine()
    {
        var groups = Vessels().SelectMany(vessel => vessel.SelectTokens("$..stages[*].group")).Select(group => ((string)group!).ToLowerInvariant()).Distinct();

        Assert.All(groups, group => Assert.NotNull(English[$"shipyard-stage-{group}"]));
    }

    [Fact]
    public void EverySelectionBoxOfAVesselIsNamedButTheHull()
    {
        var boxes = Vessels().SelectMany(vessel => vessel.SelectTokens("$..selectionBoxes[*]")).Select(box => (string)box!).Distinct().Where(box => box != "HullAP");

        Assert.All(boxes, box => Assert.NotNull(English[$"creature-roughseas:*-selectionbox-{box}"]));
    }
}
