using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RoughSeas.Vessels;
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
        FramingWork.Hew or FramingWork.Cut => new ToolLaying(stage.Work),
        FramingWork.Cloth => new PieceLaying(new AssetLocation(config.Cloth), "roughseas:shipyard-bend-sail"),
        FramingWork.Plank => new PlankLaying(new AssetLocation(config.Planks)),
        FramingWork.Hide => new HideLaying(new AssetLocation(config.Hides), config.HideStates),
        FramingWork.Oar => new OarLaying(new AssetLocation(config.Oars)),
        _ => new PitchLaying(new AssetLocation(config.Pitch), stage.Litres)
    };

    /// <summary>The lang key of the help that offers this work.</summary>
    public abstract string Action { get; }

    public abstract bool Takes(ItemSlot slot);

    /// <summary>The lang key saying why this piece can't go on her now, or null.</summary>
    public virtual string? Refusal(ItemSlot slot, Framing framing) => null;

    /// <summary>The wood this piece is of, when it is of one.</summary>
    public virtual string? WoodOf(ItemSlot slot) => null;

    /// <summary>How long this piece stays wet once laid, in game hours.</summary>
    public virtual double WetHours(IWorldAccessor world, ItemSlot slot) => 0;

    public abstract void Spend(ItemSlot slot, EntityAgent byEntity);

    /// <summary>Takes the piece, of which the work needs only <paramref name="used"/> of its <see cref="Pieces"/>.</summary>
    public virtual void Spend(ItemSlot slot, EntityAgent byEntity, int used) => Spend(slot, byEntity);

    public virtual int Pieces(ItemSlot slot) => 1;

    /// <summary>Whether a hold of this work shows vanilla's progress bar: work whose piece shows nothing on her until it is done.</summary>
    public virtual bool ShowsBar => false;

    public virtual string Hands => WorkHands.Work;

    /// <summary>What the help shows this work done with.</summary>
    public abstract IEnumerable<ItemStack> Shown(IWorldAccessor world);

    /// <summary>Whether the lath or pole in <paramref name="slot"/> is shorter than the piece it would make; a longer one is cut down to it.</summary>
    protected static bool Short(ItemSlot slot, Framing framing) => ShorterThan(slot, framing.NextMetres);

    public static bool ShorterThan(ItemSlot slot, double metres) =>
        double.TryParse(slot.Itemstack?.Collectible.Variant["length"], NumberStyles.Float, CultureInfo.InvariantCulture, out var length)
        && length < metres;
}

/// <summary>A lath split from a pole, bent into her wet.</summary>
public sealed class LathLaying(AssetLocation laths) : Laying
{
    private const string Wet = "wet";

    public override string Action => "roughseas:shipyard-lay-lath";

    public override bool Takes(ItemSlot slot) => slot.Itemstack?.Collectible is { } lath && WildcardUtil.Match(laths, lath.Code);

    public override string? Refusal(ItemSlot slot, Framing framing) =>
        slot.Itemstack!.Collectible.Variant["state"] != Wet ? "shipyard-soak-laths"
        : framing.Wood is { } wood && wood != WoodOf(slot) ? "shipyard-laths-one-wood"
        : Short(slot, framing) ? "shipyard-lath-short"
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
        framing.Wood is { } wood && wood != WoodOf(slot) ? "shipyard-poles-one-wood"
        : Short(slot, framing) ? "shipyard-pole-short"
        : null;

    public override string? WoodOf(ItemSlot slot) => slot.Itemstack?.Collectible.Variant["wood"];

    public override void Spend(ItemSlot slot, EntityAgent byEntity) => slot.TakeOut(1);

    public override IEnumerable<ItemStack> Shown(IWorldAccessor world) => world.SearchItems(poles).Select(pole => new ItemStack(pole));
}

/// <summary>Work done with a tool held on her, which wears it as vanilla wears a tool for each job: an axe hewing, a knife cutting bark.</summary>
public sealed class ToolLaying(FramingWork work) : Laying
{
    private readonly EnumTool tool = ToolOf(work)!.Value;

    /// <summary>The tool a kind of work is done with; none for work that lays a piece.</summary>
    public static EnumTool? ToolOf(FramingWork work) => work switch
    {
        FramingWork.Hew => EnumTool.Axe,
        FramingWork.Cut => EnumTool.Knife,
        _ => null
    };

