namespace RoughSeas.Vessels;

public interface IVesselHold
{
    double Litres { get; }

    /// <summary>What her hold takes now: for a hull still being built, only the part cut out.</summary>
    double CapacityLitres { get; }

    /// <summary>The part of her hold not yet cut out of the wood, which weighs and floats as her hull does.</summary>
    double UncutLitres { get; }

    void Ship(double litres, string cause);
}
