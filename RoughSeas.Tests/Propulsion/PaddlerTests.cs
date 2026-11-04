using RoughSeas.Propulsion;
using Vintagestory.API.MathTools;

namespace RoughSeas.Tests.Propulsion;

public class PaddlerTests
{
    private const double StrokeSeconds = 1.5;
    private const float Veer = 5 * GameMath.DEG2RAD;

    [Theory]
    [InlineData(1, 1, Side.Starboard)]
    [InlineData(0, 1, Side.Starboard)]
    [InlineData(-1, 1, Side.Port)]
    [InlineData(1, -1, Side.Port)]
    [InlineData(-1, -1, Side.Starboard)]
    public void ToTurnHerHeWorksTheSideThatTurnsHer(int ahead, int turn, Side side)
    {
        var paddler = new Paddler(5);

        paddler.Paddle(ahead, turn, 0, StrokeSeconds);

        Assert.Equal(side, paddler.Side);
    }

    [Fact]
    public void HoldingHerCourseHeKeepsHisSideUntilSheVeersOffIt()
    {
        var paddler = new Paddler(5);
        paddler.Paddle(1, 0, 0, StrokeSeconds);

        Assert.False(paddler.Paddle(1, 0, Veer * 0.9f, StrokeSeconds));
        Assert.True(paddler.Paddle(1, 0, Veer * 1.1f, StrokeSeconds));
        Assert.Equal(Side.Port, paddler.Side);
    }

    [Fact]
    public void ChangingSidesTakesHimAStroke()
    {
        var paddler = new Paddler(5);
        paddler.Paddle(1, -1, 0, StrokeSeconds);

        paddler.Lap(StrokeSeconds * 0.9);
        Assert.True(paddler.Changing);
        Assert.False(paddler.Paddle(1, 1, 0, StrokeSeconds));

        paddler.Lap(StrokeSeconds * 0.2);
        Assert.False(paddler.Changing);
        Assert.True(paddler.Paddle(1, 1, 0, StrokeSeconds));
    }

    [Fact]
    public void GoingAsternAfterAheadHeTakesANewCourse()
    {
        var paddler = new Paddler(5);
        paddler.Paddle(1, 0, 0, StrokeSeconds);

        Assert.False(paddler.Paddle(-1, 0, Veer * 1.1f, StrokeSeconds));
    }

    [Fact]
    public void BackingOffHerCourseHeChangesToTheSideThatBacksHerBack()
    {
        var paddler = new Paddler(5);
        paddler.Paddle(-1, 0, 0, StrokeSeconds);

        paddler.Paddle(-1, 0, -Veer * 1.1f, StrokeSeconds);

        Assert.Equal(Side.Port, paddler.Side);
    }
}
