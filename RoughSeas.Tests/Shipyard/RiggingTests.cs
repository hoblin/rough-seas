using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RoughSeas.Shipyard;
using RoughSeas.Vessels;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace RoughSeas.Tests.Shipyard;

public class RiggingTests
{
    private static readonly JToken Entity = JToken.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "vessel", "outrigger.json")));
    private static readonly JObject Shape = JObject.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "shapes", "outrigger.json")))!;
    private static readonly RiggingConfig Config = JsonConvert.DeserializeObject<RiggingConfig>(Entity["behaviorConfigs"]![EntityBehaviorRigging.Code]!.ToString())!;

    private TreeAttribute? tree;

    private Framing Outrigger()
    {
        Config.Measure(Shape);
        return new Framing(() => tree, () => { }, Config.Stages);
    }

    private Framing JustJoined()
    {
        tree = new TreeAttribute();
        tree.SetInt(Config.Stages[0].Group, 1);
        return Outrigger();
    }

    private static void LayUpTo(Framing outrigger, string group)
    {
        while (outrigger.Current is { } stage && stage.Group != group) outrigger.Lay("oak", 0);
    }

    [Fact]
    public void AnOutriggerFromBeforeTheBuildIsFinishedWithHerFloatHewn()
    {
        var outrigger = Outrigger();

        Assert.True(outrigger.Finished(0));
        Assert.Contains("Blunt1", outrigger.Hidden());
        Assert.Contains("Blunt2", outrigger.Hidden());
        Assert.DoesNotContain("Ends1", outrigger.Hidden());
    }

    [Fact]
    public void HerFirstBoomIsAcrossHerOnceTheFloatJoins()
    {
        var outrigger = JustJoined();

        Assert.Equal("Booms", outrigger.Current?.Group);
        Assert.Equal(1, outrigger.Laid(outrigger.Current!));
        Assert.DoesNotContain("Booms1", outrigger.Hidden());
        Assert.Contains("Booms2", outrigger.Hidden());
    }

    [Fact]
    public void SheIsRiggedInTheOrderARealOutriggerIs()
    {
        var outrigger = JustJoined();
        var order = new List<string>();
        while (outrigger.Current is { } stage)
        {
            if (order.LastOrDefault() != stage.Group) order.Add(stage.Group);
            outrigger.Lay(null, 0);
        }

        Assert.Equal(["Booms", "Lashing", "Ends", "Mast", "SailFurled", "Stays", "Strakes", "Seams"], order);
    }

    [Fact]
    public void SheIsFinishedBeforeHerWashstrakesAndTheyRaiseHerSideOnceSewn()
    {
        var outrigger = JustJoined();
        LayUpTo(outrigger, "Strakes");

        Assert.True(outrigger.Finished(0));
        Assert.Equal(0, outrigger.Raised(stage => stage.RaisesMetres));

        while (outrigger.Current is not null) outrigger.Lay(null, 0);

        Assert.Equal(0.15, outrigger.Raised(stage => stage.RaisesMetres), 6);
    }

    [Fact]
    public void AnOutriggerFromBeforeTheBuildHasHerWashstrakes() =>
        Assert.Equal(422, Outrigger().Raised(stage => stage.RaisesLitres));

    [Fact]
    public void HewingAnEndOfHerFloatTakesItsBluntEndAway()
    {
        var outrigger = JustJoined();
        LayUpTo(outrigger, "Ends");
        Assert.DoesNotContain("Blunt1", outrigger.Hidden());

        outrigger.Lay(null, 0);

        Assert.Contains("Blunt1", outrigger.Hidden());
        Assert.DoesNotContain("Ends1", outrigger.Hidden());
        Assert.DoesNotContain("Blunt2", outrigger.Hidden());
    }

    [Fact]
    public void EveryPoleSheTakesIsCutFromAThreeMetrePole() =>
        Assert.All(Outrigger().Stages.Where(stage => stage.Work == FramingWork.Pole).SelectMany(stage => stage.Metres), metres => Assert.InRange(metres, 2.5, 3));

    [Fact]
    public void HerFirstBoomIsOneOfHerPolesOfFrame()
    {
        var outrigger = JustJoined();
        var poles = outrigger.Stages.Where(stage => stage.Work == FramingWork.Pole).Sum(stage => stage.Pieces);

        Assert.Equal(1.0 / poles, outrigger.Framed, 6);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(GameMath.PIHALF)]
    [InlineData(GameMath.PI)]
    public void AFloatRowOnHerStarboardSideJoinsHerAsSheLies(float yaw) =>
        Assert.Equal((Row(yaw, 1).Centre, false), Float(yaw, Row(yaw, 1)) is var (row, turned) ? (row.Centre, turned) : default);

    [Theory]
    [InlineData(0)]
    [InlineData(GameMath.PIHALF)]
    public void AFloatRowOnHerPortSideTurnsHerEndForEnd(float yaw) =>
        Assert.Equal((Row(yaw, -1).Centre, true), Float(yaw, Row(yaw, -1)) is var (row, turned) ? (row.Centre, turned) : default);

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void OnlyARowOfHerFloatsLogsMakesHerFloat(int logs) => Assert.Null(Float(0, Row(0, 1, logs)));

    [Fact]
    public void LogsLyingAcrossHerMakeNoFloat() => Assert.Null(Float(GameMath.PIHALF, Row(0, 1)));

    [Fact]
    public void ARowNotCentredOnHerMakesNoFloat()
    {
        var centred = Row(0, 1);
        Assert.Null(Float(0, centred with { Centre = centred.Centre.AddCopy(centred.Along == EnumAxis.X ? 1 : 0, 0, centred.Along == EnumAxis.Z ? 1 : 0) }));
    }

    private static readonly Vec3d Hull = new(100.5, 5, 200.5);

    private static LogRow Row(float yaw, int side, int logs = 3)
    {
        var (alongX, alongZ) = HullShape.ToWorld(1, 0, yaw);
        var (x, z) = HullShape.ToWorld(0, side * Config.FloatOffMetres, yaw);
        var axis = Math.Abs(alongX) > Math.Abs(alongZ) ? EnumAxis.X : EnumAxis.Z;
        var centre = new BlockPos((int)Math.Floor(Hull.X + x), (int)Hull.Y, (int)Math.Floor(Hull.Z + z));
        return new LogRow(new Wood("game", "oak"), centre, axis, logs);
    }

    private static (LogRow Row, bool EndForEnd)? Float(float yaw, LogRow laid)
    {
        var logs = laid.Blocks.ToDictionary(pos => pos, _ => new LyingLog(laid.Wood, laid.Along));
        return EntityBehaviorRiggable.FloatBeside(Hull.X, Hull.Y, Hull.Z, yaw, Config.FloatLogs, Config.FloatOffMetres, logs.GetValueOrDefault);
    }
}
