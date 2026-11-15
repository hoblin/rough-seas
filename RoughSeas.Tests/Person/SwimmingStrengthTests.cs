using RoughSeas.Person;
using Vintagestory.API.Common.Entities;

namespace RoughSeas.Tests.Person;

public class SwimmingStrengthTests
{
    private static readonly Entity Swimmer = null!;

    [Fact]
    public void WithNothingAddedTheArmsPullAsTheyDo() => Assert.Equal(1, SwimmingStrength.Factor(Swimmer));

    [Fact]
    public void AnAddedFactorMultipliesThePullUntilRemoved()
    {
        System.Func<Entity, double> goodSwimmer = _ => 1.5;
        System.Func<Entity, double> poorSwimmer = _ => 0.5;

        SwimmingStrength.Add(goodSwimmer);
        SwimmingStrength.Add(poorSwimmer);
        var both = SwimmingStrength.Factor(Swimmer);
        SwimmingStrength.Remove(poorSwimmer);
        var one = SwimmingStrength.Factor(Swimmer);
        SwimmingStrength.Remove(goodSwimmer);

        Assert.Equal(0.75, both, 9);
        Assert.Equal(1.5, one, 9);
        Assert.Equal(1, SwimmingStrength.Factor(Swimmer));
    }
}
