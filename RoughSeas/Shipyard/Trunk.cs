using System;
using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace RoughSeas.Shipyard;

public sealed record Trunk(IReadOnlyList<BlockPos> Blocks)
{
    public BlockPos Foot => Blocks[0];

    public int Height => Blocks.Count;

    public static Trunk Through(BlockPos pos, Func<BlockPos, bool> trunkAt)
    {
        var foot = pos.Copy();
        while (trunkAt(foot.DownCopy())) foot.Down();

        var blocks = new List<BlockPos>();
        for (var at = foot; trunkAt(at); at = at.UpCopy()) blocks.Add(at);
        return new Trunk(blocks);
    }
}
