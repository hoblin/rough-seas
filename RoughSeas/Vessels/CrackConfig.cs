using Vintagestory.API.Common;

namespace RoughSeas.Vessels;

public class CrackConfig
{
    public string[] Textures = [];

    /// <summary>Overlays laid over <see cref="Textures"/>, one list per hull condition after "sound".</summary>
    public AssetLocation[][] Overlays = [];
}
