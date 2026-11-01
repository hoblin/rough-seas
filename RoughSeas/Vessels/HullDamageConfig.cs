using Vintagestory.API.Common;

namespace RoughSeas.Vessels;

public class HullDamageConfig
{
    public string[] Textures = [];

    /// <summary>What the hull's surface is, wood, bark, reed or hide, for its sounds; whose marks <see cref="Textures"/> show, of which only wood and reed are drawn.</summary>
    public string Material = "wood";

    public AssetLocation Overlay(int stage) => new($"roughseas:entity/vessel/damage/{Material}-{stage}");
}
