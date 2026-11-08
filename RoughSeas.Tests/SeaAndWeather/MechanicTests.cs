using Newtonsoft.Json.Linq;
using RoughSeas.SeaAndWeather;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Tests.SeaAndWeather;

public class MechanicTests
{
    private static readonly string[] Codes = Mechanic.All.Select(mechanic => mechanic.Code).ToArray();

    [Fact]
    public void EveryMechanicIsOnInAWorldThatNeverSetIt()
    {
        var config = new TreeAttribute();

        Assert.All(Mechanic.All, mechanic => Assert.True(mechanic.On(config)));
        Assert.Null(Mechanic.Migrate(config));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AWorldSavedWithDangerousOceansTakesItsValueForEveryMechanicOnce(bool dangerous)
    {
        var config = new TreeAttribute();
        config.SetBool(Mechanic.LegacyDangerousOceans, dangerous);

        Assert.Equal(dangerous, Mechanic.Migrate(config));
        Assert.All(Mechanic.All, mechanic => Assert.Equal(dangerous, mechanic.On(config)));
        Assert.False(config.HasAttribute(Mechanic.LegacyDangerousOceans));
    }

    [Fact]
    public void AMechanicChangedAfterTheMigrationStaysChanged()
    {
        var config = new TreeAttribute();
        config.SetBool(Mechanic.LegacyDangerousOceans, false);
        Mechanic.Migrate(config);

        config.SetBool(Mechanic.HullDamage.Code, true);

        Assert.Null(Mechanic.Migrate(config));
        Assert.True(Mechanic.HullDamage.On(config));
        Assert.False(Mechanic.BilgeWater.On(config));
    }

    [Fact]
    public void TheWorldSettingsDeclareEveryMechanicAndNothingElse()
    {
        var declared = JObject.Parse(File.ReadAllText(Path.Combine(Repository.Mod, "worldconfig.json")))["worldConfigAttributes"]!
            .Select(attribute => (string)attribute["code"]!);

        Assert.Equal(Codes, declared);
    }

    public static TheoryData<string> Locales()
    {
        var locales = new TheoryData<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Repository.Mod, "assets", "game", "lang"), "worldconfig-*.json")) locales.Add(Path.GetFileName(file));
        return locales;
    }

    [Theory]
    [MemberData(nameof(Locales))]
    public void EveryLocaleNamesAndDescribesEveryMechanic(string file)
    {
        var lang = JObject.Parse(File.ReadAllText(Path.Combine(Repository.Mod, "assets", "game", "lang", file)));
        var keys = Codes.SelectMany(code => new[] { $"worldattribute-{code}", $"worldattribute-{code}-desc" });

        Assert.All(keys, key => Assert.False(string.IsNullOrWhiteSpace((string?)lang[key]), $"{file} lacks {key}"));
        Assert.DoesNotContain(lang.Properties(), property => property.Name.Contains(Mechanic.LegacyDangerousOceans));
    }
}
