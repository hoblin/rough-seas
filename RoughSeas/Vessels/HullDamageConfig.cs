using Vintagestory.API.Common;

namespace RoughSeas.Vessels;

public class HullDamageConfig
{
    public string[] Textures = [];

    /// <summary>Whose marks <see cref="Textures"/> show: wood, bark, reed or hide.</summary>
    public string Material = "wood";

    public AssetLocation Overlay(int stage) => new($"roughseas:entity/vessel/damage/{Material}-{stage}");
}