    public override string Action => tool == EnumTool.Axe ? "roughseas:shipyard-hew-float" : "roughseas:shipyard-cut-bark";

    public override bool Takes(ItemSlot slot) => slot.Itemstack?.Collectible.Tool == tool;

    public override void Spend(ItemSlot slot, EntityAgent byEntity) => slot.Itemstack!.Collectible.DamageItem(byEntity.World, byEntity, slot);

    public override IEnumerable<ItemStack> Shown(IWorldAccessor world) =>
        world.Items.Where(item => item.Code != null && item.Tool == tool).Select(held => new ItemStack(held));
}

public sealed class PieceLaying(AssetLocation pieces, string action) : Laying
{
    public override string Action => action;

    public override bool Takes(ItemSlot slot) => slot.Itemstack?.Collectible is { } piece && WildcardUtil.Match(pieces, piece.Code);

    public override void Spend(ItemSlot slot, EntityAgent byEntity) => slot.TakeOut(1);

    public override IEnumerable<ItemStack> Shown(IWorldAccessor world) => world.SearchItems(pieces).Select(piece => new ItemStack(piece));
}

public sealed class OarLaying(AssetLocation oars) : Laying
{
    public override string Action => "roughseas:shipyard-ship-oar";

    public override bool Takes(ItemSlot slot) => slot.Itemstack?.Collectible is { } oar && WildcardUtil.Match(oars, oar.Code);

    public override string? WoodOf(ItemSlot slot) => slot.Itemstack?.Collectible.Variant[Wood.VariantCode];

    public override void Spend(ItemSlot slot, EntityAgent byEntity) => slot.TakeOut(1);

    public override IEnumerable<ItemStack> Shown(IWorldAccessor world) => world.SearchItems(oars).Select(oar => new ItemStack(oar));
}

public sealed class PlankLaying(AssetLocation planks) : Laying
{
    public override string Action => "roughseas:shipyard-fit-plank";

    public override bool Takes(ItemSlot slot) => slot.Itemstack?.Collectible is { } plank && WildcardUtil.Match(planks, plank.Code);

    public override string? Refusal(ItemSlot slot, Framing framing) =>
        framing.Current is { } stage && framing.WoodOf(stage) is { } wood && wood != WoodOf(slot) ? "shipyard-planks-one-wood" : null;

    public override string? WoodOf(ItemSlot slot) =>
        slot.Itemstack?.Collectible is { } plank && plank.Variant[Wood.VariantCode] is { } species ? new Wood(plank.Code.Domain, species).Code : null;

    public override void Spend(ItemSlot slot, EntityAgent byEntity) => slot.TakeOut(1);

    public override IEnumerable<ItemStack> Shown(IWorldAccessor world) => world.SearchItems(planks).Select(plank => new ItemStack(plank));
}

public sealed class RopeLaying(AssetLocation rope) : Laying
{
    public override string Action => "roughseas:shipyard-lash";

    public override bool ShowsBar => true;

    public override string Hands => WorkHands.Tie;

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
        var held = Pitch(slot)!;
        if (held.Content.Collectible.Variant["state"] != Hot) return "shipyard-pitch-cold";

        return held.Litres < litres - 1e-6 ? "shipyard-pitch-short" : null;
    }

    public override void Spend(ItemSlot slot, EntityAgent byEntity) => Pitch(slot)!.Pour(litres, byEntity);

    public override IEnumerable<ItemStack> Shown(IWorldAccessor world) =>
        world.SearchItems(pitch).Where(glue => glue.Variant["state"] == Hot).Select(glue => new ItemStack(glue));

    private HeldPitch? Pitch(ItemSlot slot) =>
        HeldPitch.Of(slot) is { } held && WildcardUtil.Match(pitch, held.Content.Collectible.Code) ? held : null;
}

/// <summary>Pitch in the container held in a slot: a bowl, jug or bucket, or the cooking pot it was cooked in.</summary>
public abstract class HeldPitch(ItemSlot slot, ItemStack content)
{
    public ItemStack Content => content;

    public abstract double Litres { get; }

    public abstract void Pour(double litres, EntityAgent byEntity);

    protected ItemSlot Slot => slot;

    protected static float ItemsPerLitre(ItemStack content) => BlockLiquidContainerBase.GetContainableProps(content)?.ItemsPerLitre ?? 100;

