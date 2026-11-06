using Vintagestory.API.Common;

namespace RoughSeas.Shipyard;

public class CollectibleBehaviorDrive(CollectibleObject collectible) : CollectibleBehaviorBlows(collectible)
{
    public const string Code = "roughseas:drive";

    /// <summary>Its cycle in patches/player-poses.json is one blow: 45 frames, wedge.json's <c>secondsPerBlow</c> of 1.5.</summary>
    protected override string Animation => "roughseas-drive";

    protected override IStruck? Struck(IWorldAccessor world, BlockSelection? blockSel, EntitySelection? entitySel) =>
        entitySel == null && blockSel != null && world.BlockAccessor.GetBlock(blockSel.Position) is BlockWedge wedge ? wedge.At(world, blockSel.Position) : null;
}
