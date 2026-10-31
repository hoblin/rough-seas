using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace RoughSeas.Shipyard;

public class CollectibleBehaviorLathBed(CollectibleObject collectible) : CollectibleBehavior(collectible)
{
    public const string Code = "roughseas:lathbed";

    private static readonly AssetLocation Bed = new("roughseas:lathbed");

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent,
        ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (blockSel == null || entitySel != null || !byEntity.Controls.ShiftKey || slot.Itemstack is not { } stack) return;

        var world = byEntity.World;
        var accessor = world.BlockAccessor;
        var pos = accessor.GetBlockEntity(blockSel.Position) is BlockEntityLathBed ? blockSel.Position : blockSel.Position.UpCopy();
        if (accessor.GetBlockEntity(pos) is not BlockEntityLathBed && !Ground(accessor, blockSel, pos)) return;

        handling = EnumHandling.PreventSubsequent;
        handHandling = EnumHandHandling.PreventDefault;
        if (!BlockEntityLathBed.Wet(stack))
        {
            (byEntity.Api as ICoreClientAPI)?.TriggerIngameError(this, "shipyard-soak-laths", Lang.Get("roughseas:shipyard-soak-laths"));
            return;
        }
        if (world.Side != EnumAppSide.Server || !world.Claims.TryAccess((byEntity as EntityPlayer)?.Player, pos, EnumBlockAccessFlags.BuildOrBreak)) return;

        if (accessor.GetBlockEntity(pos) is not BlockEntityLathBed bed)
        {
            accessor.SetBlock(world.GetBlock(Bed).Id, pos);
            bed = (BlockEntityLathBed)accessor.GetBlockEntity(pos);
        }
        var hit = blockSel.Position.ToVec3d().Add(blockSel.HitPosition);
        var facing = BlockFacing.HorizontalFromYaw(byEntity.Pos.Yaw);
        var lath = slot.TakeOut(1);
        bed.Add(lath, Snap(hit.X), Snap(hit.Z), facing.Axis);
        slot.MarkDirty();
        world.Api.ModLoader.GetModSystem<ShipyardModSystem>().Log("{0} lays {1} on the ground at {2:F3} {3:F3}, along {4}", byEntity.GetName(), lath.Collectible.Code,
            Snap(hit.X), Snap(hit.Z), facing.Axis);
        world.PlaySoundAt(new AssetLocation("game:sounds/block/planks"), pos, 0, (byEntity as EntityPlayer)?.Player);
    }

    private static bool Ground(IBlockAccessor accessor, BlockSelection blockSel, BlockPos pos) =>
        blockSel.Face == BlockFacing.UP
        && accessor.GetBlock(blockSel.Position).CanAttachBlockAt(accessor, accessor.GetBlock(pos), blockSel.Position, BlockFacing.UP)
        && accessor.GetBlock(pos).Replaceable >= 6000;

    private static double Snap(double metres) => Math.Round(metres * 16) / 16;
}
