using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Shipyard;

/// <summary>
/// On vanilla's <c>game:groundstorage</c> (patches/shipyard-groundstorage.json), ahead of its <c>UnstableFalling</c>: a long
/// pile rests on its whole length, and takes up the fillers <see cref="CollectibleBehaviorLongPile"/> set beside it.
/// </summary>
public class BlockBehaviorPileFootprint(Block block) : BlockBehavior(block), IWorkedBlock
{
    public const string Code = "roughseas:pilefootprint";

    public IWorkedHull? HullAt(IWorldAccessor world, BlockPos pos) => LathPile.At(world, pos);

    private static readonly Vec3i[] Reach = Enumerable.Range(-PileFootprint.MaxReach, 2 * PileFootprint.MaxReach + 1)
        .Where(blocks => blocks != 0)
        .SelectMany(blocks => new[] { new Vec3i(blocks, 0, 0), new Vec3i(0, 0, blocks) })
        .ToArray();

    public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos, ref EnumHandling handling)
    {
        if (world.Side != EnumAppSide.Server || Held(world, pos) is not { Count: > 0 } held) return;

        handling = EnumHandling.PreventSubsequent;
        if (held.Append(pos).Any(at => Rests(world, at))) return;

        Shipyard(world).Log("the pile at {0} has nothing under its length and falls apart", pos);
        world.BlockAccessor.BreakBlock(pos, null);
    }

    public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos, ref EnumHandling handling)
    {
        var held = Held(world, pos);
        foreach (var at in held) world.BlockAccessor.SetBlock(0, at);
        if (held.Count > 0) Shipyard(world).Log("the pile at {0} is gone, and the {1} blocks it held beside it", pos, held.Count);
    }

    private static List<BlockPos> Held(IWorldAccessor world, BlockPos pos) => Reach.Select(offset => pos.AddCopy(offset.X, 0, offset.Z))
        .Where(at => world.BlockAccessor.GetBlock(at) is BlockMultiblock filler && filler.Offset.Equals(new Vec3i(at.X - pos.X, 0, at.Z - pos.Z)))
        .ToList();

    private bool Rests(IWorldAccessor world, BlockPos at) =>
        world.BlockAccessor.GetBlock(at.DownCopy()).CanAttachBlockAt(world.BlockAccessor, block, at.DownCopy(), BlockFacing.UP);

    private static ShipyardModSystem Shipyard(IWorldAccessor world) => world.Api.ModLoader.GetModSystem<ShipyardModSystem>();
}
