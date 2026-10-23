using System.Collections.Generic;

namespace RoughSeas.Soundscape;

public class SoundscapeConfig
{
    public double UpdateSeconds = 0.1;
    public int SeaProbeBlocks = 3;
    public double SeaFromMetres = 0.1;
    public float VanillaRowingVolume = 1;
    public BedsConfig Beds = new();
    public CrestsConfig Crests = new();
    public ShoreConfig Shore = new();
    public HullVoicesConfig Hull = new();
    public WaterConfig Water = new();
}

public class Cue
{
    public string Sound = "";
    public double[][] Volume = [];

    public virtual double VolumeAt(double amount) => Curve.At(Volume, amount);
}

public class WaveCue : Cue
{
    public double VolumePerMetre;

    public override double VolumeAt(double waveHeight) => System.Math.Min(1, VolumePerMetre * waveHeight);
}

public class BedsConfig
{
    public double EaseSeconds = 4;
    public double[][] Lufs = [];
    public BedConfig[] Loops = [];
}

public class BedConfig
{
    public string Sound = "";
    public double Lufs;
    public double[][] Share = [];
}

public class CrestsConfig
{
    public float Range = 48;
    public double[] Metres = [12, 28];
    public double SpreadDegrees = 70;
    public ScatteredCue Hiss = new();
    public ScatteredCue Plunge = new();
}

public class ScatteredCue : Cue
{
    public double PerSecond;
}

public class SplashCue : WaveCue
{
    public double FullFromMetres = 1;
}

public class ShoreConfig
{
    public SurfBand[] Surf = [];
    public int SurfBlockCount = 50;
    public int MaxDepthBlocks = 8;
    public double[][] BreakingStrength = [];
}

public class SurfBand
{
    public string Sound = "";
    public double FromMetres;
    public double Gain = 1;
}

public class HullVoicesConfig
{
    public float Range = 24;
    public double[][] Volume = [];
    public double HardFromSeverity = 1;
    public float BreakVolume = 1;
    public Dictionary<string, Dictionary<string, HullVoice>> Models = new();
}

public class HullVoice
{
    public string? Knock;
    public string? Hard;
    public string? Harm;
    public string[] Break = [];
}

public class WaterConfig
{
    public float Range = 24;
    public double ListenMetres = 32;
    public double StrongSlapFromMetres = double.PositiveInfinity;
    public WaveCue Slap = new();
    public WaveCue StrongSlap = new();
    public Cue Slam = new();
    public Cue Pour = new();
    public Cue Slosh = new();
    public Cue Leak = new();
    public SplashCue Splash = new();
}
