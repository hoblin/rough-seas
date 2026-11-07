using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Shipyard;

public class CollectibleBehaviorLongPile(CollectibleObject collectible) : CollectibleBehavior(collectible)
{
    public const string Code = "roughseas:longpile";

    private const int Replaceable = 6000;

    private int Metres => int.Parse(collObj.Variant["length"]);

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent,
        ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (blockSel == null || entitySel != null || !byEntity.Controls.ShiftKey || slot.Itemstack is not { } stack) return;

        var world = byEntity.World;
        var accessor = world.BlockAccessor;
        var filler = accessor.GetBlock(blockSel.Position) as BlockMultiblock;
        var aimed = blockSel.Clone();
        if (filler != null) aimed.Position.Add(filler.OffsetInv);

        var pile = NewPile(world, aimed, stack);
        if (pile != null && !Room(accessor, new PileFootprint(Metres, Angle(byEntity, aimed)), pile))
        {
            Shipyard(world).Log("{0} finds no room for a {1} m pile of {2} at {3}", byEntity.GetName(), Metres, collObj.Code, pile);
            (byEntity.Api as ICoreClientAPI)?.TriggerIngameError(this, "notenoughspace", Lang.Get("placefailure-notenoughspace"));
            (handHandling, handling) = (EnumHandHandling.PreventDefault, EnumHandling.PreventSubsequent);
            return;
        }

        CollectibleBehaviorGroundStorable.Interact(slot, byEntity, aimed, entitySel, firstEvent, ref handHandling, ref handling);
        if (filler != null || handHandling == EnumHandHandling.PreventDefault) handling = EnumHandling.PreventSubsequent;
        if (world.Side == EnumAppSide.Server && pile != null && accessor.GetBlockEntity(pile) is BlockEntityGroundStorage storage) Fill(world, storage);
    }

    /// <summary>Where <see cref="CollectibleBehaviorGroundStorable.Interact"/> will start a pile, if it starts one.</summary>
    private static BlockPos? NewPile(IWorldAccessor world, BlockSelection aimed, ItemStack stack)
    {
        var accessor = world.BlockAccessor;
        if ((accessor.GetBlockEntity(aimed.Position) as BlockEntityGroundStorage ?? accessor.GetBlockEntity(aimed.Position.UpCopy()) as BlockEntityGroundStorage) is not { } storage)
        {
            return aimed.Face == BlockFacing.UP ? aimed.Position.UpCopy() : null;
        }
        return storage.TotalStackSize >= storage.Capacity && storage.Inventory[0].Itemstack?.Equals(world, stack, GlobalConstants.IgnoredStackAttributes) == false
            ? storage.Pos.UpCopy()
            : null;
    }

    /// <summary>The turn <see cref="BlockGroundStorage.CreateStorage"/> will give the new pile, worked out the same way.</summary>
    private static float Angle(EntityAgent byEntity, BlockSelection aimed)
    {
        var at = aimed.DidOffset ? aimed.Position.AddCopy(aimed.Face.Opposite) : aimed.Position;
        var toward = (float)Math.Atan2(byEntity.Pos.X - (at.X + aimed.HitPosition.X), byEntity.Pos.Z - (at.Z + aimed.HitPosition.Z));
        return (int)Math.Round(toward / GameMath.PIHALF) * GameMath.PIHALF;
    }

    private static bool Room(IBlockAccessor accessor, PileFootprint footprint, BlockPos pile) =>
        footprint.Around(pile).Where(at => !at.Equals(pile)).All(at =>
            accessor.GetBlock(at).Replaceable >= Replaceable &&
            accessor.GetBlock(at.DownCopy()).CanAttachBlockAt(accessor, accessor.GetBlock(at), at.DownCopy(), BlockFacing.UP));

    private void Fill(IWorldAccessor world, BlockEntityGroundStorage storage)
    {
        var filled = new PileFootprint(Metres, storage.MeshAngle).Around(storage.Pos)
            .Where(at => !at.Equals(storage.Pos) && world.BlockAccessor.GetBlock(at) is not BlockMultiblock)
            .ToList();
        foreach (var at in filled)
        {
            world.BlockAccessor.SetBlock(world.GetBlock(new AssetLocation($"multiblock-monolithic-{Signed(at.X - storage.Pos.X)}-0-{Signed(at.Z - storage.Pos.Z)}")).Id, at);
        }
        if (filled.Count > 0) Shipyard(world).Log("a {0} m pile of {1} at {2} holds {3} blocks beside it", Metres, collObj.Code, storage.Pos, filled.Count);
    }

    private static ShipyardModSystem Shipyard(IWorldAccessor world) => world.Api.ModLoader.GetModSystem<ShipyardModSystem>();

    private static string Signed(int blocks) => blocks < 0 ? $"n{-blocks}" : blocks > 0 ? $"p{blocks}" : "0";
}
