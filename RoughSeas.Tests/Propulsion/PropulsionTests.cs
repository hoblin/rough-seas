using RoughSeas.Propulsion;
using RoughSeas.Tests.Storms;
using RoughSeas.Vessels;

namespace RoughSeas.Tests.Propulsion;

public class PropulsionTests
{
    private const double StepSeconds = 0.05;
    private const double PoleDensity = 600;

    [Fact]
    public void ABarkCanoeWithAPaddleCruisesAtACanoesPace()
    {
        Assert.InRange(Cruise("barkcanoe", Paddled("barkcanoe")), 1.8, 2.6);
    }

    [Fact]
    public void TheCoracleScullsSlowlyAndSpinsOnTheSpot()
    {
        Assert.True(Cruise("coracle", Paddled("coracle")) < Cruise("barkcanoe", Paddled("barkcanoe")) / 2);
        Assert.True(Spin("coracle", Paddled("coracle")) > 4 * Spin("barkcanoe", Paddled("barkcanoe")));
    }

    [Fact]
    public void TheCurrachRowsOnHerTholesFasterThanSheWouldPaddle()
    {
        var oars = Fixtures.PropulsionOf("currach")["oars"]!;
        var rowed = new TholeOars(Blade("rowing", (double)oars["bladeSquareMetres"]!), (double)oars["spanMetres"]!);
        var paddled = new HeldTool(Blade("paddling", Fixtures.PaddleSquareMetres), Sweep("currach"));

        Assert.True(Cruise("currach", rowed) > 1.2 * Cruise("currach", paddled));
    }

    [Fact]
    public void ALogRaftIsABargeThatBarelyMakesWay()
    {
        Assert.InRange(Cruise("boat-raft", Paddled("boat-raft")), 0.2, 1);
    }

    [Fact]
    public void TheOutriggersFloatSlowsHerTurning()
    {
        Assert.True(Spin("outrigger", Paddled("outrigger")) < Spin("dugout", Paddled("dugout")));
    }

    [Theory]
    [InlineData(3, 2)]
    [InlineData(5, 4)]
    public void APoleWorksDownToWaterAMetreShallowerThanItIsLong(double metres, double depth)
    {
        Assert.Equal(depth, new Pole(metres, PoleDensity, Fixtures.Propulsion.Pole).Reach(0.7), 0.2);
    }

    [Fact]
    public void ALongerPoleWorkingDeeperWaterSpendsMoreOfTheShoveLiftingItself()
    {
        var shallow = new PoleStroke("pole", Fixtures.Propulsion.Strokes["pole"], 3 * 0.09 * 0.09 * PoleDensity, 1, 0.6);
        var deep = new PoleStroke("pole", Fixtures.Propulsion.Strokes["pole"], 5 * 0.09 * 0.09 * PoleDensity, 3.5, 0.6);

        Assert.True(deep.Thrust(1, 180) < shallow.Thrust(1, 180));
    }

    [Fact]
    public void TwoOarsPulledTogetherDriveHerStraight()
    {
        var push = Oars().Push(1, 0, 1, 0, 180);

        Assert.True(push.Newtons > 0);
        Assert.Equal(0, push.NewtonMetres, 6);
    }

    [Fact]
    public void OneOarPulledAndTheOtherBackedTurnHerWhereSheLies()
    {
        var push = Oars().Push(0, 1, 0, 0, 180);

        Assert.Equal(0, push.Newtons, 6);
        Assert.True(push.NewtonMetres > 0);
    }

    [Fact]
    public void APoleFindsNoBottomInWaterDeeperThanItReaches()
    {
        var pole = new Pole(3, PoleDensity, Fixtures.Propulsion.Pole);

        Assert.NotNull(Fixtures.Station("dugout", 1).Propulsor(pole, out _));
        Assert.Null(Fixtures.Station("dugout", 3).Propulsor(pole, out var refusal));
        Assert.Contains("deeper", refusal);
    }

    [Fact]
    public void APaddleInTheCurrachLeavesHerRowingHerOwnOars()
    {
        Assert.IsType<TholeOars>(Fixtures.Station("currach", 5).Propulsor(new Paddle(Fixtures.PaddleSquareMetres), out _));
        Assert.IsType<TholeOars>(Fixtures.Station("currach", 5).Propulsor(null, out _));
    }

