using System;
using System.Linq;
using RoughSeas.Vessels;
using Vintagestory.API.MathTools;

namespace RoughSeas.Shipyard;

public sealed record LogRow(Wood Wood, BlockPos Centre, EnumAxis Along, BlockFacing Water)
{
    public const int Length = 5;

    private static readonly int[] Offsets = Enumerable.Range(-Length / 2, Length).ToArray();

    public BlockPos[] Blocks => Offsets.Select(offset => At(Centre, Along, offset)).ToArray();

    public static LogRow? Find(BlockPos centre, Func<BlockPos, LyingLog?> logAt, Func<BlockPos, bool> waterAt)
    {
        if (logAt(centre) is not { } log) return null;

        var row = Offsets.Select(offset => At(centre, log.Axis, offset)).ToArray();
        if (row.Any(pos => logAt(pos) != log)) return null;

        var water = Across(log.Axis).FirstOrDefault(side => row.All(pos => Wet(pos.AddCopy(side), waterAt)));
        return water == null ? null : new LogRow(log.Wood, centre, log.Axis, water);
    }

    private static bool Wet(BlockPos beside, Func<BlockPos, bool> waterAt) => waterAt(beside) || waterAt(beside.DownCopy());

    private static BlockPos At(BlockPos centre, EnumAxis axis, int offset) =>
        axis == EnumAxis.X ? centre.AddCopy(offset, 0, 0) : centre.AddCopy(0, 0, offset);

    private static BlockFacing[] Across(EnumAxis axis) =>
        axis == EnumAxis.X ? [BlockFacing.NORTH, BlockFacing.SOUTH] : [BlockFacing.EAST, BlockFacing.WEST];
}

public sealed record LyingLog(Wood Wood, EnumAxis Axis);
