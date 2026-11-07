using RoughSeas.SeaAndWeather;
using RoughSeas.Vessels;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Shipyard;

/// <summary>A pile of laths on the ground, which rope binds into a hide boat's blank where it lies when it holds exactly the laths her build starts from.</summary>
public sealed class LathPile(BlockEntityGroundStorage pile, LathBinding binding) : IWorkedHull
{
    private const float Lengthwise = GameMath.PIHALF;

    private ItemSlot Laths => pile.Inventory[0];

    public static LathPile? At(IWorldAccessor world, BlockPos pos) =>
        world.BlockAccessor.GetBlockEntity(pos) is BlockEntityGroundStorage pile && pile.Inventory[0].Itemstack is { } laths
        && LathBinding.For(world, laths) is { } binding ? new LathPile(pile, binding) : null;

    public HullWork? WorkWith(ItemSlot slot) =>
        binding.Rope.Takes(slot) ? new(binding.Seconds, LathBinding.Wet(Laths.Itemstack!) ? null : "shipyard-soak-laths", binding.Rope.ShowsBar) : null;

    public void Lay(EntityAgent byEntity, ItemSlot slot)
    {
        var world = pile.Api.World;
        var laths = Laths.Itemstack!;
        if (!LathBinding.Wet(laths) || Wood.Of(laths.Collectible.Variant) is not { } wood || !LandClaims.MayBreak(byEntity, [pile.Pos])) return;

        double wetHours = laths.Collectible.UpdateAndGetTransitionState(world, Laths, EnumTransitionType.Dry)?.FreshHoursLeft ?? 0;
        double hours = world.Api.ModLoader.GetModSystem<SeaWeatherSystem>().Clock.Hours;
        var vessel = binding.Bind(world, pile.Pos.ToVec3d().Add(0.5, 0, 0.5), pile.MeshAngle + Lengthwise, wood, hours + wetHours);
        world.BlockAccessor.SetBlock(0, pile.Pos);
        if ((byEntity as EntityPlayer)?.Player?.WorldData.CurrentGameMode != EnumGameMode.Creative) binding.Rope.Spend(slot, byEntity);
        slot.MarkDirty();
        world.PlaySoundAt(new AssetLocation("game:sounds/player/build"), vessel, null, true, 16);
        world.Api.ModLoader.GetModSystem<ShipyardModSystem>().Log(vessel, "bound by {0} from a pile of {1} {2} at {3}, turned {4:F2}, setting in {5:F0} h",
            byEntity.GetName(), laths.StackSize, laths.Collectible.Code, pile.Pos, pile.MeshAngle, wetHours);
    }
}
