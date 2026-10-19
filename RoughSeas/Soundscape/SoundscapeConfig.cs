using System.Collections.Generic;

namespace RoughSeas.Soundscape;

public class SoundscapeConfig
{
    public double UpdateSeconds = 0.25;
    public int SeaProbeBlocks = 3;
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
}

public class BedsConfig
{
    public double EaseSeconds = 4;
    public double[][] Volume = [];
    public BedConfig[] Loops = [];
}

public class BedConfig
{
    public string Sound = "";
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

public class ShoreConfig
{
    public string Surf = "";
    public int SurfBlockCount = 50;
    public int MaxDepthBlocks = 8;
    public double[][] Lapping = [];
    public double[][] Breaking = [];
}

public class HullVoicesConfig
{
    public float Range = 24;
    public double[][] Volume = [];
    public Dictionary<string, Dictionary<string, HullVoice>> Models = new();
}

public class HullVoice
{
    public string? Knock;
    public string? Harm;
    public string[] Break = [];
}

public class WaterConfig
{
    public float Range = 24;
    public double ListenMetres = 32;
    public Cue Slap = new();
    public Cue Slam = new();
    public Cue Pour = new();
    public Cue Slosh = new();
    public Cue Leak = new();
    public Cue Lapping = new();
}
