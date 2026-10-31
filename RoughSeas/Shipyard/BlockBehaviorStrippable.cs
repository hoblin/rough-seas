using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace RoughSeas.Shipyard;

/// <summary>
/// A standing trunk whose bark a knife takes off as one sheet: the sheet is the canoe at the start of her build, and
/// the trunk stands on with its wood bare.
/// </summary>
public class BlockBehaviorStrippable(Block block) : BlockBehavior(block)
{
    public const string Code = "roughseas:strippable";

    private static readonly AssetLocation PeelSound = new("game:sounds/block/leafy-picking");

    private readonly HeldWork work = new();
    private string vessel = "";
    private AssetLocation debarked = null!;
    private double seconds;
    private int trunkBlocks;

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);
        vessel = properties["vessel"].AsString("");
        debarked = new AssetLocation(properties["debarked"].AsString(""));
        seconds = properties["seconds"].AsDouble();
        trunkBlocks = properties["trunkBlocks"].AsInt();
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
    {
        if (byPlayer.InventoryManager.ActiveTool != EnumTool.Knife) return false;

        handling = EnumHandling.PreventDefault;
        if (Strippable(world, byPlayer, blockSel.Position) == null) return false;

        work.Start(byPlayer.Entity);
        return true;
    }

    public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
    {
        handling = EnumHandling.PreventDefault;
        if (world.Rand.NextDouble() < 0.05) world.PlaySoundAt(PeelSound, blockSel.Position, 0, byPlayer);
        return work.Step(secondsUsed, seconds);
    }

    public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
    {
        handling = EnumHandling.PreventDefault;
        if (!work.Stop(byPlayer.Entity, secondsUsed, seconds) || world.Side != EnumAppSide.Server) return;
        if (Strippable(world, byPlayer, blockSel.Position) is { } trunk) Strip(world, byPlayer, trunk);
    }

    public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
    {
        work.Stop(byPlayer.Entity);
        return true;
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer, ref EnumHandling handling) =>
        ObjectCacheUtil.GetOrCreate(world.Api, $"{Code}/help", () => new[]
        {
            new WorldInteraction
            {
                ActionLangCode = "roughseas:shipyard-strip",
                MouseButton = EnumMouseButton.Right,
                Itemstacks = [.. world.Items.Where(item => item.Tool == EnumTool.Knife && item.Code != null).Select(item => new ItemStack(item))]
            }
        });

    private Trunk? Strippable(IWorldAccessor world, IPlayer byPlayer, BlockPos pos)
    {
        var trunk = Trunk.Through(pos, at => world.BlockAccessor.GetBlock(at).Id == block.Id);
        if (trunk.Height < trunkBlocks)
        {
            (world.Api as ICoreClientAPI)?.TriggerIngameError(this, "shipyard-trunk-short", Lang.Get("roughseas:shipyard-trunk-short", trunkBlocks));
            return null;
        }
        return LandClaims.MayBreak(byPlayer.Entity, trunk.Blocks) ? trunk : null;
    }

    private void Strip(IWorldAccessor world, IPlayer byPlayer, Trunk trunk)
    {
        var shipyard = world.Api.ModLoader.GetModSystem<ShipyardModSystem>();
        var type = world.GetEntityType(new AssetLocation(vessel));
        var bare = world.GetBlock(debarked);
        if (type == null || bare == null)
        {
            shipyard.Log("no {0} or {1} to strip a trunk into", vessel, debarked);
            return;
        }

        foreach (var pos in trunk.Blocks) world.BlockAccessor.ExchangeBlock(bare.Id, pos);
        var toward = BlockFacing.HorizontalFromYaw(byPlayer.Entity.Pos.Yaw + GameMath.PI);
        var sheet = world.ClassRegistry.CreateEntity(type);
        sheet.Pos.SetPos(trunk.Foot.AddCopy(toward).ToVec3d().Add(0.5, 0, 0.5));
        sheet.Pos.Yaw = toward.Axis == EnumAxis.X ? 0 : GameMath.PIHALF;
        EntityBehaviorFraming.Begin(sheet);
        world.SpawnEntity(sheet);
        byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack?.Collectible.DamageItem(world, byPlayer.Entity, byPlayer.InventoryManager.ActiveHotbarSlot);
        shipyard.Log(sheet, "stripped by {0} off a trunk of {1} blocks", byPlayer.PlayerName, trunk.Height);
    }
}
