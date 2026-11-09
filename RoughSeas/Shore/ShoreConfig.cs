namespace RoughSeas.Shore;

public class ShoreConfig
{
    public RunUpConfig RunUp = new();
    public SheetConfig Sheet = new();
    public BedConfig[] Beds = [];
    public BandConfig Band = new();
}

public class RunUpConfig
{
    public double StockdonFactor = 1.1;
    public double SetupSlopeFactor = 0.35;
    public double IncidentSwashFactor = 0.563;
    public double InfragravitySwash = 0.004;
    public double DissipativeIribarren = 0.3;
    public double DissipativeFactor = 0.043;
    public double SandyMaxSlope = 0.143;
    public double EurotopFactor = 1.65;
    public double EurotopCeiling = 4.0;
    public double EurotopCeilingRoot = 1.5;
    public double SpectralPeriodRatio = 1.1;
    public double WallSlope = 1.0;
    public int SlopeSearchBlocks = 48;
}

public class SheetConfig
{
    public double UprushFriction = 0.007;
    public double LensDepthRatio = 27;
}

public class BedConfig
{
    public string Material = "stone";
    public double Conductivity;
    public double Roughness = 1;
    public bool Sandy;
}

public class BandConfig
{
    public int HaloBlocks = 96;
    public int HighestRiseBlocks = 16;
    public double RefreshMinutes = 10;
    public double TickBudgetMilliseconds = 6;
}
