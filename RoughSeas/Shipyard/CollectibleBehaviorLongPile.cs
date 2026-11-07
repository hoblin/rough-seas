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

    private static readonly AssetLocation GroundStorage = new("groundstorage");

    private int Metres => int.Parse(collObj.Variant["length"]);

    /// <summary>Whether <see cref="BlockGroundStorage.CreateStorage"/> may start a pile of this where it is about to.</summary>
    public bool MayStart(EntityAgent byEntity, BlockSelection blockSel)
    {
        var world = byEntity.World;
        var pile = blockSel.Face == null ? blockSel.Position : blockSel.Position.AddCopy(blockSel.Face);
        var footprint = new PileFootprint(Metres, Angle(byEntity, blockSel)).Around(pile).ToList();
        if (LandClaims.MayBreak(byEntity, footprint) && footprint.Where(at => !at.Equals(pile)).All(at => Free(world, at) &&
                world.BlockAccessor.GetBlock(at.DownCopy()).CanAttachBlockAt(world.BlockAccessor, Pile(world), at.DownCopy(), BlockFacing.UP)))
        {
            return true;
        }

        Shipyard(world).Log("{0} finds no room for a {1} m pile of {2} at {3}", byEntity.GetName(), Metres, collObj.Code, pile);
        (byEntity.Api as ICoreClientAPI)?.TriggerIngameError(this, "notenoughspace", Lang.Get("placefailure-notenoughspace"));
        return false;
    }

    public void Fill(IWorldAccessor world, BlockEntityGroundStorage storage)
    {
        var filled = new PileFootprint(Metres, storage.MeshAngle).Around(storage.Pos)
            .Where(at => !at.Equals(storage.Pos))
            .ToList();
        foreach (var at in filled)
        {
            world.BlockAccessor.SetBlock(world.GetBlock(new AssetLocation($"multiblock-monolithic-{Signed(at.X - storage.Pos.X)}-0-{Signed(at.Z - storage.Pos.Z)}")).Id, at);
        }
        if (filled.Count > 0) Shipyard(world).Log("a {0} m pile of {1} at {2} holds {3} blocks beside it", Metres, collObj.Code, storage.Pos, filled.Count);
    }

    /// <summary>The turn <see cref="BlockGroundStorage.CreateStorage"/> gives the new pile, worked out the same way.</summary>
    private static float Angle(EntityAgent byEntity, BlockSelection blockSel)
    {
        var at = blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.Opposite) : blockSel.Position;
        var toward = (float)Math.Atan2(byEntity.Pos.X - (at.X + blockSel.HitPosition.X), (float)byEntity.Pos.Z - (at.Z + blockSel.HitPosition.Z));
        return (int)Math.Round(toward / GameMath.PIHALF) * GameMath.PIHALF;
    }

    private static bool Free(IWorldAccessor world, BlockPos at) => world.BlockAccessor.GetBlock(at).IsReplacableBy(Pile(world));

    private static Block Pile(IWorldAccessor world) => world.GetBlock(GroundStorage);

    private static ShipyardModSystem Shipyard(IWorldAccessor world) => world.Api.ModLoader.GetModSystem<ShipyardModSystem>();

    private static string Signed(int blocks) => blocks < 0 ? $"n{-blocks}" : blocks > 0 ? $"p{blocks}" : "0";
}
