using RoughSeas.Vessels;
using Vintagestory.API.Common;

namespace RoughSeas.Tests.Vessels;

public class DamageConversionTests
{
    private readonly DamageConversion conversion = new()
    {
        JoulesPerDamage = new() { [EnumDamageSource.Explosion] = 1000 },
        DefaultJoulesPerDamage = 200,
        BroadSources = [EnumDamageSource.Explosion]
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

    [Fact]
    public void ABlastLoadsTheWholeHull()
    {
        Assert.Equal(Blow.Broad, conversion.BlowOf(new DamageSource { Source = EnumDamageSource.Explosion, Type = EnumDamageType.BluntAttack }));
    }

    [Fact]
    public void AHitLoadsOnlyThePlaceItLands()
    {
        Assert.Equal(Blow.Point, conversion.BlowOf(new DamageSource { Source = EnumDamageSource.Entity, Type = EnumDamageType.PiercingAttack }));
    }

    [Fact]
    public void AnEdgeCuts()
    {
        Assert.Equal(Blow.Cut, conversion.BlowOf(new DamageSource { Source = EnumDamageSource.Player, Type = EnumDamageType.SlashingAttack }));
    }
}
