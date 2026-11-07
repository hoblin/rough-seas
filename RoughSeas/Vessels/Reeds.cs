using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace RoughSeas.Vessels;

/// <summary>
/// The plants a vessel's reed bundles were bound from, one per side in her <see cref="Port"/> and <see cref="Starboard"/>
/// variants; a plant is the code of the vanilla tops item its bundle was bound from, and is named as that item.
/// </summary>
public static class Reeds
{
    public const string Port = "port";
    public const string Starboard = "starboard";
    public const string None = "none";

    public static string[] Of(IDictionary<string, string> variant) =>
        new[] { Port, Starboard }.Select(side => variant.TryGetValue(side, out var plant) ? plant : None).Where(plant => plant != None).Distinct().ToArray();

    public static string? NameOf(AssetLocation code, IDictionary<string, string> variant, string kind) =>
        Of(variant) is { Length: > 0 } plants
            ? Lang.GetMatching($"{code.Domain}:{kind}-{code.Path}", string.Join(", ", plants.Select(plant => Lang.Get($"{Wood.Vanilla}:item-{plant}"))))
            : null;
}
