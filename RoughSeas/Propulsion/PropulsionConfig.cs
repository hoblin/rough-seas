using System;
using System.Collections.Generic;
using RoughSeas.Vessels;
using Vintagestory.API.MathTools;

namespace RoughSeas.Propulsion;

public class PropulsionConfig
{
    public RowerConfig Rower = new();
    public Dictionary<string, StrokeConfig> Strokes = [];
    public PoleConfig Pole = new();
}

public class RowerConfig
{
    public double MetabolicWatts;
    public double SatietyPerKilojoule;
    public double VanillaPace;
}

public class StrokeConfig
{
    public double StrokesPerMinute;
    public double DriveShare;
    public double GrossEfficiency;
    public double DragCoefficient;
    public double PushNewtons;
    public double PushMetres;
    public double VeerDegrees;
}

public class PolingConfig
{
    public string Stroke = Pole.Code;
    public double HandsAboveWaterMetres;
    public double PlantAheadMetres;
    public double PlantAsternMetres;
    public double PlantAcrossMetres;
    public double DrawnPoleMetres;
    public double DrawnBedMetres;
    public StanceConfig? Stance;
}

public class StanceConfig
{
    public string Animation = "";
    public Vec3f? RiderOffset;
    public float EyeHeight;
    public Vec3f? MountRotation;

    public SeatPose Pose => new(RiderOffset, Animation, EyeHeight, MountRotation);
}

public class PoleConfig
{
    public double WorkingDegrees;
    public double WorkedMetres;

    private double WorkingRadians => WorkingDegrees * Math.PI / 180;

    public double Reach(double metres, double handsAboveWaterMetres) => metres * Math.Cos(WorkingRadians) - handsAboveWaterMetres;

    public double Footing(double handsAboveBedMetres) => handsAboveBedMetres * Math.Tan(WorkingRadians);

    public double Slanted(double perUpright) => perUpright / Math.Cos(WorkingRadians);

    public double Travel(double metres, double handsAboveBedMetres) =>
        handsAboveBedMetres >= metres ? 0 : Math.Max(0, Math.Sqrt(metres * metres - handsAboveBedMetres * handsAboveBedMetres) - Footing(handsAboveBedMetres));
}
