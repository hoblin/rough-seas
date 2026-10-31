using System;
using System.Linq;
using RoughSeas.Vessels;
using Block = Vintagestory.API.Common.Block;
using Vintagestory.API.MathTools;

namespace RoughSeas.Shipyard;

public sealed record LogRow(Wood Wood, BlockPos Centre, EnumAxis Along)
{
    public const int Length = 5;

    private static readonly int[] Offsets = Enumerable.Range(-Length / 2, Length).ToArray();

    public BlockPos[] Blocks => Offsets.Select(offset => At(Centre, Along, offset)).ToArray();

    public static LogRow? Find(BlockPos centre, Func<BlockPos, LyingLog?> logAt)
    {
        if (logAt(centre) is not { } log) return null;

        return Offsets.All(offset => logAt(At(centre, log.Axis, offset)) == log) ? new LogRow(log.Wood, centre, log.Axis) : null;
    }

    public static LogRow? Through(BlockPos pos, Func<BlockPos, LyingLog?> logAt) =>
        logAt(pos) is { } log ? Offsets.Select(offset => Find(At(pos, log.Axis, -offset), logAt)).FirstOrDefault(row => row != null) : null;

    public BlockFacing? Water(Func<BlockPos, bool> waterAt) =>
        Across(Along).FirstOrDefault(side => Blocks.All(pos => waterAt(pos.AddCopy(side)) || waterAt(pos.AddCopy(side).DownCopy())));

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
