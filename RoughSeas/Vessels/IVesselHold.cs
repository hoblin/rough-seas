namespace RoughSeas.Vessels;

public interface IVesselHold
{
    double Litres { get; }

    double CapacityLitres { get; }

    void Ship(double litres, string cause);
}
