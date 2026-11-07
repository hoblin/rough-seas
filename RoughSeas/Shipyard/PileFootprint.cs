using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace RoughSeas.Shipyard;

public sealed record PileFootprint(int Metres, float MeshAngle)
{
    /// <summary>As far from its pile as a vanilla filler can stand: <c>multiblock-monolithic</c> has offsets n2 to p2.</summary>
    public const int MaxReach = 2;

    public IEnumerable<BlockPos> Around(BlockPos pile) => Enumerable.Range(-(Metres - 1) / 2, Metres).Select(blocks =>
    {
        var turned = new Matrixf().RotateY(MeshAngle).TransformVector(new Vec4f(blocks, 0, 0, 0));
        return pile.AddCopy((int)Math.Round(turned.X), 0, (int)Math.Round(turned.Z));
    });
}
