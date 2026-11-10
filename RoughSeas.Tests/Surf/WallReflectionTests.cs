using RoughSeas.Surf;
using RoughSeas.Tests.Shore;

namespace RoughSeas.Tests.Surf;

public class WallReflectionTests
{
    private static readonly WallReflection Reflection = new(ShoreFixtures.Config.Faces, ShoreFixtures.Config.RunUp.SpectralPeriodRatio);

    [Fact]
    public void ASlopeBetweenABeachAndAWallSendsBackWhatZanuttighMeasured() => Assert.Equal(0.69, Reflection.Coefficient(2, 8, 0.5, 1), 2);

    [Fact]
    public void AVerticalWallSendsTheWholeWaveBack() => Assert.Equal(1, Reflection.Coefficient(4, 10, 20, 1), 3);

    [Fact]
    public void AGentleBeachSendsAlmostNothingBack() => Assert.True(Reflection.Coefficient(1, 8, 0.05, 1) < 0.1);

    [Fact]
    public void ARoughSlopeSendsLessBackThanASmoothOne() =>
        Assert.True(Reflection.Coefficient(2, 8, 0.5, 0.55) < Reflection.Coefficient(2, 8, 0.5, 1));

    [Fact]
    public void AWallLongerThanTheWaveReflectsAsAPlaneNearby() => Assert.Equal(1, WallReflection.Spreading(200, 50, 40));

    [Fact]
    public void AShortWallsReflectionThinsWithDistance() =>
        Assert.True(WallReflection.Spreading(5, 50, 90) < WallReflection.Spreading(5, 50, 10));

    [Fact]
    public void TheReflectionTapersOutAtItsReach()
    {
        var reach = ShoreFixtures.Config.Faces.ReachBlocks;
        Assert.Equal(1, Reflection.Taper(0));
        Assert.Equal(0, Reflection.Taper(reach));
    }
}
