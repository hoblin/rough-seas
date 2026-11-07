using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace RoughSeas.Shipyard;

/// <summary>
/// A standing trunk whose bark a knife takes off as one sheet: the sheet is the canoe at the start of her build, and
/// the trunk stands on with its wood bare.
/// </summary>
public class BlockBehaviorStrippable(Block block) : BlockBehavior(block), IWorkedBlock
{
    public const string Code = "roughseas:strippable";

    private const string TrunkShort = "shipyard-trunk-short";

    private static readonly AssetLocation PeelSound = new("game:sounds/block/leafy-picking");

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

    public IWorkedHull HullAt(IWorldAccessor world, BlockPos pos) => new Standing(this, world, pos);

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

    private Trunk Through(IWorldAccessor world, BlockPos pos) => Trunk.Through(pos, at => world.BlockAccessor.GetBlock(at).Id == block.Id);

    private void Strip(IWorldAccessor world, EntityAgent byEntity, ItemSlot slot, Trunk trunk)
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
        var toward = BlockFacing.HorizontalFromYaw(byEntity.Pos.Yaw + GameMath.PI);
        var sheet = world.ClassRegistry.CreateEntity(type);
        sheet.Pos.SetPos(trunk.Foot.AddCopy(toward).ToVec3d().Add(0.5, 0, 0.5));
        sheet.Pos.Yaw = toward.Axis == EnumAxis.X ? 0 : GameMath.PIHALF;
        EntityBehaviorFraming.Begin(sheet);
        world.SpawnEntity(sheet);
        slot.Itemstack?.Collectible.DamageItem(world, byEntity, slot);
        shipyard.Log(sheet, "stripped by {0} off a trunk of {1} blocks", byEntity.GetName(), trunk.Height);
    }

    private sealed class Standing(BlockBehaviorStrippable strippable, IWorldAccessor world, BlockPos pos) : IWorkedHull
    {
        public HullWork? WorkWith(ItemSlot slot) =>
            slot.Itemstack?.Collectible.Tool != EnumTool.Knife ? null
            : strippable.Through(world, pos).Height < strippable.trunkBlocks ? new(strippable.seconds, TrunkShort, ShowsBar: true) { RefusalArgs = [strippable.trunkBlocks] }
            : new(strippable.seconds, ShowsBar: true);

        public void Working(EntityAgent byEntity)
        {
            if (world.Rand.NextDouble() < 0.05) world.PlaySoundAt(PeelSound, pos, 0, (byEntity as EntityPlayer)?.Player);
        }

        public void Lay(EntityAgent byEntity, ItemSlot slot)
        {
            if (WorkWith(slot) is not { Refusal: null }) return;

            var trunk = strippable.Through(world, pos);
            if (!LandClaims.MayBreak(byEntity, trunk.Blocks))
            {
                world.Api.ModLoader.GetModSystem<ShipyardModSystem>().Log("trunk at {0} not stripped: {1} may not break blocks on this claim", pos, byEntity.GetName());
                return;
            }

            strippable.Strip(world, byEntity, slot, trunk);
        }
    }
}
