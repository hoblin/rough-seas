using RoughSeas.Vessels;
using RoughSeas.Wind;

namespace RoughSeas.Sailing;

public class SailingConfig
{
    public double Camber;
    public double ClothKgPerSquareMetre;
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
