using Newtonsoft.Json.Linq;
using RoughSeas.Vessels;

namespace RoughSeas.Tests.Vessels;

public class WoodRecipesTests
{
    private static readonly JArray Template = JArray.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "woodrecipes.json")));

    [Fact]
    public void PeelsVanillaBarkUnderItsBareSpecies()
    {
        var bark = Recipe(Expand(new Wood("game", "birch"), ["oak", "birch"], ["birch"]), "Bark");

        Assert.Equal("roughseas:bark-{wood}", (string?)bark["output"]!["code"]);
        Assert.Equal("game:log-placed-*-ud", (string?)bark["ingredients"]!["L"]!["code"]);
    }

    [Fact]
    public void PeelsBarkOnlyOffTheTreesOnTheCanoeList()
    {
        var bark = Recipe(Expand(new Wood("wildcrafttree", "elm"), ["elm", "ash"], ["elm"]), "Bark");

        Assert.Equal(["elm"], bark["ingredients"]!["L"]!["allowedVariants"]!.Values<string>());
        Assert.Equal("roughseas:bark-wildcrafttree-{wood}", (string?)bark["output"]!["code"]);
        Assert.Equal("wildcrafttree:debarkedlog-{wood}-ud", (string?)bark["ingredients"]!["L"]!["returnedStack"]!["code"]);
    }

    [Fact]
    public void PeelsNoBarkFromAModWithoutCanoeBark()
    {
        var recipes = Expand(new Wood("othermod", "teak"), ["teak"], []);

        Assert.DoesNotContain(recipes, recipe => (string?)recipe["name"] == "Bark");
    }

    private static List<JToken> Expand(Wood wood, string[] woods, string[] barks) =>
        WoodRecipes.Expand(Template, wood, new Dictionary<string, string[]> { ["{woods}"] = woods, ["{barks}"] = barks }).ToList();

    private static JToken Recipe(IEnumerable<JToken> recipes, string name) => recipes.Single(recipe => (string?)recipe["name"] == name);
}
