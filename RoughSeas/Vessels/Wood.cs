using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace RoughSeas.Vessels;

/// <summary>
/// A tree species, in the domain whose <c>block/wood</c> list names it. Its <see cref="Code"/> is the vessel variant:
/// a vanilla wood's is the bare species and never carries its domain, which keeps vanilla vessel codes (<c>dugout-oak</c>) stable in saves.
/// </summary>
public record Wood(string Domain, string Species)
{
    public const string Vanilla = "game";
    public const string VariantCode = "wood";

    public static Wood Parse(string code)
    {
        var dash = code.IndexOf('-');
        return dash < 0 ? new Wood(Vanilla, code) : new Wood(code[..dash], code[(dash + 1)..]);
    }

    public static Wood? Of(IDictionary<string, string> variant) =>
        variant.TryGetValue(VariantCode, out var code) && code != null ? Parse(code) : null;

    public string Prefix => Domain == Vanilla ? "" : $"{Domain}-";

    public string Code => Prefix + Species;

    public IEnumerable<string> Keys => [$"{Domain}:{Species}", Species];

    public AssetLocation Log => new(Domain, $"log-placed-{Species}-ud");

    public AssetLocation LyingLog => new(Domain, $"log-placed-{Species}-we");

    public AssetLocation Plank => new(Domain, $"plank-{Species}");

    public string Name => Lang.Get($"{Vanilla}:material-{Species}");

    public static string? NameOf(CollectibleObject collectible) =>
        Of(collectible.Variant) is { } wood ? Lang.GetMatching($"{collectible.Code.Domain}:item-{collectible.Code.Path}", wood.Name) : null;

    public AssetLocation Locate(AssetLocation code) => code.Path switch
    {
        "{log}" => Log,
        "{plank}" => Plank,
        _ => code
    };
}
