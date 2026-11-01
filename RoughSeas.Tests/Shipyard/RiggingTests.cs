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
        Assert.Contains("FloatLog1", outrigger.Hidden());
        Assert.Contains("Hewing5", outrigger.Hidden());
        Assert.Contains("Booms1At0", outrigger.Hidden());
        Assert.DoesNotContain("Hewing6", outrigger.Hidden());
        Assert.DoesNotContain("Booms1At6", outrigger.Hidden());
    }

    [Fact]
    public void HerFloatJoinsAsTheThreeLogsTheyWereAndIsHewnDownPassByPass()
    {
        var outrigger = JustJoined();
        Assert.DoesNotContain("FloatLog1", outrigger.Hidden());
        Assert.DoesNotContain("Booms1At0", outrigger.Hidden());

        LayUpTo(outrigger, "Hewing");
        outrigger.Lay(null, 0);

        Assert.Contains("FloatLog1", outrigger.Hidden());
        Assert.DoesNotContain("Hewing1", outrigger.Hidden());
        Assert.Contains("Booms1At0", outrigger.Hidden());
        Assert.DoesNotContain("Booms3At1", outrigger.Hidden());

        outrigger.Lay(null, 0);

        Assert.Contains("Hewing1", outrigger.Hidden());
        Assert.DoesNotContain("Hewing2", outrigger.Hidden());
        Assert.DoesNotContain("Booms2At2", outrigger.Hidden());
    }

    [Fact]
    public void HerFloatsLogsAreThreeCubicMetresHewnDownToItsRoundLog()
    {
        var hewing = Outrigger().Stages.Single(stage => stage.Group == Config.FloatStage);

        Assert.Equal(3000, hewing.WoodLitres[0], 0);
        Assert.Equal(3, hewing.WaterlineSquareMetres[0], 2);
        Assert.Equal(hewing.WoodLitres.OrderByDescending(litres => litres), hewing.WoodLitres);
        Assert.InRange(hewing.WoodLitres[^1], 150, 300);
    }

    [Theory]
    [InlineData(100.5, 200.5, 0)]
    [InlineData(100.2, 200.9, 0)]
    [InlineData(100.9, 200.1, GameMath.PIHALF)]
    public void HerHullIsBroughtOntoTheLogsSoTheFloatLiesWhereTheyLay(double x, double z, float yaw)
    {
        var (dx, dz) = HullShape.ToWorld(0, Config.FloatOffMetres, yaw);
        var row = new BlockPos((int)Math.Floor(x + dx), 5, (int)Math.Floor(z + dz));
        var (moveX, moveZ) = EntityBehaviorRiggable.OntoRow(x, z, yaw, Config.FloatOffMetres, row);

        Assert.Equal(row.X + 0.5, x + moveX + dx, 6);
        Assert.Equal(row.Z + 0.5, z + moveZ + dz, 6);
        Assert.InRange(Math.Abs(moveX), 0, 0.5);
        Assert.InRange(Math.Abs(moveZ), 0, 0.5);
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

        Assert.Equal(["Booms", "Hewing", "Lashing", "Mast", "SailFurled", "Stays", "Strakes", "Seams"], order);
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
