using Vintagestory.API.Common;

namespace RoughSeas.Vessels;

public class HullRepair
{
    /// <summary>What one patch consumes from the mender's hand, stack size included.</summary>
    public JsonItemStack Material = null!;

    /// <summary>Integrity one patch gives back, out of 1.</summary>
    public float Restores;

    /// <summary>Satiety the mender spends on one patch: the work.</summary>
    public float Saturation;

    /// <summary>How long one patch is held on her: the real time of that work over sixty, as a build's pieces are.</summary>
    public double Seconds;

    /// <summary>Upkeep rather than a mend: gives back only integrity lost to wear, never to a hit.</summary>
    public bool Greases;
}
