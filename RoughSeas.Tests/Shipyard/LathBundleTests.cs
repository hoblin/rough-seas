using Newtonsoft.Json.Linq;

namespace RoughSeas.Tests.Shipyard;

public class LathBundleTests
{
    private static readonly JToken Lath = Load("itemtypes", "resource", "lath.json");
    private static readonly JToken Bundle = Load("itemtypes", "resource", "lathbundle.json");
    private static readonly JToken Entity = Load("entities", "lathbundle.json");
    private static readonly JArray Recipes = (JArray)Load("recipes", "grid", "lathbundles.json");
    private static readonly JArray Barrel = (JArray)Load("recipes", "barrel", "lath.json");

    private const int VanillaStoneStack = 64;

    public static TheoryData<int> Lengths => [2, 3, 4, 5];

    [Theory]
    [MemberData(nameof(Lengths))]
    public void ABundleTakesAFullStackOfDryLathsOfOneLength(int metres)
    {
        var laths = Weighted(metres)["ingredients"]!["L"]!;

        Assert.Equal($"roughseas:lath-*-{metres}-dry", laths.Value<string>("code"));
        Assert.Equal(Lath.Value<int>("maxstacksize"), laths.Value<int>("quantity"));
    }

    [Fact]
    public void AFullStackOfStonesHoldsDownTheLongestLaths()
    {
        Assert.Equal(VanillaStoneStack, Stones(5));
    }

    [Theory]
    [MemberData(nameof(Lengths))]
    public void ShorterLathsTakeStonesInProportionToTheirLength(int metres)
    {
        Assert.Equal((int)Math.Round(VanillaStoneStack * metres / 5.0), Stones(metres));
    }

    [Theory]
    [InlineData("roughseas:lath-{bundle}-wet", "roughseas:lathbundle-*-soaked")]
    [InlineData("roughseas:lath-{bundle}-dry", "roughseas:lathbundle-*-dry")]
    public void UntyingABundleGivesBackEveryLath(string laths, string bundle)
    {
        var recipe = Recipe(laths);

        Assert.Equal(bundle, recipe["ingredients"]!["B"]!.Value<string>("code"));
        Assert.Equal(Lath.Value<int>("maxstacksize"), recipe["output"]!.Value<int>("quantity"));
    }

    [Fact]
    public void ABundleSoaksThroughAsLongAsLathsSealedInABarrel()
    {
        Assert.All(Barrel, recipe => Assert.Equal(recipe.Value<double>("sealHours"), Entity["attributes"]!.Value<double>("soakHours")));
    }

    [Fact]
    public void ASoakedBundleDriesAsItsLathsDo()
    {
        var lath = Lath["transitionablePropsByType"]!["*-wet"]![0]!;
        var bundle = Bundle["transitionablePropsByType"]!["*-soaked"]![0]!;

        Assert.Equal(lath["freshHours"]!.ToString(), bundle["freshHours"]!.ToString());
        Assert.Equal("roughseas:lathbundle-{wood}-{length}-dry", bundle["transitionedStack"]!.Value<string>("code"));
    }

    [Theory]
    [MemberData(nameof(Lengths))]
    public void ABundleDrawsEachOfItsLathsAndStones(int metres)
    {
        var elements = Load("shapes", "entity", $"lathbundle-{metres}.json")["elements"]!.Select(element => element.Value<string>("name")!).ToList();

        Assert.Equal(Lath.Value<int>("maxstacksize"), elements.Count(name => name.StartsWith("Lath")));
        Assert.Equal(Stones(metres), elements.Count(name => name.StartsWith("Stone")));
    }

    private static int Stones(int metres) => Weighted(metres)["ingredients"]!["S"]!.Value<int>("quantity");

    private static JToken Weighted(int metres) => Recipe($"roughseas:lathbundle-{{wood}}-{metres}-dry");

    private static JToken Recipe(string output) => Recipes.Single(recipe => recipe["output"]!.Value<string>("code") == output);

    private static JToken Load(params string[] path) =>
        JToken.Parse(File.ReadAllText(Path.Combine([Repository.Mod, "assets", "roughseas", .. path])));
}
