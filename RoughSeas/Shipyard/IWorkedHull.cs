using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RoughSeas.Shipyard;

/// <summary>What takes a piece of a vessel's build from the hand, held on it for as long as the work takes: a vessel being built, or laths laid out to bind into one.</summary>
public interface IWorkedHull
{
    /// <summary>The held item's work on her, or null when it does none.</summary>
    HullWork? WorkWith(ItemSlot slot);

    /// <summary>Each step of a hold that is doing that work, on both sides.</summary>
    void Working(EntityAgent byEntity) { }

    /// <summary>Does that work with the held item. Server only.</summary>
    void Lay(EntityAgent byEntity, ItemSlot slot);
}

/// <summary>
/// A block a held item works into a vessel where it stands. The hold goes through the held item, never the block's
/// own interact hooks, since the work replaces the block: see docs/modding.md.
/// </summary>
public interface IWorkedBlock
{
    IWorkedHull? HullAt(IWorldAccessor world, BlockPos pos);
}

/// <summary>A held item's work on a hull: its time, the lang key refusing it with that key's arguments, and whether vanilla's progress bar shows the hold.</summary>
public sealed record HullWork(double Seconds, string? Refusal = null, bool ShowsBar = false)
{
    public object[] RefusalArgs { get; init; } = [];
}
