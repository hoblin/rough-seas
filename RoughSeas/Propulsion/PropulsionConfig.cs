using System;
using System.Collections.Generic;

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
}

public class PoleConfig
{
    public double WorkingDegrees;
    public double DressedMetres;

    public double Reach(double metres, double handsAboveWaterMetres) =>
        metres * Math.Cos(WorkingDegrees * Math.PI / 180) - handsAboveWaterMetres;
}
