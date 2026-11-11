using Newtonsoft.Json.Linq;
using RoughSeas.Propulsion;
using RoughSeas.Tests.Storms;
using RoughSeas.Vessels;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RoughSeas.Tests.Propulsion;

public class PropulsionTests
{
    private const double StepSeconds = 0.05;
    private const double PoleDensity = 600;
    private const int ChangeStrokes = 1;
    private const double SeatedHands = 0.49;

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
    public void ARaftsBluntLogEndsTakeMostOfHerPaddlersPush()
    {
        var drag = Fixtures.DragOf("boat-raft", Fixtures.Hull("boat-raft").Crewed());
        var speed = Cruise("boat-raft", Paddled("boat-raft"));
        var bow = Fixtures.WaterDensity / 2 * (double)Fixtures.PropulsionOf("boat-raft")["bowDrag"]! *
                  2 * Fixtures.Shape("boat-raft").HalfBeamMetres * Fixtures.Hull("boat-raft").Float(Fixtures.Hull("boat-raft").Crewed()).DraftMetres * speed * speed;

        Assert.True(bow > 0.9 * -drag.Along(speed));
    }

    [Fact]
    public void TheRowerRowsAsHisKeysAsk()
    {
        Assert.Equal((1, 0), EntityBehaviorPropulsion.Asked(new EntityControls { Forward = true }));
        Assert.Equal((-1, 1), EntityBehaviorPropulsion.Asked(new EntityControls { Backward = true, Left = true }));
        Assert.Equal((0, -1), EntityBehaviorPropulsion.Asked(new EntityControls { Right = true }));
        Assert.Equal((0, 0), EntityBehaviorPropulsion.Asked(null));
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

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public void APoleGivesItsFullShoveDownToItsFullStrokeDepthAndLessBelowIt(double metres)
    {
        var pole = new Pole(metres, PoleDensity, Fixtures.Propulsion.Pole);
        var push = Fixtures.Propulsion.Strokes["seatedpole"].PushMetres;
        var depth = pole.FullStrokeDepth(SeatedHands, push);

        Assert.Equal(push, pole.Travel(depth + SeatedHands), 6);
        Assert.True(pole.Travel(depth + SeatedHands + 0.1) < push);
        Assert.True(depth < pole.Reach(SeatedHands));
    }

    [Fact]
    public void ALongerPoleWorkingDeeperWaterSpendsMoreOfTheShoveLiftingItself()
    {
        var shallow = new HeldTool(Poled("pole", 3, 1), 1);
        var deep = new HeldTool(Poled("pole", 5, 3.5), 1);

        Assert.True(deep.Push(1, 0, Side.Starboard, 1, 0, SeatedWatts).Newtons < shallow.Push(1, 0, Side.Starboard, 1, 0, SeatedWatts).Newtons);
    }

    [Fact]
    public void APoleDrivesHerOnlyWhileTheDrawnShoveIsOnTheBedAndGivesTheSameWayOverTheStroke()
    {
        var pole = Poled("seatedpole", 3, 1);
        var shares = Enumerable.Range(0, 300).Select(i => (double?)(i / 300.0)).ToList();

        Assert.Equal(0, pole.Shove(100, 0.5, 0));
        Assert.Equal(100, shares.Average(share => pole.Shove(100, 0.5, share)), 0);
        Assert.Equal(0.5, shares.Count(share => pole.Shove(100, 0.5, share) > 0) / 300.0, 2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.3)]
    [InlineData(1.5)]
    public void NoShovePushesHarderThanAManCanHold(double metresPerSecond)
    {
        var pole = Poled("seatedpole", 3, 1);
        var mean = pole.Thrust(metresPerSecond, SeatedWatts);

        Assert.InRange(Enumerable.Range(0, 30).Max(i => pole.Shove(mean, metresPerSecond, i / 30.0)), 0, Fixtures.Propulsion.Strokes["seatedpole"].PushNewtons + 1e-9);
    }

    [Fact]
    public void APoleWhoseTopReachesTheHandsLeavesNoShove()
    {
        var reach = new Pole(3, PoleDensity, Fixtures.Propulsion.Pole).Reach(SeatedHands);

        Assert.True(Poled("seatedpole", 3, reach - 0.3).Thrust(1, SeatedWatts) < Poled("seatedpole", 3, 1).Thrust(1, SeatedWatts));
        Assert.Equal(0, Poled("seatedpole", 3, reach).Thrust(1, SeatedWatts), 6);
    }

    [Fact]
    public void AFasterHullQuickensThePolersBeatAndHisStrokeIsDrawnAsQuick()
    {
        var pole = Poled("seatedpole", 3, 1);

        const double inTheShove = 0.5;
        const double inTheRecovery = 0;

        Assert.Equal(1, pole.Pace(0.5, inTheShove), 6);
        Assert.True(pole.Pace(2, inTheShove) > 1.2);
        Assert.Equal(1, pole.Pace(2, inTheRecovery), 6);
        Assert.True(pole.LiftWatts(2) > pole.LiftWatts(0.5));
    }

    [Fact]
    public void BackingHerAgainstHerWayOrAtTheLimitOfItsReachThePoleStaysWithinAMansPush()
    {
        const double inTheShove = 0.5;
        var poled = new HeldTool(Poled("seatedpole", 3, 1), 1);
        var atReach = Poled("seatedpole", 3, new Pole(3, PoleDensity, Fixtures.Propulsion.Pole).Reach(SeatedHands));

        Assert.InRange(Math.Abs(poled.Push(-1, 0, Side.Starboard, 2, 0, SeatedWatts, inTheShove).Newtons), 0, Fixtures.Propulsion.Strokes["seatedpole"].PushNewtons + 1e-9);
        Assert.Equal(0, atReach.Shove(atReach.Thrust(1, SeatedWatts), 1, inTheShove));
        Assert.True(double.IsFinite(atReach.Pace(1, inTheShove)));
    }

    [Fact]
    public void TurningWhereSheLiesTheStrokeKeepsTheBeatOfTheSweep()
    {
        var poled = new HeldTool(Poled("seatedpole", 3, 1), 1);

        Assert.Equal(2, poled.StrokeSpeed(0, 1, 0, 2), 6);
        Assert.Equal(0, poled.StrokeSpeed(-1, 0, 2, 0), 6);
    }

    [Fact]
    public void TheWaterBearsPartOfAPoleBroughtUpFromTheBed()
    {
        const double gravity = 9.81;
        const double depthMetres = 2;
        var pole = new Pole(5, PoleDensity, Fixtures.Propulsion.Pole);
        var inAir = pole.Kilograms * gravity * depthMetres;

        Assert.InRange(pole.LiftJoules(depthMetres, Fixtures.WaterDensity), 0.5 * inAir, 0.8 * inAir);
    }

    [Fact]
    public void APolerDrivingAndTurningAtOnceLiftsHisPoleOnce()
    {
        const double ironwood = 1100;
        var heavy = new PoleStroke("seatedpole", Fixtures.Propulsion.Strokes["seatedpole"], new Pole(5, ironwood, Fixtures.Propulsion.Pole), 3, SeatedHands, Fixtures.WaterDensity);
        var push = new HeldTool(heavy, 1).Push(1, 1, Side.Starboard, 0.5, 0, SeatedWatts);

        Assert.InRange(heavy.LiftWatts(0.5), SeatedWatts / 2, SeatedWatts);
        Assert.True(push.Newtons > 0);
        Assert.True(push.NewtonMetres > 0);
    }

    [Fact]
    public void TwoOarsPulledTogetherDriveHerStraight()
    {
        var push = Oars().Push(1, 0, Side.Starboard, 1, 0, 180);

        Assert.True(push.Newtons > 0);
        Assert.Equal(0, push.NewtonMetres, 6);
    }

    [Fact]
    public void OneOarPulledAndTheOtherBackedTurnHerWhereSheLies()
    {
        var push = Oars().Push(0, 1, Side.Starboard, 0, 0, 180);

        Assert.Equal(0, push.Newtons, 6);
        Assert.True(push.NewtonMetres > 0);
    }

    [Fact]
    public void APoleFindsNoBottomInWaterDeeperThanItReaches()
    {
        var pole = new Pole(3, PoleDensity, Fixtures.Propulsion.Pole);

        Assert.NotNull(Fixtures.Station("dugout", 1).Propulsor(pole, 1, out _));
        Assert.Null(Fixtures.Station("dugout", 3).Propulsor(pole, 1, out var refusal));
        Assert.Contains("deeper", refusal);
    }

    [Fact]
    public void WithThePolesDepthOffAPoleShovesInAnyDepthAsAtItsFullStroke()
    {
        var pole = new Pole(3, PoleDensity, Fixtures.Propulsion.Pole);
        var poling = Fixtures.PropulsionOf("dugout")["pole"]!;
        var fullStroke = pole.FullStrokeDepth((double)poling["handsAboveWaterMetres"]!, Fixtures.Propulsion.Strokes[(string)poling["stroke"]!].PushMetres);
        var deep = Fixtures.Station("dugout", 40, deepWaterRefuses: false).Propulsor(pole, 1, out var refusal);
        var atFullStroke = Fixtures.Station("dugout", fullStroke).Propulsor(pole, 1, out _);

        Assert.Null(refusal);
        Assert.Equal(atFullStroke!.Push(1, 0, Side.Starboard, 1, 0, SeatedWatts).Newtons, deep!.Push(1, 0, Side.Starboard, 1, 0, SeatedWatts).Newtons, 6);
    }

    [Fact]
    public void APoleFindsTheBedWhereItsFootIsPlantedAsternOfHimGoingAheadAndAheadOfHimGoingAstern()
    {
        var pole = new Pole(3, PoleDensity, Fixtures.Propulsion.Pole);
        var shelvingAstern = Fixtures.Station("reedboat", along => along > 0.3 ? 1 : 5);

        Assert.NotNull(shelvingAstern.Propulsor(pole, 1, out _));
        Assert.Null(shelvingAstern.Propulsor(pole, -1, out _));
    }

    [Fact]
    public void APoleOverAShelfEdgeTakesTheDeeperOfTheBedsItsFootCouldLandOn()
    {
        var pole = new Pole(3, PoleDensity, Fixtures.Propulsion.Pole);
        var shelfAstern = Fixtures.Station("reedboat", along => along > 1 ? 1 : 3);

        Assert.Null(shelfAstern.Propulsor(pole, 1, out _));
    }

    [Fact]
    public void ThePoleIsPlantedAheadWhenHeBacksHerOrTurnsHerToStarboardFromRest()
    {
        Assert.Equal(1, EntityBehaviorPropulsion.Way(1, -1));
        Assert.Equal(1, EntityBehaviorPropulsion.Way(0, 1));
        Assert.Equal(-1, EntityBehaviorPropulsion.Way(-1, 1));
        Assert.Equal(-1, EntityBehaviorPropulsion.Way(0, -1));
    }

    [Theory]
    [InlineData("dugout")]
    [InlineData("outrigger")]
    [InlineData("barkcanoe")]
    [InlineData("coracle")]
    [InlineData("currach")]
    [InlineData("boat-raft")]
    public void APolerStandsWhereHerHullBearsAManStanding(string vessel)
    {
        var station = Fixtures.Station(vessel, 1);

        Assert.Equal("roughseas-stand", station.Stance(Pole.Code)?.Animation);
        Assert.Equal(Pole.Code, station.Propulsor(new Pole(3, PoleDensity, Fixtures.Propulsion.Pole), 1, out _)!.Stroke.Code);
    }

    [Fact]
    public void TheDugoutsPolerStandsAtHerBowEndOnHerFloor()
    {
        var offset = Fixtures.Station("dugout", 1).Stance(Pole.Code)!.Value.Offset!;

        Assert.Equal(-0.875, offset.X, 3);
        Assert.Equal(-0.132, offset.Y, 3);
        Assert.Equal(0, offset.Z, 3);
    }

    [Fact]
    public void OnTheReedBoatTooTenderToStandOnHePolesFromTheSeat()
    {
        var station = Fixtures.Station("reedboat", 1);

        Assert.Null(station.Stance(Pole.Code));
        Assert.Equal("seatedpole", station.Propulsor(new Pole(3, PoleDensity, Fixtures.Propulsion.Pole), 1, out _)!.Stroke.Code);
    }

    [Fact]
    public void APaddleKeepsHimSeated()
    {
        Assert.Null(Fixtures.Station("barkcanoe", 1).Stance(Paddle.Code));
    }

    [Theory]
    [InlineData("dugout")]
    [InlineData("barkcanoe")]
    public void StandingHeReachesTheBedOfTheShallowestSeaWithTheShortestPole(string vessel)
    {
        const double oneBlockOfStillWater = 7 / 8.0;

        Assert.NotNull(Fixtures.Station(vessel, oneBlockOfStillWater).Propulsor(new Pole(2, PoleDensity, Fixtures.Propulsion.Pole), 1, out _));
    }

    [Fact]
    public void StandingHeShovesHarderThanFromTheSeat()
    {
        var metabolic = Fixtures.Propulsion.Rower.MetabolicWatts;
        var standing = new HeldTool(Poled("pole", 3, 1), 1);
        var seated = new HeldTool(Poled("seatedpole", 3, 1), 1);

        Assert.True(standing.Push(1, 0, Side.Starboard, 1.5, 0, metabolic * standing.Stroke.GrossEfficiency).Newtons >
                    seated.Push(1, 0, Side.Starboard, 1.5, 0, metabolic * seated.Stroke.GrossEfficiency).Newtons);
    }

    [Fact]
    public void APaddleInTheCurrachLeavesHerRowingHerOwnOars()
    {
        Assert.IsType<TholeOars>(Fixtures.Station("currach", 5).Propulsor(new Paddle(Fixtures.PaddleSquareMetres), 1, out _));
        Assert.IsType<TholeOars>(Fixtures.Station("currach", 5).Propulsor(null, 1, out _));
    }

    [Fact]
    public void AnEmptyHandMovesNoBoatWithoutOarsOfHerOwn()
    {
        Assert.Null(Fixtures.Station("dugout", 1).Propulsor(null, 1, out var refusal));
        Assert.NotNull(refusal);
    }

    [Fact]
    public void OneOarPulledWhileSheGoesAheadTurnsHerAsSheGoes()
    {
        var push = Oars().Push(1, 1, Side.Starboard, 1, 0, 180);

        Assert.True(push.Newtons > 0);
        Assert.True(push.NewtonMetres > 0);
    }

    [Fact]
    public void AHeldToolSweepingAsItDrivesSplitsTheRowersEffort()
    {
        var paddle = new HeldTool(Blade("paddling", Fixtures.PaddleSquareMetres), 1);

        Assert.True(paddle.Push(1, 1, Side.Starboard, 1, 0, 104).Newtons < paddle.Push(1, 0, Side.Starboard, 1, 0, 104).Newtons);
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

    [Fact]
    public void OurDragTakesOverFromVanillasAlongTheWaterAndLeavesItOnHerHeave()
    {
        const double waterDrag = 0.92;
        const float seconds = 1 / 60f;
        var motion = new Vec3d(0.03, 0.002, -0.01);

        VanillaSwim.LeaveWaterDragOnHeave(motion, waterDrag, seconds);
        motion.Scale(Math.Pow(waterDrag, seconds * 33));

        Assert.Equal(0.03, motion.X, 9);
        Assert.Equal(-0.01, motion.Z, 9);
        Assert.Equal(0.002 * Math.Pow(waterDrag, seconds * 33), motion.Y, 9);
    }

    [Theory]
    [InlineData("barkcanoe", 3, 5)]
    [InlineData("outrigger", 8, 20)]
    public void HoldingHerCourseHeChangesSidesAsOftenAsRealPaddlersDo(string vessel, double least, double most)
    {
        Assert.InRange(StrokesASide(vessel), least, most);
    }

    [Fact]
    public void APaddlerFinishesTheStrokeHeIsInBeforeChangingSides()
    {
        const double strokeSeconds = 1.5;
        var paddler = new Paddler(10);
        paddler.Paddle(1, 0, 0, strokeSeconds);
        paddler.Lap(1.4 * strokeSeconds);

        Assert.True(paddler.Paddle(1, -1, 0, strokeSeconds));
        Assert.Equal((Side.Starboard, false, Side.Port), (paddler.Side, paddler.Changing, paddler.Coming));
        paddler.Lap(0.5 * strokeSeconds);
        Assert.Equal((Side.Starboard, false), (paddler.Side, paddler.Changing));
        paddler.Lap(0.2 * strokeSeconds);
        Assert.Equal((Side.Port, true, (Side?)null), (paddler.Side, paddler.Changing, paddler.Coming));
        paddler.Lap(strokeSeconds);
        Assert.False(paddler.Changing);
    }

    [Fact]
    public void APaddlerAtRestChangesSidesAtOnce()
    {
        var paddler = new Paddler(10);
        paddler.Rest();
        paddler.Lap(0.05);

        Assert.True(paddler.Paddle(1, -1, 0, 1.5));
        Assert.Equal((Side.Port, true), (paddler.Side, paddler.Changing));
    }

    [Fact]
    public void APaddlerWhoRestedOutHisStrokeChangesSidesAtOnce()
    {
        var paddler = new Paddler(10);
        paddler.Paddle(1, 0, 0, 1.5);
        paddler.Lap(0.6);
        paddler.Rest();
        paddler.Lap(1);

        Assert.True(paddler.Paddle(1, -1, 0, 1.5));
        Assert.Equal((Side.Port, true), (paddler.Side, paddler.Changing));
    }

    [Fact]
    public void APaddlerTakingUpBeforeHisStrokeEndsKeepsItsPhase()
    {
        var paddler = new Paddler(10);
        paddler.Paddle(1, 0, 0, 1.5);
        paddler.Lap(0.6);
        paddler.Rest();
        paddler.Lap(0.3);
        paddler.Paddle(1, 0, 0, 1.5);

        Assert.True(paddler.Paddle(1, -1, 0, 1.5));
        paddler.Lap(0.5);
        Assert.Equal(Side.Starboard, paddler.Side);
        paddler.Lap(0.2);
        Assert.Equal(Side.Port, paddler.Side);
    }

    [Theory]
    [InlineData("dugout")]
    [InlineData("outrigger")]
    [InlineData("barkcanoe")]
    [InlineData("reedboat")]
    [InlineData("coracle")]
    [InlineData("currach")]
    [InlineData("boat-raft")]
    public void HasEveryStrokeItsRowerTakesUp(string vessel)
    {
        var registered = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "patches"), "player-*.json")
            .SelectMany(file => JArray.Parse(File.ReadAllText(file)))
            .Where(op => op.Value<string>("path") == "/client/animations/-")
            .Select(op => op["value"]!.Value<string>("code")).ToHashSet();

        Assert.All(Fixtures.PropulsionOf(vessel)["strokes"]!.SelectTokens("*.*").Select(code => code.Value<string>()),
            code => Assert.Contains(code, registered));
    }

