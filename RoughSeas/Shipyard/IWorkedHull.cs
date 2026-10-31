using Vintagestory.API.Common;

namespace RoughSeas.Shipyard;

/// <summary>A vessel that takes a piece of her build from the hand, held on her for as long as the work takes.</summary>
public interface IWorkedHull
{
    /// <summary>How long the held item's work on her takes, with the lang key refusing it, or null when it does none.</summary>
    (double Seconds, string? Refusal)? WorkWith(ItemSlot slot);

    /// <summary>Does that work with the held item. Server only.</summary>
    void Lay(EntityAgent byEntity, ItemSlot slot);
}
