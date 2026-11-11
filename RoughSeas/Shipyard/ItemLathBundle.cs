using System.Linq;
using System.Text;
using RoughSeas.Vessels;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace RoughSeas.Shipyard;

public class ItemLathBundle : ItemOfWood
{
    public new const string ClassName = "roughseas:ItemLathBundle";
    public const string WeightKey = "weight";
    private const string SoakedState = "soaked";
    private const string LathPart = "lath";

    public bool Soaked => Variant["state"] == SoakedState;

    public AssetLocation EntityCode => Code.CopyWithPath(CodeWithoutParts(1));

    public static ItemStack? WeightOf(ItemStack bundle, IWorldAccessor world) => bundle.Attributes.GetItemstack(WeightKey) is { } weight && weight.ResolveBlockOrItem(world) ? weight : null;

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
    {
        if (blockSel == null || byEntity is not EntityPlayer { Player: { } player }) return;

        handHandling = EnumHandHandling.PreventDefaultAction;
        if (api.Side != EnumAppSide.Server) return;
        var at = blockSel.DidOffset ? blockSel.Position : blockSel.Position.AddCopy(blockSel.Face);
        if (!api.World.Claims.TryAccess(player, at, EnumBlockAccessFlags.BuildOrBreak)) return;
        if (api.World.GetEntityType(EntityCode) is not { } type || api.World.ClassRegistry.CreateEntity(type) is not EntityLathBundle bundle) return;

        bundle.Pos.SetPos(at.X + 0.5, at.Y, at.Z + 0.5);
        bundle.Pos.Yaw = byEntity.Pos.Yaw;
        bundle.Lay(WetHoursLeft(slot.Itemstack), WeightOf(slot.Itemstack, api.World));
        if (player.WorldData.CurrentGameMode != EnumGameMode.Creative)
        {
            slot.TakeOut(1);
            slot.MarkDirty();
        }
        api.World.SpawnEntity(bundle);
        Shipyard.Log(bundle, "laid by {0}{1}", byEntity.GetName(), Soaked ? ", already soaked" : "");
    }

    public override void OnCreatedByCrafting(ItemSlot[] allInputslots, ItemSlot outputSlot, IRecipeBase byRecipe)
    {
        base.OnCreatedByCrafting(allInputslots, outputSlot, byRecipe);
        if (outputSlot.Itemstack is not { } bundle) return;
        if (allInputslots.Select(slot => slot.Itemstack).FirstOrDefault(stack => stack != null && stack.Collectible.FirstCodePart() != LathPart) is not { } weight) return;

        var laid = weight.Clone();
        laid.StackSize = byRecipe.RecipeIngredients.FirstOrDefault(ingredient => ingredient.SatisfiesAsIngredient(weight, false))?.Quantity ?? 1;
        bundle.Attributes.SetItemstack(WeightKey, laid);
    }

    public override void OnConsumedByCrafting(ItemSlot[] allInputSlots, ItemSlot stackInSlot, IRecipeBase recipe, IRecipeIngredient fromIngredient, IPlayer byPlayer, int quantity)
    {
        var weight = stackInSlot.Itemstack is { } bundle ? WeightOf(bundle, api.World) : null;
        base.OnConsumedByCrafting(allInputSlots, stackInSlot, recipe, fromIngredient, byPlayer, quantity);
        if (weight == null) return;

        if (!byPlayer.InventoryManager.TryGiveItemstack(weight, true)) api.World.SpawnItemEntity(weight, byPlayer.Entity.Pos.XYZ);
    }

    public override ItemStack OnTransitionNow(ItemSlot slot, TransitionableProperties props)
    {
        var dried = base.OnTransitionNow(slot, props);
        if (slot.Itemstack is { } bundle && WeightOf(bundle, api.World) is { } weight) dried.Attributes.SetItemstack(WeightKey, weight);
        return dried;
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        if (inSlot.Itemstack is { } bundle && WeightOf(bundle, api.World) is { } weight) dsc.AppendLine(Lang.Get("roughseas:lathbundle-weight", weight.StackSize, weight.GetName()));
    }

    private double WetHoursLeft(ItemStack bundle) =>
        Soaked ? UpdateAndGetTransitionState(api.World, new DummySlot(bundle.Clone()), EnumTransitionType.Dry)?.FreshHoursLeft ?? 0 : 0;

    private ShipyardModSystem Shipyard => api.ModLoader.GetModSystem<ShipyardModSystem>();
}
