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
        FramingWork.Rope => new RopeLaying(new AssetLocation(config.Rope)),
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

    public abstract void Spend(ItemSlot slot);

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

    public override void Spend(ItemSlot slot) => slot.TakeOut(1);

    public override IEnumerable<ItemStack> Shown(IWorldAccessor world) =>
        world.SearchItems(laths).Where(lath => lath.Variant["state"] == Wet).Select(lath => new ItemStack(lath));
}

/// <summary>A rope, its length the lashing that binds the bark to her gunwales and stems.</summary>
public sealed class RopeLaying(AssetLocation rope) : Laying
{
    public override string Action => "roughseas:shipyard-lash";

    public override bool Takes(ItemSlot slot) => slot.Itemstack?.Collectible.Code == rope;

    public override void Spend(ItemSlot slot) => slot.TakeOut(1);

    public override IEnumerable<ItemStack> Shown(IWorldAccessor world) =>
        world.GetItem(rope) is { } item ? [new ItemStack(item)] : [];
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

    public override void Spend(ItemSlot slot)
    {
        var container = (BlockLiquidContainerBase)slot.Itemstack!.Collectible;
        var props = container.GetContentProps(slot.Itemstack)!;
        container.TryTakeContent(slot.Itemstack, (int)Math.Round(litres * props.ItemsPerLitre));
    }

    public override IEnumerable<ItemStack> Shown(IWorldAccessor world) =>
        world.SearchItems(pitch).Where(glue => glue.Variant["state"] == Hot).Select(glue => new ItemStack(glue));

    private ItemStack? Pitch(ItemSlot slot) =>
        slot.Itemstack?.Collectible is BlockLiquidContainerBase container && container.GetContent(slot.Itemstack) is { } content
        && WildcardUtil.Match(pitch, content.Collectible.Code) ? content : null;
}
