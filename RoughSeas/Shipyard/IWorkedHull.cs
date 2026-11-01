using Vintagestory.API.Common;

namespace RoughSeas.Shipyard;

/// <summary>What takes a piece of a vessel's build from the hand, held on it for as long as the work takes: a vessel being built, or laths laid out to bind into one.</summary>
public interface IWorkedHull
{
    /// <summary>How long the held item's work on her takes, with the lang key refusing it, or null when it does none.</summary>
    (double Seconds, string? Refusal)? WorkWith(ItemSlot slot);

    /// <summary>Does that work with the held item. Server only.</summary>
    void Lay(EntityAgent byEntity, ItemSlot slot);
}
