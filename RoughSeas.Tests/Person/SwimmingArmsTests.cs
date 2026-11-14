using RoughSeas.Person;
using RoughSeas.Storms;
using RoughSeas.Tests.Storms;

namespace RoughSeas.Tests.Person;

public class SwimmingArmsTests
{
    private static readonly Waves Waves = new(Fixtures.Config().Waves);
    private static readonly double Pool = Waves.DensityOf(false);
    private static readonly SwimmingConfig Measured = Fixtures.PersonConfig.Body.Swimming;
    private static readonly SwimmingArms Arms = new(Measured, Pool);

    [Fact]
    public void PullsInThePoolAtItsMeasuredSpeedWhatItsUsefulPowerHoldsThere() =>
        Assert.Equal(Measured.UsefulWatts / Measured.MetresPerSecond, Arms.Thrust(Measured.MetresPerSecond, Pool), 6);

    [Fact]
    public void PullsHarderInDenserWater() =>
        Assert.True(Arms.Thrust(0.5, Waves.DensityOf(true)) > Arms.Thrust(0.5, Pool));
}
