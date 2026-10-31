using System.Linq;
using RoughSeas.Vessels;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace RoughSeas.Shipyard;

/// <summary>
/// A wedge driven into a lying log. With one on each log of a row lying end to end, the axe drives them all, and the
/// trunk splits along its grain into what a trunk of that length gives: five logs into a bundle of laths, three into
/// a set of poles.
/// </summary>
public class BlockWedge : Block
{
    public const string ClassName = "roughseas:BlockWedge";

    private static readonly AssetLocation SplitSound = new("game:sounds/effect/treefell");

    private readonly HeldWork work = new();
    private double seconds;
    private WedgeSplit[] splits = [];

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        seconds = Attributes["splitSeconds"].AsDouble();
        splits = Attributes["splits"].AsObject<WedgeSplit[]>([]);
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
            var lengths = splits.Select(split => split.Logs).Order().ToArray();
            (world.Api as ICoreClientAPI)?.TriggerIngameError(this, "shipyard-wedge-each-log", Lang.Get("roughseas:shipyard-wedge-each-log", lengths.First(), lengths.Last()));
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
        var row = LogRow.Run(wedge.DownCopy(), at => accessor.GetBlock(at.UpCopy()) is BlockWedge ? LyingLog.Of(accessor.GetBlock(at)) : null);
        return row != null && splits.Any(split => split.Logs == row.Length)
            && LandClaims.MayBreak(byPlayer.Entity, row.Blocks.SelectMany(log => new[] { log, log.UpCopy() })) ? row : null;
    }

    private void Split(IWorldAccessor world, IPlayer byPlayer, LogRow row)
    {
        var shipyard = world.Api.ModLoader.GetModSystem<ShipyardModSystem>();
        var split = splits.First(split => split.Logs == row.Length);
        var piece = world.GetItem(new AssetLocation(split.Item.Replace("{wood}", row.Wood.Code)));
        if (piece == null)
        {
            shipyard.Log("no {0} of {1} to split a row into", split.Item, row.Wood.Code);
            return;
        }

        foreach (var log in row.Blocks)
        {
            world.BlockAccessor.SetBlock(0, log.UpCopy());
            world.BlockAccessor.SetBlock(0, log);
            foreach (var wedge in GetDrops(world, log.UpCopy(), byPlayer)) world.SpawnItemEntity(wedge, log.ToVec3d().Add(0.5, 0.5, 0.5));
        }
        var at = row.Centre.ToVec3d().Add(0.5, 0.5, 0.5);
        world.SpawnItemEntity(new ItemStack(piece, split.Count), at);
        world.SpawnCubeParticles(at, new ItemStack(piece), 2, 32, 0.8f);
        world.PlaySoundAt(SplitSound, at.X, at.Y, at.Z, byPlayer);
        byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack?.Collectible.DamageItem(world, byPlayer.Entity, byPlayer.InventoryManager.ActiveHotbarSlot);
        shipyard.Log("{0} splits a row of {1} {2} at {3} {4} into {5} {6}", byPlayer.PlayerName, row.Length, row.Wood.Code, row.Centre.X, row.Centre.Z,
            split.Count, piece.Code);
    }
}

public sealed record WedgeSplit(int Logs, string Item, int Count);
