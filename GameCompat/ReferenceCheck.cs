using Mono.Cecil;

namespace GameCompat;

internal static class ReferenceCheck
{
    public static IEnumerable<string> Run(ModuleDefinition mod, GameRelease release, GameRelease current)
    {
        foreach (var type in mod.GetTypeReferences().Where(type => IsGames(type, current)))
        {
            if (Resolve(type.Resolve) == null) yield return $"type {type.FullName} is missing";
        }

        foreach (var member in mod.GetMemberReferences().Where(member => IsGames(member.DeclaringType, current)))
        {
            if (Resolve(member.Resolve) == null) yield return $"{member.FullName} is missing";
        }
    }

    private static bool IsGames(TypeReference type, GameRelease current) =>
        type.GetElementType().Scope is AssemblyNameReference assembly && current.Ships(assembly.Name);

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
