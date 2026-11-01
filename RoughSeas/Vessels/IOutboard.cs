using System;

namespace RoughSeas.Vessels;

/// <summary>
/// What stands off a vessel's hull, an outrigger's float: a knock toward its <see cref="Side"/>, or a blow landing
/// outside the hull on that side, strikes it instead of the hull.
/// </summary>
public interface IOutboard
{
    /// <summary>The hull's place it stands off, in her shape's frame.</summary>
    HullPlace Side { get; }

    /// <param name="joules">The knock, as the hull would have taken it.</param>
    void Strike(double joules);

    /// <summary>Server side, after a knock that leaves it worse off: what the knock did.</summary>
    event Action<Knock>? Struck;
}
