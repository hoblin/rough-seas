using System;

namespace RoughSeas.Vessels;

public interface IHullForm
{
    /// <summary>How much of her hold is cut out: 0 for a solid blank, 1 when she is finished.</summary>
    double Hollowed { get; }

    /// <summary>How high the floor of her hold stands above her origin now: the top of the wood not yet cut out.</summary>
    double FloorMetres { get; }

    /// <summary>Raised on either side when <see cref="Hollowed"/> changes.</summary>
    event Action? Reformed;
}
