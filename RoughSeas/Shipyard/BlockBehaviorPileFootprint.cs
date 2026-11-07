using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Shipyard;

/// <summary>On vanilla's <c>game:groundstorage</c> (patches/shipyard-groundstorage.json): takes up the fillers <see cref="CollectibleBehaviorLongPile"/> set beside a pile.</summary>
public class BlockBehaviorPileFootprint(Block block) : BlockBehavior(block)
{
    public const string Code = "roughseas:pilefootprint";

    private static readonly Vec3i[] Reach = Enumerable.Range(-PileFootprint.MaxReach, 2 * PileFootprint.MaxReach + 1)
        .Where(blocks => blocks != 0)
        .SelectMany(blocks => new[] { new Vec3i(blocks, 0, 0), new Vec3i(0, 0, blocks) })
        .ToArray();

    public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos, ref EnumHandling handling)
    {
        var held = Reach.Select(offset => pos.AddCopy(offset.X, 0, offset.Z))
            .Where(at => world.BlockAccessor.GetBlock(at) is BlockMultiblock filler && filler.Offset.Equals(new Vec3i(at.X - pos.X, 0, at.Z - pos.Z)))
            .ToList();
        foreach (var at in held) world.BlockAccessor.SetBlock(0, at);
        if (held.Count > 0) world.Api.ModLoader.GetModSystem<ShipyardModSystem>().Log("the pile at {0} is gone, and the {1} blocks it held beside it", pos, held.Count);
    }
}
