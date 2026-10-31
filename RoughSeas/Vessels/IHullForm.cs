using System;

namespace RoughSeas.Vessels;

public interface IHullForm
{
    /// <summary>How much of her hold holds water: 0 while none of it does yet, 1 when she is finished.</summary>
    double Hollowed { get; }

    /// <summary>How much of her hold is still solid wood, not yet cut out.</summary>
    double Uncut { get; }

    /// <summary>How high the floor of her hold stands above her origin now: the top of the wood not yet cut out.</summary>
    double FloorMetres { get; }

    /// <summary>Her parts as far as she is built, from the parts of the finished vessel.</summary>
    VesselPart[] Built(VesselPart[] designed);

    /// <summary>Her shell as far as she is built, from the shell of the finished vessel.</summary>
    Shell Formed(Shell designed);

    /// <summary>Raised on either side when what she is built of, or how much of her hold holds water, changes.</summary>
    event Action? Reformed;
}