    public static HeldPitch? Of(ItemSlot slot) => slot.Itemstack is not { } stack ? null : stack.Collectible switch
    {
        BlockLiquidContainerBase container when container.GetContent(stack) is { } content => new InContainer(slot, container, content),
        IBlockMealContainer pot when slot.Inventory?.Api?.World is { } world && pot.GetNonEmptyContents(world, stack) is [var content] => new InPot(slot, world, pot, content),
        _ => null
    };

    private sealed class InContainer(ItemSlot slot, BlockLiquidContainerBase container, ItemStack content) : HeldPitch(slot, content)
    {
        public override double Litres => container.GetCurrentLitres(Slot.Itemstack);

        public override void Pour(double litres, EntityAgent byEntity)
        {
            var portions = (int)Math.Round(litres * ItemsPerLitre(Content));
            container.SplitStackAndPerformAction(byEntity, Slot, stack => container.TryTakeContent(stack, portions)?.StackSize ?? 0);
        }
    }

    private sealed class InPot(ItemSlot slot, IWorldAccessor world, IBlockMealContainer pot, ItemStack content) : HeldPitch(slot, content)
    {
        private const string EmptiedKey = "emptiedBlockCode";

        private double ServingLitres => Content.StackSize / ItemsPerLitre(Content);

        public override double Litres => pot.GetQuantityServings(world, Slot.Itemstack!) * ServingLitres;

        public override void Pour(double litres, EntityAgent byEntity)
        {
            var stack = Slot.Itemstack!;
            var left = (float)Math.Round(pot.GetQuantityServings(world, stack) - litres / ServingLitres, 4);
            pot.SetQuantityServings(world, stack, left);
            byEntity.Api.ModLoader.GetModSystem<ShipyardModSystem>().Log("{0} pours {1} L of {2} from {3}: {4} servings of {5} L left",
                byEntity.GetName(), litres, Content.Collectible.Code, stack.Collectible.Code, left, ServingLitres);
            if (left <= 0 && stack.Collectible.Attributes?[EmptiedKey].AsString() is { } emptied && world.GetBlock(new AssetLocation(emptied)) is { } empty)
            {
                Slot.Itemstack = new ItemStack(empty);
            }
            Slot.MarkDirty();
        }
    }
}

public sealed class HideLaying(AssetLocation hides, string[] states) : Laying
{
    private static readonly Dictionary<string, int> Sections = new() { ["small"] = 1, ["medium"] = 2, ["large"] = 3, ["huge"] = 5 };

    public override string Action => "roughseas:shipyard-sew-hide";

    public override string Hands => WorkHands.Tie;

    public override bool Takes(ItemSlot slot) =>
        slot.Itemstack?.Collectible is { } hide && WildcardUtil.Match(hides, hide.Code)
        && states.Contains(hide.Variant["type"]) && hide.Variant["size"] is { } size && Sections.ContainsKey(size);

    public override int Pieces(ItemSlot slot) => Sections[slot.Itemstack!.Collectible.Variant["size"]];

    public override void Spend(ItemSlot slot, EntityAgent byEntity) => slot.TakeOut(1);

    public override void Spend(ItemSlot slot, EntityAgent byEntity, int used)
    {
        var hide = slot.Itemstack!.Collectible;
        var attributes = slot.Itemstack.Attributes.Clone();
        Spend(slot, byEntity);
        foreach (var size in Remainder(Pieces(hide) - used))
        {
            if (byEntity.World.GetItem(hide.CodeWithVariant("size", size)) is not { } smaller) continue;
            var rest = new ItemStack(smaller) { Attributes = attributes.Clone() };
            if (!byEntity.TryGiveItemStack(rest)) byEntity.World.SpawnItemEntity(rest, byEntity.Pos.XYZ);
        }
    }

    public static IEnumerable<string> Remainder(int sections)
    {
        foreach (var (size, count) in Sections.OrderByDescending(entry => entry.Value))
        {
            for (; sections >= count; sections -= count) yield return size;
        }
    }

    public override IEnumerable<ItemStack> Shown(IWorldAccessor world) =>
        world.SearchItems(hides).Where(hide => states.Contains(hide.Variant["type"]) && hide.Variant["size"] is { } size && Sections.ContainsKey(size))
            .Select(hide => new ItemStack(hide));

    private static int Pieces(CollectibleObject hide) => Sections[hide.Variant["size"]];
}
