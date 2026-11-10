using Mono.Cecil;

namespace GameCompat;

internal static class TextAnchorCheck
{
    private const string AttributeName = "RoughSeas.ReliesOnTextAttribute";

    public static IEnumerable<string> Run(ModuleDefinition mod, GameRelease release)
    {
        foreach (var attribute in mod.GetTypes().SelectMany(BehaviourCheck.Members).SelectMany(member => member.CustomAttributes).Where(attribute => attribute.AttributeType.FullName == AttributeName))
        {
            var (asset, anchor) = ((string)attribute.ConstructorArguments[0].Value, (string)attribute.ConstructorArguments[1].Value);
            if (release.FindAsset(asset) is not { } path) yield return $"{asset} is missing";
            else if (!File.ReadAllText(path).Contains(anchor, StringComparison.Ordinal)) yield return $"{asset} no longer has: {anchor}";
        }
    }
}