    [Fact]
    public void UnderWayAHullResistsTurningMoreThanLyingStill()
    {
        var drag = Drag("barkcanoe");

        Assert.True(drag.Turning(0.2, 2) < drag.Turning(0.2, 0));
        Assert.True(drag.Turning(0.2, -2) < drag.Turning(0.2, 0));
    }

    private static TholeOars Oars() => new(Blade("rowing", 0.1), 4.2);

    private static HeldTool Paddled(string vessel) =>
        new(Blade((string)Fixtures.PropulsionOf(vessel)["paddle"]!, Fixtures.PaddleSquareMetres), Sweep(vessel),
            (double?)Fixtures.PropulsionOf(vessel)["paddleAbeamMetres"] ?? 0);

    private static double SeatedWatts => Fixtures.Propulsion.Rower.MetabolicWatts * Fixtures.Propulsion.Strokes["seatedpole"].GrossEfficiency;

    private static PoleStroke Poled(string stroke, double metres, double depthMetres) =>
        new(stroke, Fixtures.Propulsion.Strokes[stroke], new Pole(metres, PoleDensity, Fixtures.Propulsion.Pole), depthMetres, SeatedHands, Fixtures.WaterDensity);

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
            speed += (propulsor.Push(1, 0, Side.Starboard, speed, 0, Watts(propulsor)).Newtons + drag.Along(speed)) / drag.AlongKg * StepSeconds;
        }
        return speed;
    }

    private static double StrokesASide(string vessel)
    {
        var propulsor = Paddled(vessel);
        var drag = Drag(vessel);
        var paddler = new Paddler(Fixtures.Propulsion.Strokes["paddling"].VeerDegrees);
        var strokeSeconds = 60 / propulsor.Stroke.StrokesPerMinute;
        double speed = 0, turn = 0, yaw = 0;
        var changes = new List<double>();
        for (var t = 0.0; t < 600; t += StepSeconds)
        {
            paddler.Lap(StepSeconds);
            if (paddler.Paddle(1, 0, (float)yaw, strokeSeconds) && t > 60) changes.Add(t);
            var push = paddler.Changing ? default : propulsor.Push(1, 0, paddler.Side, speed, turn, Watts(propulsor));
            speed += (push.Newtons + drag.Along(speed)) / drag.AlongKg * StepSeconds;
            turn += (push.NewtonMetres + drag.Turning(turn, speed)) / drag.TurningKgSquareMetres * StepSeconds;
            yaw += turn * StepSeconds;
        }
        var paddling = (changes[^1] - changes[0]) / (changes.Count - 1) / strokeSeconds;
        return paddling - ChangeStrokes;
    }

    private static double Spin(string vessel, Propulsor propulsor)
    {
        var drag = Drag(vessel);
        var turn = 0.0;
        for (var t = 0.0; t < 300; t += StepSeconds)
        {
            turn += (propulsor.Push(0, 1, Side.Starboard, 0, turn, Watts(propulsor)).NewtonMetres + drag.Turning(turn, 0)) / drag.TurningKgSquareMetres * StepSeconds;
        }
        return turn;
    }
}
