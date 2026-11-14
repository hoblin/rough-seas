using RoughSeas.Person;
using RoughSeas.Tests.Storms;
using Vintagestory.API.Common;

namespace RoughSeas.Tests.Person;

public class PlayerPersonTests
{
    private const double FreshWater = 998;
    private static readonly CarriedConfig Config = Fixtures.PersonConfig.Carried;

    private static Carried Tool(EnumTool tool, string? material) => Fixtures.Person.Weigh([], [new Held(tool, material)]);

    private static double InWater(Carried carried) => carried.Kg - FreshWater * carried.Litres / Body.LitresPerCubicMetre;

    [Fact]
    public void WeighsFullPlateAsFieldArmourOfItsMetal()
    {
        var plate = Fixtures.Person.Weigh([new Worn(0.14, "iron"), new Worn(0.14, "iron"), new Worn(0.14, "iron")], []);

        Assert.InRange(plate.Kg, 30, 40);
        Assert.Equal(plate.Kg / Config.Densities["iron"] * Body.LitresPerCubicMetre, plate.Litres, 6);
    }

    [Fact]
    public void TakesArmourOfAnUnknownMaterialAsSteel()
    {
        var odd = Fixtures.Person.Weigh([new Worn(0.03, "orichalcum")], []);

        Assert.Equal(odd.Kg / Config.Densities["steel"] * Body.LitresPerCubicMetre, odd.Litres, 6);
    }

    [Fact]
    public void WoodenArmourHoldsTheSwimmerUp()
    {
        Assert.True(InWater(Fixtures.Person.Weigh([new Worn(0.03, "wood")], [])) < 0);
        Assert.True(InWater(Fixtures.Person.Weigh([new Worn(0, "wood")], [])) < 0);
    }

    [Fact]
    public void BearHideArmourWeighsAlmostNothingInTheWater()
    {
        var hide = Fixtures.Person.Weigh([new Worn(0.03, "bear-polar")], []);

        Assert.InRange(InWater(hide), 0, 0.1 * hide.Kg);
    }

    [Fact]
    public void AnAxeWeighsLessInTheWaterThanItsHeadForItsFloatingHaft()
    {
        var axe = Tool(EnumTool.Axe, "steel");
        var head = Config.Tools["axe"].Head;

        Assert.Equal(head.Kg + Config.Tools["axe"].Haft!.Kg, axe.Kg, 6);
        Assert.True(InWater(axe) < head.Kg * (1 - FreshWater / Config.Densities["steel"]), $"the axe weighs {InWater(axe):F2} kg in the water");
    }

    [Fact]
    public void AHeadOfAnotherMetalKeepsItsSize()
    {
        var steel = Tool(EnumTool.Axe, "steel");
        var copper = Tool(EnumTool.Axe, "copper");

        Assert.Equal(steel.Litres, copper.Litres, 6);
        Assert.True(copper.Kg > steel.Kg, $"copper {copper.Kg:F2} kg, steel {steel.Kg:F2} kg");
    }

    [Fact]
    public void AFlintAxeHeadWeighsAsKnappedOnesDo()
    {
        var flint = Tool(EnumTool.Axe, "chert").Kg - Config.Tools["axe"].Haft!.Kg;

        Assert.InRange(flint, 0.243, 0.482);
    }

    [Fact]
    public void ABoneHandledToolHasAHandleOfBone()
    {
        var wooden = Tool(EnumTool.Axe, "chert");
        var bony = Tool(EnumTool.Axe, "bonechert");

        Assert.Equal(wooden.Litres, bony.Litres, 6);
        Assert.True(bony.Kg > wooden.Kg, $"bone {bony.Kg:F2} kg, wood {wooden.Kg:F2} kg");
    }

    [Fact]
    public void AMaterialOfUnknownMetalWeighsAsTheToolMeasured()
    {
        Assert.Equal(Tool(EnumTool.Axe, "steel"), Tool(EnumTool.Axe, "orichalcum"));
    }

    [Fact]
    public void WeighsNoMaterials()
    {
        Assert.Equal(default, Fixtures.Person.Weigh([], [new Held(null, "granite"), new Held(null, null)]));
    }

    [Fact]
    public void HasPartsForEveryToolTheGameKnows()
    {
        Assert.All(Enum.GetValues<EnumTool>(), tool => Assert.True(Config.Tools.ContainsKey(tool.ToString().ToLowerInvariant()), tool.ToString()));
    }

    [Fact]
    public void ConfigNamingAMaterialWithNoDensityFailsToLoad()
    {
        var broken = new CarriedConfig { Densities = Config.Densities, HandlePrefix = Config.HandlePrefix, UnknownArmourMaterial = "orichalcum" };

        Assert.Throws<ArgumentException>(() => new Materials(broken));
    }

    [Fact]
    public void ArmourThatSlowsNobodyWeighsNothingUnlessItIsWood()
    {
        Assert.Equal(default, Fixtures.Person.Weigh([new Worn(0, "leather")], []));
    }

    [Fact]
    public void AMaterialThatIsOnlyTheHandlesPrefixIsAHeadOfIt()
    {
        Assert.Equal((Config.HandlePrefix, (string?)null), new Materials(Config).Split(Config.HandlePrefix));
    }
}
