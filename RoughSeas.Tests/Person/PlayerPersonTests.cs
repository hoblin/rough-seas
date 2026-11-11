using RoughSeas.Tests.Storms;
using Vintagestory.API.Common;

namespace RoughSeas.Tests.Person;

public class PlayerPersonTests
{
    private static readonly double SteelKgPerLitre = Fixtures.PersonConfig.Carried.DensityKgPerLitre;

    [Fact]
    public void WeighsFullPlateAsFieldArmour()
    {
        var plate = Fixtures.Person.Weigh([0.14, 0.14, 0.14], []);

        Assert.InRange(plate.Kg, 30, 40);
        Assert.Equal(plate.Kg / SteelKgPerLitre, plate.Litres, 6);
    }

    [Fact]
    public void WeighsToolsAndWeaponsByTheirType()
    {
        var kit = Fixtures.Person.Weigh([], [EnumTool.Axe, EnumTool.Sword, null, null]);

        Assert.Equal(1.85 + 1.47, kit.Kg, 6);
    }

    [Fact]
    public void WeighsNoMaterials()
    {
        Assert.Equal(0, Fixtures.Person.Weigh([], [null, null, null]).Kg);
    }

    [Fact]
    public void HasAWeightForEveryToolTheGameKnows()
    {
        foreach (var tool in Enum.GetValues<EnumTool>())
        {
            Assert.True(Fixtures.PersonConfig.Carried.ToolKg.ContainsKey(tool.ToString().ToLowerInvariant()), tool.ToString());
        }
    }
}
