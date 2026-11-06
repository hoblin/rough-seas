using Vintagestory.API.Common;

namespace RoughSeas.Shipyard;

public interface IStruck
{
    double SecondsPerBlow { get; }

    /// <returns>Whether the work may start, on both sides.</returns>
    bool TakeUp(EntityAgent byEntity);

    /// <returns>Whether the work takes another blow.</returns>
    bool Strike(EntityAgent byEntity, ItemSlot tool);
}