    [Fact]
    public void AnEmptyHandMovesNoBoatWithoutOarsOfHerOwn()
    {
        Assert.Null(Fixtures.Station("dugout", 1).Propulsor(null, out var refusal));
        Assert.NotNull(refusal);
    }

    [Fact]
    public void OneOarPulledWhileSheGoesAheadTurnsHerAsSheGoes()
    {
        var push = Oars().Push(1, 1, 1, 0, 180);

        Assert.True(push.Newtons > 0);
        Assert.True(push.NewtonMetres > 0);
    }

    [Fact]
    public void AHeldToolSweepingAsItDrivesSplitsTheRowersEffort()
    {
        var paddle = new HeldTool(Blade("paddling", Fixtures.PaddleSquareMetres), 1);

        Assert.True(paddle.Push(1, 1, 1, 0, 104).Newtons < paddle.Push(1, 0, 1, 0, 104).Newtons);
    }

    [Fact]
    public void AFasterHullTakesLessFromTheSameBlade()
    {
        var blade = Blade("paddling", Fixtures.PaddleSquareMetres);

        Assert.True(blade.Thrust(2, 104) < blade.Thrust(0.5, 104));
    }

    [Fact]
    public void TheDragLawMatchesARacingKayakTowedThroughTheWater()
    {
        var k1 = new HullDrag(new HullForm(5.2, 0.45, 0, 0.75, 0), 0.09, 87, Fixtures.WaterDensity, Fixtures.Drag);

        Assert.InRange(-k1.Along(2.78), 21.37 * 0.8, 21.37 * 1.35);
        Assert.InRange(-k1.Along(5.56), 89.32 * 0.8, 89.32 * 1.35);
    }

    [Theory]
    [InlineData(0.25, 0.7e-3, 1.4e-3)]
    [InlineData(0.5, 3e-3, 5.5e-3)]
    public void TheWavesAWigleyHullMakesMatchMichellsTheory(double froude, double least, double most)
    {
        const double wettedSquareMetres = 0.1487;
        var speed = froude * Math.Sqrt(9.81);
        var newtons = new WaveMaking(1, 0.1, 0.0625, 1000).Newtons(speed);

        Assert.InRange(newtons / (500 * speed * speed * wettedSquareMetres), least, most);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(2)]
    [InlineData(4)]
    public void VanillaCarriesABoatAtTheSpeedItIsCommanded(double metresPerSecond)
    {
        Assert.Equal(metresPerSecond, VanillaSwim.Realized(VanillaSwim.Commanded(metresPerSecond)), 3);
    }

    private static TholeOars Oars() => new(Blade("rowing", 0.1), 4.2);

    private static HeldTool Paddled(string vessel) =>
        new(Blade((string)Fixtures.PropulsionOf(vessel)["paddle"]!, Fixtures.PaddleSquareMetres), Sweep(vessel));

    private static double Sweep(string vessel) => (double)Fixtures.PropulsionOf(vessel)["sweepMetres"]!;

    private static BladeStroke Blade(string stroke, double squareMetres) =>
        new(stroke, Fixtures.Propulsion.Strokes[stroke], squareMetres, Fixtures.WaterDensity);

    private static double Watts(Propulsor propulsor) => Fixtures.Propulsion.Rower.MetabolicWatts * propulsor.Stroke.GrossEfficiency;

    private static HullDrag Drag(string vessel) => Fixtures.DragOf(vessel, Fixtures.Hull(vessel).Crewed());

    private static double Cruise(string vessel, Propulsor propulsor)
    {
        var drag = Drag(vessel);
        var speed = 0.0;
        for (var t = 0.0; t < 300; t += StepSeconds)
        {
            speed += (propulsor.Push(1, 0, speed, 0, Watts(propulsor)).Newtons + drag.Along(speed)) / drag.AlongKg * StepSeconds;
        }
        return speed;
    }

    private static double Spin(string vessel, Propulsor propulsor)
    {
        var drag = Drag(vessel);
        var turn = 0.0;
        for (var t = 0.0; t < 300; t += StepSeconds)
        {
            turn += (propulsor.Push(0, 1, 0, turn, Watts(propulsor)).NewtonMetres + drag.Turning(turn)) / drag.TurningKgSquareMetres * StepSeconds;
        }
        return turn;
    }
}
