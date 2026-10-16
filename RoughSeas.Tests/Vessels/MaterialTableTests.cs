using RoughSeas.Vessels;
using Vintagestory.API.Common;

namespace RoughSeas.Tests.Vessels;

public class MaterialTableTests
{
    private readonly MaterialTable table = new(new Dictionary<string, Material>
    {
        ["wood-oak"] = new() { Strength = 100 },
        ["wood-ebony"] = new() { Strength = 160 },
        ["rope-flax"] = new() { Strength = 11 },
        ["rope-bast"] = new() { Strength = 5.5 }
    }, new SilentLogger());

    [Fact]
    public void ScalesTheCapacityByEveryPartAgainstItsReference()
    {
        var parts = new[] { Part("wood-ebony", "wood-oak"), Part("rope-bast", "rope-flax") };

        Assert.Equal(0.8, table.StrengthFactor(parts), 6);
    }

    [Fact]
    public void CountsAnUnknownMaterialAsItsReference()
    {
        Assert.Equal(1, table.StrengthFactor([Part("wood-modded", "wood-oak")]));
    }

    private static VesselPart Part(string material, string reference) => new() { Material = material, Reference = reference };

    private class SilentLogger : LoggerBase
    {
        protected override void LogImpl(EnumLogType logType, string format, params object[] args) { }
    }
}
