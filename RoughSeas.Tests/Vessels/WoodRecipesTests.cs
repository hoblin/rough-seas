using Newtonsoft.Json.Linq;
using RoughSeas.Vessels;

namespace RoughSeas.Tests.Vessels;

public class WoodRecipesTests
{
    private static readonly JArray Template = JArray.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "woodrecipes.json")));

    [Fact]
    public void BuildsVanillaVesselsUnderTheirBareSpecies()
    {
        var outrigger = Recipe(Expand(new Wood("game", "oak"), ["oak", "birch"], ["birch"]), "Outrigger canoe");

        Assert.Equal("roughseas:outrigger-{wood}", (string?)outrigger["output"]!["code"]);
        Assert.Equal("game:log-placed-*-ud", (string?)outrigger["ingredients"]!["L"]!["code"]);
        Assert.Equal(["oak", "birch"], outrigger["ingredients"]!["L"]!["allowedVariants"]!.Values<string>());
    }

    [Fact]
    public void BuildsAnotherModsVesselsFromItsOwnBlocksUnderItsDomain()
    {
        var outrigger = Recipe(Expand(new Wood("wildcrafttree", "elm"), ["elm", "ash"], ["elm"]), "Outrigger canoe");

        Assert.Equal("roughseas:outrigger-wildcrafttree-{wood}", (string?)outrigger["output"]!["code"]);
        Assert.Equal("wildcrafttree:plank-*", (string?)outrigger["ingredients"]!["P"]!["code"]);
    }

    [Fact]
    public void NamesTheWoodOfAnOutriggerBuiltFromSectionsByItsSectionsAlone()
    {
        var outrigger = Recipe(Expand(new Wood("game", "redwood"), ["oak", "redwood"], []), "Outrigger canoe from log sections");

        Assert.Equal(["L"], outrigger["ingredients"]!.Children<JProperty>().Where(ingredient => ingredient.Value["name"] != null).Select(ingredient => ingredient.Name));
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
        Assert.Contains(recipes, recipe => (string?)recipe["name"] == "Outrigger canoe");
    }

    private static List<JToken> Expand(Wood wood, string[] woods, string[] barks) =>
        WoodRecipes.Expand(Template, wood, new Dictionary<string, string[]> { ["{woods}"] = woods, ["{barks}"] = barks }).ToList();

    private static JToken Recipe(IEnumerable<JToken> recipes, string name) => recipes.Single(recipe => (string?)recipe["name"] == name);
}
