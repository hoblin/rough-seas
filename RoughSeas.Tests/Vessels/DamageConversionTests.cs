using RoughSeas.Vessels;
using Vintagestory.API.Common;

namespace RoughSeas.Tests.Vessels;

public class DamageConversionTests
{
    private readonly DamageConversion conversion = new()
    {
        JoulesPerDamage = new() { [EnumDamageSource.Explosion] = 1000 },
        DefaultJoulesPerDamage = 200
    };

    [Fact]
    public void ConvertsDamageByItsSource()
    {
        Assert.Equal(4000, conversion.Joules(new DamageSource { Source = EnumDamageSource.Explosion }, 4));
    }

    [Fact]
    public void ConvertsAnUnlistedSourceAtTheDefaultRate()
    {
        Assert.Equal(600, conversion.Joules(new DamageSource { Source = EnumDamageSource.Player }, 3));
    }
}
