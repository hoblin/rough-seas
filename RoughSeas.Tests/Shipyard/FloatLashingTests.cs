using Newtonsoft.Json.Linq;
using RoughSeas.Shipyard;
using RoughSeas.Vessels;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Tests.Shipyard;

public class FloatLashingTests
{
    private static readonly JObject Shape = JObject.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "shapes", "outrigger.json")));
    private static readonly JToken Item = JToken.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "items", "outrigger.json")));

    [Theory]
    [InlineData(0, null)]
    [InlineData(1, "Loose")]
    [InlineData(2, "Loose")]
    [InlineData(3, "Torn")]
    [InlineData(4, "Shifted")]
    public void HerFloatIsDrawnAsLooseAsItsLashingsWorked(int stage, string? form)
    {
        Assert.Equal(form, EntityBehaviorFloatLashing.FormOf(stage));
    }

    [Fact]
    public void AKnockWorksTheLashingsLooseByTheShareOfWhatTheyHold()
    {
        Assert.Equal(0.25, EntityBehaviorFloatLashing.Loss(520, 2080), 6);
    }

    [Fact]
    public void AFloatNotYetLashedComesAwayAtAnyKnock()
    {
        Assert.Equal(1, EntityBehaviorFloatLashing.Loss(1, 0));
    }

    [Fact]
    public void TheLashingsWearAsTheRaftsDoAndAreBoundUpAgain()
    {
        var tree = new TreeAttribute();
        var lashing = new LashedHull(() => tree, () => { }, "roughseas:vessel-float-lashing");

        Assert.Equal(Knock.Harmed, lashing.Take(HullPlace.StarboardSide, 0.6));
        Assert.Equal(3, lashing.Stage(HullPlace.StarboardSide));
        lashing.Mend(new HullRepair { Restores = 0.11f }, HullPlace.StarboardSide);
        Assert.Equal(0.51, lashing.Condition, 2);
        Assert.Equal(Knock.BrokenUp, lashing.Take(HullPlace.StarboardSide, 1));
    }

    [Fact]
    public void DrawsEachFormOfItsFloatInsteadOfPartsItHas()
    {
        var names = Shape.SelectTokens("$..name").Select(name => name.Value<string>()).ToHashSet();
        var forms = Shape.SelectTokens("$.elements[?(@.name == 'FloatLashing')].children[*]").ToList();

        Assert.Equal(["FloatLashingLoose", "FloatLashingTorn", "FloatLashingShifted"], forms.Select(form => form.Value<string>("name")));
        Assert.All(forms, form => Assert.All(form["replaces"]!.Values<string>(), replaced => Assert.Contains(replaced, names)));
    }

    [Fact]
    public void LeavesTheFloatsFormsOffTheItem()
    {
        Assert.Contains("FloatLashing", Item["shape"]!["ignoreElements"]!.Values<string>());
    }
}
