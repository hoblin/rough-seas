namespace RoughSeas.Vessels;

/// <summary>What moves a hull through the water, for whatever else acts on her there.</summary>
public interface IHullWay
{
    /// <summary>The water's drag on her as she floats now; null while she has no draft to take it on.</summary>
    HullDrag? WaterDrag { get; }

    /// <summary>Whether her rower's own drag has taken over from vanilla's water drag along the water.</summary>
    bool Driving { get; }
}
