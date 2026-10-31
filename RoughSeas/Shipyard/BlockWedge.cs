using System.Linq;
using RoughSeas.Vessels;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace RoughSeas.Shipyard;

/// <summary>
/// A wedge driven into a lying log. With one on each of five logs lying end to end, the axe drives them all, and the
/// trunk splits along its grain into a bundle of laths.
/// </summary>
public class BlockWedge : Block
{
    public const string ClassName = "roughseas:BlockWedge";

    private static readonly AssetLocation SplitSound = new("game:sounds/effect/treefell");

    private readonly HeldWork work = new();
    private double seconds;
    private int laths;
    private string lathCode = "";

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        seconds = Attributes["splitSeconds"].AsDouble();
        laths = Attributes["laths"].AsInt();
        lathCode = Attributes["lath"].AsString("");
    }

    public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref string failureCode)
    {
        if (LyingLog.Of(world.BlockAccessor.GetBlock(blockSel.Position.DownCopy())) != null) return base.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode);

        failureCode = "roughseas-wedge-on-log";
        return false;
    }

    public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
    {
        var log = LyingLog.Of(world.BlockAccessor.GetBlock(blockSel.Position.DownCopy()))!;
        var grain = world.GetBlock(CodeWithVariant("grain", log.Axis == EnumAxis.X ? "we" : "ns"));
        world.BlockAccessor.SetBlock(grain.Id, blockSel.Position);
        return true;
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (byPlayer.InventoryManager.ActiveTool != EnumTool.Axe) return base.OnBlockInteractStart(world, byPlayer, blockSel);

        if (Row(world, byPlayer, blockSel.Position) == null)
        {
            (world.Api as ICoreClientAPI)?.TriggerIngameError(this, "shipyard-wedge-each-log", Lang.Get("roughseas:shipyard-wedge-each-log", LogRow.Length));
            return false;
        }
        work.Start(byPlayer.Entity);
        return true;
    }

    public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (world.Rand.NextDouble() < 0.08) world.PlaySoundAt(new AssetLocation($"game:sounds/block/chop{1 + world.Rand.Next(3)}"), blockSel.Position, 0, byPlayer);
        return work.Step(secondsUsed, seconds);
    }

    public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (!work.Stop(byPlayer.Entity, secondsUsed, seconds) || world.Side != EnumAppSide.Server) return;
        if (Row(world, byPlayer, blockSel.Position) is { } row) Split(world, byPlayer, row);
    }

    public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
    {
        work.Stop(byPlayer.Entity);
        return true;
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer) =>
        ObjectCacheUtil.GetOrCreate(world.Api, $"{ClassName}/help", () => new[]
        {
            new WorldInteraction
            {
                ActionLangCode = "roughseas:shipyard-split",
                MouseButton = EnumMouseButton.Right,
                Itemstacks = [.. world.Items.Where(item => item.Tool == EnumTool.Axe && item.Code != null).Select(item => new ItemStack(item))]
            }
        }).Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));

    private LogRow? Row(IWorldAccessor world, IPlayer byPlayer, BlockPos wedge)
    {
        var accessor = world.BlockAccessor;
        var row = LogRow.Through(wedge.DownCopy(), at => accessor.GetBlock(at.UpCopy()) is BlockWedge ? LyingLog.Of(accessor.GetBlock(at)) : null);
        return row != null && LandClaims.MayBreak(byPlayer.Entity, row.Blocks.SelectMany(log => new[] { log, log.UpCopy() })) ? row : null;
    }

    private void Split(IWorldAccessor world, IPlayer byPlayer, LogRow row)
    {
        var shipyard = world.Api.ModLoader.GetModSystem<ShipyardModSystem>();
        var lath = world.GetItem(new AssetLocation(lathCode.Replace("{wood}", row.Wood.Code)));
        if (lath == null)
        {
            shipyard.Log("no laths of {0} to split a row into", row.Wood.Code);
            return;
        }

        foreach (var log in row.Blocks)
        {
            world.BlockAccessor.SetBlock(0, log.UpCopy());
            world.BlockAccessor.SetBlock(0, log);
            foreach (var wedge in GetDrops(world, log.UpCopy(), byPlayer)) world.SpawnItemEntity(wedge, log.ToVec3d().Add(0.5, 0.5, 0.5));
        }
        var at = row.Centre.ToVec3d().Add(0.5, 0.5, 0.5);
        world.SpawnItemEntity(new ItemStack(lath, laths), at);
        world.SpawnCubeParticles(at, new ItemStack(lath), 2, 32, 0.8f);
        world.PlaySoundAt(SplitSound, at.X, at.Y, at.Z, byPlayer);
        byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack?.Collectible.DamageItem(world, byPlayer.Entity, byPlayer.InventoryManager.ActiveHotbarSlot);
        shipyard.Log("{0} splits a row of {1} at {2} {3} into {4} laths", byPlayer.PlayerName, row.Wood.Code, row.Centre.X, row.Centre.Z, laths);
    }
}
