using RoughSeas.Vessels;

namespace RoughSeas.Tests.Vessels;

public class CarriedConditionTests
{
    [Fact]
    public void CarriesIntegrityAsDurabilityAndBack()
    {
        int durability = CarriedCondition.Durability(0.62f, 100);

        Assert.Equal(62, durability);
        Assert.Equal(0.62f, CarriedCondition.Integrity(durability, 100), 3);
    }

    [Fact]
    public void KeepsABarelyFloatingHullAnItem()
    {
        Assert.Equal(1, CarriedCondition.Durability(0.001f, 100));
    }

    [Fact]
    public void ReadsAStackWithoutDurabilityAsSound()
    {
        Assert.Equal(1, CarriedCondition.Integrity(100, 100));
        Assert.Equal(1, CarriedCondition.Integrity(0, 0));
    }
}
