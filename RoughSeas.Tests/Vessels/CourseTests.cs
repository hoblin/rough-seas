using RoughSeas.Vessels;

namespace RoughSeas.Tests.Vessels;

public class CourseTests
{
    private readonly Course course = new();

    [Fact]
    public void KnowsNoSpeedBeforeTwoFramesAreOver()
    {
        course.Record(0, 0, 0);
        course.Record(1, 0, 50);

        Assert.Equal((0.0, 0.0), course.Velocity);
    }

    [Fact]
    public void MovesAtTheSpeedHerPlaceChangedBy()
    {
        course.Record(0, 0, 0);
        course.Record(0.05, -0.1, 50);
        course.Record(0.06, -0.12, 60);

        Assert.Equal(1, course.Velocity.X, 9);
        Assert.Equal(-2, course.Velocity.Z, 9);
    }

    [Fact]
    public void TimesAFrameByWhereItsLastStepLeftHer()
    {
        course.Record(0, 0, 0);
        for (int step = 1; step <= 6; step++) course.Record(0.05 * step, 0, 100);
        course.Record(0.35, 0, 200);

        Assert.Equal(3, course.Velocity.X, 9);
    }

    [Fact]
    public void ReadsOnlyTheLastMoment()
    {
        course.Record(0, 0, 0);
        course.Record(10, 0, 1000);
        course.Record(10, 0, 1050);
        course.Record(10, 0, 1100);

        Assert.Equal(0, course.Velocity.X, 9);
    }

    [Fact]
    public void AHullHeldStillMovesAtNoSpeedHoweverHardSheIsDriven()
    {
        for (long ms = 0; ms <= 200; ms += 17) course.Record(3, 4, ms);

        Assert.Equal((0.0, 0.0), course.Velocity);
    }
}
