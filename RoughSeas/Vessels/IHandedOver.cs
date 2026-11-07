using Vintagestory.API.Common.Entities;

namespace RoughSeas.Vessels;

/// <summary>A vessel's state that stays with her when she is built on into another vessel.</summary>
public interface IHandedOver
{
    void HandOver(Entity successor, bool endForEnd);
}
