using System;
using System.Linq;
using RoughSeas.Vessels;
using Block = Vintagestory.API.Common.Block;
using Vintagestory.API.MathTools;

namespace RoughSeas.Shipyard;

public sealed record LogRow(Wood Wood, BlockPos Centre, EnumAxis Along, int Length)
{
    public BlockPos[] Blocks => Offsets(Length).Select(offset => At(Centre, Along, offset)).ToArray();

    public static LogRow? Find(BlockPos centre, int length, Func<BlockPos, LyingLog?> logAt)
    {
        if (logAt(centre) is not { } log) return null;

        return Offsets(length).All(offset => logAt(At(centre, log.Axis, offset)) == log) ? new LogRow(log.Wood, centre, log.Axis, length) : null;
    }

    /// <summary>The logs of one wood lying end to end through <paramref name="pos"/>, counted no further than past <paramref name="longest"/>: a longer run comes back longer than it, not whole.</summary>
    public static LogRow? Run(BlockPos pos, Func<BlockPos, LyingLog?> logAt, int longest)
    {
        if (logAt(pos) is not { } log) return null;

        int Reach(int step) => Enumerable.Range(1, longest).TakeWhile(n => logAt(At(pos, log.Axis, n * step)) == log).Count();
        int back = Reach(-1), length = back + 1 + Reach(1);
        return new LogRow(log.Wood, At(pos, log.Axis, (length - 1) / 2 - back), log.Axis, length);
    }

    public BlockFacing? Water(Func<BlockPos, bool> waterAt) =>
        Across(Along).FirstOrDefault(side => Blocks.All(pos => waterAt(pos.AddCopy(side)) || waterAt(pos.AddCopy(side).DownCopy())));

    private static int[] Offsets(int length) => Enumerable.Range(-(length - 1) / 2, length).ToArray();

    private static BlockPos At(BlockPos centre, EnumAxis axis, int offset) =>
        axis == EnumAxis.X ? centre.AddCopy(offset, 0, 0) : centre.AddCopy(0, 0, offset);

    private static BlockFacing[] Across(EnumAxis axis) =>
        axis == EnumAxis.X ? [BlockFacing.NORTH, BlockFacing.SOUTH] : [BlockFacing.EAST, BlockFacing.WEST];
}

public sealed record LyingLog(Wood Wood, EnumAxis Axis)
{
    private static readonly string[] Lying = ["log-placed-", "logsection-placed-"];

    public static LyingLog? Of(Block block) =>
        block.Code?.Path is { } path && Lying.Any(path.StartsWith) && block.Variant["wood"] is { } species
            ? block.Variant["rotation"] switch
            {
                "we" => new LyingLog(new Wood(block.Code.Domain, species), EnumAxis.X),
                "ns" => new LyingLog(new Wood(block.Code.Domain, species), EnumAxis.Z),
                _ => null
            }
            : null;
}
