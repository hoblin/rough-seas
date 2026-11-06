using System.Linq;
using RoughSeas.Vessels;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace RoughSeas.Shipyard;

/// <summary>
/// A wedge set in a lying log, driven deeper by each blow of an axe's poll. With one on each log of a row lying end to
/// end, the trunk splits along its grain into poles as long as the row once the last is driven home.
/// </summary>
public class BlockWedge : Block
{
    public const string ClassName = "roughseas:BlockWedge";

    private const string Grain = "grain";
    private const string Depth = "driven";
    private const int ChipsPerBlow = 4;

    private static readonly AssetLocation SplitSound = new("game:sounds/effect/treefell");

    private double secondsPerBlow;
    private WedgeSplit[] splits = [];

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        secondsPerBlow = Attributes["secondsPerBlow"].AsDouble();
        splits = Attributes["splits"].AsObject<WedgeSplit[]>([]);
    }

    public IStruck? At(IWorldAccessor world, BlockPos pos) => Deeper(world) is { } deeper ? new Driving(this, deeper, pos) : null;

    public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref string failureCode)
    {
        if (LyingLog.Of(world.BlockAccessor.GetBlock(blockSel.Position.DownCopy())) != null) return base.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode);

        failureCode = "roughseas-wedge-on-log";
        return false;
    }

    public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
    {
        var log = LyingLog.Of(world.BlockAccessor.GetBlock(blockSel.Position.DownCopy()))!;
        world.BlockAccessor.SetBlock(world.GetBlock(CodeWithVariants([Grain, Depth], [log.Axis == EnumAxis.X ? "we" : "ns", "0"])).Id, blockSel.Position);
        return true;
    }

    public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos) => new(world.GetBlock(CodeWithVariants([Grain, Depth], ["we", "0"])));

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer) =>
        (Deeper(world) == null ? [] : ObjectCacheUtil.GetOrCreate(world.Api, $"{ClassName}/help", () => new[]
        {
            new WorldInteraction
            {
                ActionLangCode = "roughseas:shipyard-drive",
                MouseButton = EnumMouseButton.Right,
                Itemstacks = [.. world.Items.Where(item => item.Tool == EnumTool.Axe && item.Code != null).Select(item => new ItemStack(item))]
            }
        })).Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));

    private BlockWedge? Deeper(IWorldAccessor world) => world.GetBlock(CodeWithVariant(Depth, (int.Parse(Variant[Depth]) + 1).ToString())) as BlockWedge;

    private bool TakeUp(EntityAgent byEntity, BlockPos pos)
    {
        if (Row(byEntity, pos) != null) return true;

        var lengths = splits.Select(split => split.Logs).Order().ToArray();
        (byEntity.Api as ICoreClientAPI)?.TriggerIngameError(this, "shipyard-wedge-each-log", Lang.Get("roughseas:shipyard-wedge-each-log", lengths.First(), lengths.Last()));
        return false;
    }

    private bool Strike(EntityAgent byEntity, ItemSlot axe, BlockWedge deeper, BlockPos pos)
    {
        var world = byEntity.World;
        world.BlockAccessor.ExchangeBlock(deeper.Id, pos);
        world.SpawnCubeParticles(pos.ToVec3d().Add(0.5, 0, 0.5), new ItemStack(world.BlockAccessor.GetBlock(pos.DownCopy())), 0.3f, ChipsPerBlow, 0.6f);
        var shipyard = world.Api.ModLoader.GetModSystem<ShipyardModSystem>();
        shipyard.Log("{0} drives the wedge at {1} {2} {3} to depth {4}", byEntity.GetName(), pos.X, pos.Y, pos.Z, deeper.Variant[Depth]);
        if (deeper.Deeper(world) != null) return true;

        if (Row(byEntity, pos) is { } row && row.Blocks.All(log => world.BlockAccessor.GetBlock(log.UpCopy()) is BlockWedge wedge && wedge.Deeper(world) == null))
        {
            Split(byEntity, axe, row);
        }
        return false;
    }

    private LogRow? Row(EntityAgent byEntity, BlockPos wedge)
    {
        var accessor = byEntity.World.BlockAccessor;
        var row = LogRow.Run(wedge.DownCopy(), at => accessor.GetBlock(at.UpCopy()) is BlockWedge ? LyingLog.Of(accessor.GetBlock(at)) : null, splits.Max(split => split.Logs));
        return row != null && splits.Any(split => split.Logs == row.Length)
            && LandClaims.MayBreak(byEntity, row.Blocks.SelectMany(log => new[] { log, log.UpCopy() })) ? row : null;
    }

    private void Split(EntityAgent byEntity, ItemSlot axe, LogRow row)
    {
        var world = byEntity.World;
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
            foreach (var wedge in GetDrops(world, log.UpCopy(), (byEntity as EntityPlayer)?.Player)) world.SpawnItemEntity(wedge, log.ToVec3d().Add(0.5, 0.5, 0.5));
        }
        var at = row.Centre.ToVec3d().Add(0.5, 0.5, 0.5);
        world.SpawnItemEntity(new ItemStack(piece, split.Count), at);
        world.SpawnCubeParticles(at, new ItemStack(piece), 2, 32, 0.8f);
        world.PlaySoundAt(SplitSound, at.X, at.Y, at.Z);
        axe.Itemstack?.Collectible.DamageItem(world, byEntity, axe);
        shipyard.Log("{0} splits a row of {1} {2} at {3} {4} into {5} {6}", byEntity.GetName(), row.Length, row.Wood.Code, row.Centre.X, row.Centre.Z,
            split.Count, piece.Code);
    }

    private sealed class Driving(BlockWedge wedge, BlockWedge deeper, BlockPos pos) : IStruck
    {
        public double SecondsPerBlow => wedge.secondsPerBlow;

        public bool TakeUp(EntityAgent byEntity) => wedge.TakeUp(byEntity, pos);

        public bool Strike(EntityAgent byEntity, ItemSlot tool) => wedge.Strike(byEntity, tool, deeper, pos);
    }
}

public sealed record WedgeSplit(int Logs, string Item, int Count);
