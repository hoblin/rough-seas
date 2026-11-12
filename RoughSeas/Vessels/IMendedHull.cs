using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RoughSeas.Vessels;

/// <summary>A part of a vessel mended by hand: a patch of her material held on the place aimed at for as long as the patch takes. The shipyard's held work drives the hold.</summary>
public interface IMendedHull
{
    MendWork? MendWith(ItemSlot slot, EntityAgent byEntity, Vec3d hit);

    void Mend(EntityAgent byEntity, ItemSlot slot, Vec3d hit);
}

/// <summary>A patch's hold: its time, the lang key refusing it with that key's arguments, and the player animation the hands work it with.</summary>
public sealed record MendWork(double Seconds, string? Refusal = null, params object[] RefusalArgs)
{
    public string Hands { get; init; } = WorkHands.Work;
}
