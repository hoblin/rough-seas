using System.Collections.Generic;

namespace RoughSeas.Wind;

public class WindConfig
{
    public double AirKgPerCubicMetre;
    public double ReferenceHeightMetres;
    public double GradientExponent;
    public double BluffDrag;
    public double Camber;
    public double ClothKgPerSquareMetre;
    public double AirMetresSquaredPerSecond;
    public float LogSeconds;
    public double JumpMetresPerSecond;
    public FlowConfig Flow = new();
    public CrewConfig Crew = new();
    public LineConfig Line = new();
}

public class CrewConfig
{
    public double WalkMetresPerSecond;
    public double HaulMetresPerSecond;
}

public class FlowConfig
{
    public NormalPlateConfig NormalPlate = new();
    public double VortexSweepDegrees;
    public double[][] Deltas = [];
    public double[][] Plates = [];
}

public class NormalPlateConfig
{
    public double Across;
    public double Aspect;
}

public class LineConfig
{
    public string Material = "";
    public double MeasuredMetres;
}
