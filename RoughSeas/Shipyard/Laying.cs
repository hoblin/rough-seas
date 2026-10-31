using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace RoughSeas.Shipyard;

/// <summary>What a framing stage's pieces are laid with, and how one is taken from the hand.</summary>
public abstract class Laying
{
    public static Laying For(FramingStage stage, FramingConfig config) => stage.Work switch
    {
        FramingWork.Lath => new LathLaying(new AssetLocation(config.Laths)),
        FramingWork.Pole => new PoleLaying(new AssetLocation(config.Poles)),
        FramingWork.Rope => new RopeLaying(new AssetLocation(config.Rope)),
        FramingWork.Hew => new HewLaying(),
        FramingWork.Cloth => new PieceLaying(new AssetLocation(config.Cloth), "roughseas:shipyard-bend-sail"),
        FramingWork.Plank => new PieceLaying(new AssetLocation(config.Planks), "roughseas:shipyard-fit-plank"),
        _ => new PitchLaying(new AssetLocation(config.Pitch), stage.Litres)
    };

    /// <summary>The lang key of the help that offers this work.</summary>
    public abstract string Action { get; }

    public abstract bool Takes(ItemSlot slot);

    /// <summary>The lang key saying why this piece can't go on her now, or null.</summary>
    public virtual string? Refusal(ItemSlot slot, Framing framing) => null;

    /// <summary>The wood this piece is of, when it is a lath.</summary>
    public virtual string? WoodOf(ItemSlot slot) => null;

    /// <summary>How long this piece stays wet once laid, in game hours.</summary>
    public virtual double WetHours(IWorldAccessor world, ItemSlot slot) => 0;

    public abstract void Spend(ItemSlot slot, EntityAgent byEntity);

    /// <summary>What the help shows this work done with.</summary>
    public abstract IEnumerable<ItemStack> Shown(IWorldAccessor world);
}

/// <summary>A lath split from the row, bent into her wet.</summary>
public sealed class LathLaying(AssetLocation laths) : Laying
{
    private const string Wet = "wet";

    public override string Action => "roughseas:shipyard-lay-lath";

    public override bool Takes(ItemSlot slot) => slot.Itemstack?.Collectible is { } lath && WildcardUtil.Match(laths, lath.Code);

    public override string? Refusal(ItemSlot slot, Framing framing) =>
        slot.Itemstack!.Collectible.Variant["state"] != Wet ? "shipyard-soak-laths"
        : framing.Wood is { } wood && wood != WoodOf(slot) ? "shipyard-laths-one-wood"
        : null;

    public override string? WoodOf(ItemSlot slot) => slot.Itemstack?.Collectible.Variant["wood"];

    public override double WetHours(IWorldAccessor world, ItemSlot slot) =>
        slot.Itemstack!.Collectible.UpdateAndGetTransitionState(world, slot, EnumTransitionType.Dry)?.FreshHoursLeft ?? 0;

    public override void Spend(ItemSlot slot, EntityAgent byEntity) => slot.TakeOut(1);

    public override IEnumerable<ItemStack> Shown(IWorldAccessor world) =>
        world.SearchItems(laths).Where(lath => lath.Variant["state"] == Wet).Select(lath => new ItemStack(lath));
}

public sealed class PoleLaying(AssetLocation poles) : Laying
{
    public override string Action => "roughseas:shipyard-lay-pole";

    public override bool Takes(ItemSlot slot) => slot.Itemstack?.Collectible is { } pole && WildcardUtil.Match(poles, pole.Code);

    public override string? Refusal(ItemSlot slot, Framing framing) =>
        framing.Wood is { } wood && wood != WoodOf(slot) ? "shipyard-poles-one-wood" : null;

    public override string? WoodOf(ItemSlot slot) => slot.Itemstack?.Collectible.Variant["wood"];

    public override void Spend(ItemSlot slot, EntityAgent byEntity) => slot.TakeOut(1);

    public override IEnumerable<ItemStack> Shown(IWorldAccessor world) => world.SearchItems(poles).Select(pole => new ItemStack(pole));
}

public sealed class HewLaying : Laying
{
    public override string Action => "roughseas:shipyard-hew-float";

    public override bool Takes(ItemSlot slot) => slot.Itemstack?.Collectible.Tool == EnumTool.Axe;

    public override void Spend(ItemSlot slot, EntityAgent byEntity) => slot.Itemstack!.Collectible.DamageItem(byEntity.World, byEntity, slot);

    public override IEnumerable<ItemStack> Shown(IWorldAccessor world) =>
        world.Items.Where(item => item.Code != null && item.Tool == EnumTool.Axe).Select(axe => new ItemStack(axe));
}

public sealed class PieceLaying(AssetLocation pieces, string action) : Laying
{
    public override string Action => action;

    public override bool Takes(ItemSlot slot) => slot.Itemstack?.Collectible is { } piece && WildcardUtil.Match(pieces, piece.Code);

    public override void Spend(ItemSlot slot, EntityAgent byEntity) => slot.TakeOut(1);

    public override IEnumerable<ItemStack> Shown(IWorldAccessor world) => world.SearchItems(pieces).Select(piece => new ItemStack(piece));
}

public sealed class RopeLaying(AssetLocation rope) : Laying
{
    public override string Action => "roughseas:shipyard-lash";

    private const string TiedKey = "clothId";

    public override bool Takes(ItemSlot slot) =>
        slot.Itemstack is { } stack && WildcardUtil.Match(rope, stack.Collectible.Code) && stack.Attributes.GetInt(TiedKey) == 0;

    public override void Spend(ItemSlot slot, EntityAgent byEntity) => slot.TakeOut(1);

    public override IEnumerable<ItemStack> Shown(IWorldAccessor world) =>
        world.SearchItems(rope).Select(item => new ItemStack(item));
}

/// <summary>Hot pitch from a container, poured along one seam.</summary>
public sealed class PitchLaying(AssetLocation pitch, double litres) : Laying
{
    private const string Hot = "hot";

    public override string Action => "roughseas:shipyard-pitch";

    public override bool Takes(ItemSlot slot) => Pitch(slot) != null;

    public override string? Refusal(ItemSlot slot, Framing framing)
    {
        var content = Pitch(slot)!;
        if (content.Collectible.Variant["state"] != Hot) return "shipyard-pitch-cold";

        var container = (BlockLiquidContainerBase)slot.Itemstack!.Collectible;
        return container.GetCurrentLitres(slot.Itemstack) < litres - 1e-6 ? "shipyard-pitch-short" : null;
    }

    public override void Spend(ItemSlot slot, EntityAgent byEntity)
    {
        var container = (BlockLiquidContainerBase)slot.Itemstack!.Collectible;
        var portions = (int)Math.Round(litres * container.GetContentProps(slot.Itemstack)!.ItemsPerLitre);
        container.SplitStackAndPerformAction(byEntity, slot, stack => container.TryTakeContent(stack, portions)?.StackSize ?? 0);
    }

    public override IEnumerable<ItemStack> Shown(IWorldAccessor world) =>
        world.SearchItems(pitch).Where(glue => glue.Variant["state"] == Hot).Select(glue => new ItemStack(glue));

    private ItemStack? Pitch(ItemSlot slot) =>
        slot.Itemstack?.Collectible is BlockLiquidContainerBase container && container.GetContent(slot.Itemstack) is { } content
        && WildcardUtil.Match(pitch, content.Collectible.Code) ? content : null;
}
