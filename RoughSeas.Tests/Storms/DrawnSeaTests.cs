using RoughSeas.SeaAndWeather;
using RoughSeas.Storms;

namespace RoughSeas.Tests.Storms;

public class DrawnSeaTests
{
    private const int RegionSize = 512;
    private const int CellBlocks = 32;
    private const double X = 512031.25;
    private const double Z = 512300.5;
    private const double Shore = 512000 + 6 * CellBlocks;
    private const double Height = 4;
    private const double Frame = 1 / 60.0;
    private const double Length = 6;
    private const double Width = 2;

    private readonly SeaSurface surface = new(Fixtures.Config().Surface, new Waves(Fixtures.Config().Waves));
    private readonly SurfaceField field = new();

    private DrawnSea Sea()
    {
        var raised = new WaveGeneration(0.4, 3.5 * Math.Sqrt(Height));
        field.Update(new Coast(), X, Z, RegionSize, CellBlocks);
        for (var frame = 0; frame * Frame < 120; frame++)
        {
            if (frame % 30 == 0)
            {
                surface.Aim(Height, 0.4, 7);
                surface.Raise(raised, raised, 0);
            }
            surface.Advance(frame * Frame);
        }
        return new DrawnSea(surface, field);
    }

    [Fact]
    public void LiesFlatBeforeAnySwellIsDrawn()
    {
        var sea = new DrawnSea(surface, field);

        Assert.False(sea.Swell);
        Assert.Equal(0, sea.HeightAt(X, Z));
        Assert.Equal(default, sea.PoseOf(X, Z, 0, Length, Width));
    }

    [Fact]
    public void ReadsTheHeightTheSurfaceDrawsAtAPoint()
    {
        var sea = Sea();

        Assert.True(sea.Swell);
        Assert.NotEqual(0, sea.HeightAt(X, Z));
        Assert.Equal(surface.HeightAt(X, Z, field.Factor(X, Z)), sea.HeightAt(X, Z));
    }

    [Fact]
    public void LeavesAVesselBeyondTheDrawnSwellToItsOwnRocking()
    {
        var sea = Sea();

        Assert.Equal(0, sea.HeightAt(Shore + CellBlocks, Z));
        Assert.Equal(default, sea.PoseOf(Shore + CellBlocks, Z, 0, Length, Width));
    }

    [Fact]
    public void TiltsASquareHullTheOtherWayRoundOnAQuarterTurn()
    {
        var sea = Sea();
        var ahead = sea.PoseOf(X, Z, 0.3, Width, Width);
        var turned = sea.PoseOf(X, Z, 0.3 + Math.PI / 2, Width, Width);

        Assert.NotEqual(0, ahead.Pitch);
        Assert.Equal(ahead.Lift, turned.Lift, 9);
        Assert.Equal(ahead.Roll, turned.Pitch, 9);
        Assert.Equal(-ahead.Pitch, turned.Roll, 9);
    }

    [Fact]
    public void ReversesRollAndPitchWhenTheHullTurnsAbout()
    {
        var sea = Sea();
        var ahead = sea.PoseOf(X, Z, 0.3, Length, Width);
        var about = sea.PoseOf(X, Z, 0.3 + Math.PI, Length, Width);

        Assert.Equal(ahead.Lift, about.Lift, 9);
        Assert.Equal(-ahead.Roll, about.Roll, 9);
        Assert.Equal(-ahead.Pitch, about.Pitch, 9);
    }

    [Fact]
    public void HoldsAHullFullyWhereTheSeaRunsHighest()
    {
        Assert.Equal(1, Sea().PoseOf(X, Z, 0, Length, Width).Hold);
    }

    [Fact]
    public void TellsTheTimeTheWavesRunOn()
    {
        var sea = Sea();

        Assert.Equal(surface.Seconds, sea.Seconds);
    }

    private sealed class Coast : ISeaSource
    {
        public SeaSample Sample(double x, double z) =>
            x < Shore ? new SeaSample(12, 0, 0, 0, Height, 7, 0, 0, 10) : new SeaSample(12, 0, 0, 0, 0, 0, 0, 0, 10);

        public (double X, double Z) Wind(double x, double z) => (12, 0);

        public double StormPresence(double x, double z) => 0;

        public SeaState At(double x, double z) => default;

        public SeaCurrent CurrentAt(double x, double z) => default;

        public float? RainCloudness(double x, double z, double totalDays) => 0;

        public double RegionMps(int regionX, int regionZ) => 12;

        public WaveGeneration Generation(double x, double z, long slot) => default;
    }
}
