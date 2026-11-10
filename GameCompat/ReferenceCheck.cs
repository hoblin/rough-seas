using Mono.Cecil;

namespace GameCompat;

internal static class ReferenceCheck
{
    public static IEnumerable<string> Run(ModuleDefinition mod, GameRelease release)
    {
        foreach (var type in mod.GetTypeReferences().Where(type => IsGames(type, release)))
        {
            if (Resolve(type.Resolve) == null) yield return $"type {type.FullName} is missing";
        }

        foreach (var member in mod.GetMemberReferences().Where(member => IsGames(member.DeclaringType, release)))
        {
            if (Resolve(member.Resolve) == null) yield return $"{member.FullName} is missing";
        }
    }

    private static bool IsGames(TypeReference type, GameRelease release) =>
        type.GetElementType().Scope is AssemblyNameReference assembly && release.Ships(assembly.Name);

    private static IMemberDefinition? Resolve(Func<IMemberDefinition?> resolve)
    {
        try
        {
            return resolve();
        }
        catch (AssemblyResolutionException)
        {
            return null;
        }
    }
}
