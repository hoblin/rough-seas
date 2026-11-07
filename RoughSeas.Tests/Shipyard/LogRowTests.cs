using RoughSeas.Shipyard;
using RoughSeas.Vessels;
using Vintagestory.API.MathTools;

namespace RoughSeas.Tests.Shipyard;

public class LogRowTests
{
    private static readonly Wood Oak = new("game", "oak");
    private static readonly Wood Birch = new("game", "birch");
    private static readonly BlockPos Centre = new(10, 5, 10);

    private readonly Dictionary<BlockPos, LyingLog> logs = [];
    private readonly HashSet<BlockPos> water = [];

    [Fact]
    public void FiveLogsOfOneWoodBesideTheWaterMakeARow()
    {
        Lay(Oak, Oak, Oak, Oak, Oak);
        Flood(BlockFacing.SOUTH, 0);

        var row = Find();

        Assert.NotNull(row);
        Assert.Equal(Oak, row.Wood);
        Assert.Equal(BlockFacing.SOUTH, row.Water(water.Contains));
        Assert.Equal(Enumerable.Range(8, 5), row.Blocks.Select(pos => pos.X));
    }

    [Fact]
    public void WaterABlockLowerIsStillTheVeryNextWater()
    {
        Lay(Oak, Oak, Oak, Oak, Oak);
        Flood(BlockFacing.NORTH, -1);

        Assert.Equal(BlockFacing.NORTH, Find()?.Water(water.Contains));
    }

    [Fact]
    public void MixedWoodsStayAWallOfLogs()
    {
        Lay(Oak, Oak, Birch, Oak, Oak);
        Flood(BlockFacing.SOUTH, 0);

        Assert.Null(Find());
    }

    [Fact]
    public void LogsLyingAcrossTheRowMakeNoTrunk()
    {
        foreach (var x in Enumerable.Range(8, 5)) logs[new BlockPos(x, 5, 10)] = new LyingLog(Oak, EnumAxis.Z);
        Flood(BlockFacing.SOUTH, 0);

        Assert.Null(Find());
    }

    [Fact]
    public void ARowWithDryLandBesideOneLogIsNotBesideTheWater()
    {
        Lay(Oak, Oak, Oak, Oak, Oak);
        Flood(BlockFacing.SOUTH, 0);
        water.Remove(new BlockPos(12, 5, 11));

        Assert.Null(Find()!.Water(water.Contains));
    }

    [Fact]
    public void FourLogsMakeNoRowOfFive()
    {
        Lay(Oak, Oak, Oak, Oak);

        Assert.Null(LogRow.Find(new BlockPos(9, 5, 10), 5, logs.GetValueOrDefault));
    }

    [Fact]
    public void ARunIsTheSameWholeRowFromAnyOfItsLogs()
    {
        Lay(Oak, Oak, Oak, Oak, Oak);

        Assert.All(Enumerable.Range(8, 5), x =>
        {
            var run = LogRow.Run(new BlockPos(x, 5, 10), logs.GetValueOrDefault, 5);
            Assert.Equal((Centre, 5), (run?.Centre, run?.Length));
        });
    }

    [Fact]
    public void ARunOfThreeIsCentredOnItsMiddleLog()
    {
        Lay(Oak, Oak, Oak);

        var run = LogRow.Run(new BlockPos(8, 5, 10), logs.GetValueOrDefault, 5)!;

        Assert.Equal(3, run.Length);
        Assert.Equal(new BlockPos(9, 5, 10), run.Centre);
        Assert.Equal(Enumerable.Range(8, 3), run.Blocks.Select(pos => pos.X));
    }

    [Fact]
    public void ARunEndsWhereTheWoodChanges()
    {
        Lay(Oak, Oak, Birch, Oak, Oak);

        Assert.Equal(2, LogRow.Run(new BlockPos(8, 5, 10), logs.GetValueOrDefault, 5)?.Length);
    }

    [Fact]
    public void ARunLongerThanAskedComesBackLongerThanAskedNotWhole()
    {
        foreach (var x in Enumerable.Range(0, 20)) logs[new BlockPos(x, 5, 10)] = new LyingLog(Oak, EnumAxis.X);

        Assert.InRange(LogRow.Run(new BlockPos(10, 5, 10), logs.GetValueOrDefault, 3)!.Length, 4, 7);
    }

    private void Lay(params Wood[] woods)
    {
        for (int i = 0; i < woods.Length; i++) logs[new BlockPos(8 + i, 5, 10)] = new LyingLog(woods[i], EnumAxis.X);
    }

    private void Flood(BlockFacing side, int dy)
    {
        foreach (var x in Enumerable.Range(8, 5)) water.Add(new BlockPos(x, 5 + dy, 10).AddCopy(side));
    }

    private LogRow? Find() => LogRow.Find(Centre, 5, logs.GetValueOrDefault);
}
