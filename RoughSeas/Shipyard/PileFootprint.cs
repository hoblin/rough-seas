using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace RoughSeas.Shipyard;

/// <summary>The blocks a pile reaches over, each as its offset from the pile's own block in the pile's frame, x along its pieces.</summary>
public sealed record PileFootprint(int[][] Blocks, float MeshAngle)
{
    /// <summary>As far from its pile as a vanilla filler can stand: <c>multiblock-monolithic</c> has offsets n2 to p2.</summary>
    public const int MaxReach = 2;

    /// <summary>Pieces <paramref name="metres"/> long lying lengthwise from the pile's block, the far side taking the extra block of an even length.</summary>
    public static int[][] Lengthwise(int metres) => Enumerable.Range(-(metres - 1) / 2, metres).Select(blocks => new[] { blocks, 0 }).ToArray();

    public IEnumerable<BlockPos> Around(BlockPos pile) => Blocks.Select(offset =>
    {
        var turned = new Matrixf().RotateY(MeshAngle).TransformVector(new Vec4f(offset[0], 0, offset[1], 0));
        return pile.AddCopy((int)Math.Round(turned.X), 0, (int)Math.Round(turned.Z));
    });
}
