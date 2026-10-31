using System;
using Vintagestory.API.Common;

namespace RoughSeas.Vessels;

public interface IHullForm
{
    /// <summary>The litres her hold takes as far as she is built, from the finished vessel's.</summary>
    double HoldLitres(double designed);

    /// <summary>How much of her hold is still solid wood, not yet cut out.</summary>
    double Uncut { get; }

    /// <summary>How high the floor of her hold stands above her origin now: the top of the wood not yet cut out.</summary>
    double FloorMetres { get; }

    /// <summary>The litres of wood she still carries outside the finished hull, not yet hewn away.</summary>
    double UnhewnLitres { get; }

    /// <summary>Her depth from keel to gunwale as far as she is built, from the finished vessel's.</summary>
    double DepthMetres(double designed);

    /// <summary>Her waterplane as far as she is built, from the finished vessel's.</summary>
    double WaterplaneSquareMetres(double designed);

    /// <summary>Her parts as far as she is built, from the parts of the finished vessel.</summary>
    VesselPart[] Built(VesselPart[] designed);

    /// <summary>Her shell as far as she is built, from the shell of the finished vessel.</summary>
    Shell Formed(Shell designed);

    /// <summary>What she breaks up into as far as she is built, from the finished vessel's wreckage.</summary>
    BlockDropItemStack[] Wrecked(BlockDropItemStack[] designed);

    HideCover? Cover { get; }

    /// <summary>Raised on either side when what she is built of, or how much of her hold holds water, changes.</summary>
    event Action? Reformed;
}
