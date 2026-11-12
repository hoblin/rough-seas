namespace RoughSeas.Storms;

public readonly record struct WaterOnHull(double X, double Z, double AccelerationX = 0, double AccelerationZ = 0, double WayAlong = 0);
