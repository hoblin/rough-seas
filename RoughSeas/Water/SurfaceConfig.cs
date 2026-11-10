namespace RoughSeas.Water;

public class SurfaceConfig
{
    public SurfaceTrainConfig[] Trains = [];
    public double Steepness;
    public double MinLengthBlocks;
    public double SettleSeconds;
    public double TurnDegrees;
    public double Stretch;
    public double AimSeconds;
    public double FullFoamMetres;
    public ShoreSurfConfig Shore = new();
    public WhitecapsConfig[] Whitecaps = [];
    public ShortWavesConfig ShortWaves = new();
    public TessellationConfig Tessellation = new();
}

public class ShortWavesConfig
{
    public double PhillipsConstant;
    public ShortBandConfig[] Bands = [];
}

public class ShortBandConfig
{
    public double FromBlocks;
    public double ToBlocks;
    public double[] AnglesDegrees = [];
}

public class TessellationConfig
{
    public double ReachMetres = 64;
    public int MaxLevel = 8;
}

public class SurfaceTrainConfig
{
    public double AngleDegrees;
    public double LengthFactor;
    public double Energy;
}

public class WhitecapsConfig
{
    public int Force;
    public double Caps;
    public double Streaks;
    public double Spray;
}
