using Mono.Cecil;

namespace GameCompat;

internal sealed class ReachCheck(GameRelease release)
{
    private static readonly HashSet<string> InjectedWithoutCheck = ["__state", "__exception", "__args", "__originalMethod", "__runOriginal"];

    public IEnumerable<string> Run(ModReach reach)
    {
        foreach (var member in reach.Members.Where(member => !member.MayBeAbsent))
        {
            if (Missing(member) is { } reason) yield return $"{reason} ({member.Site})";
        }

        foreach (var patch in reach.Patches)
        {
            if (FindMethod(patch.Target, out _) is not { } target) continue;
            foreach (var mismatch in Mismatches(patch, target)) yield return $"{mismatch} ({patch.Handler.DeclaringType.FullName}.{patch.Handler.Name})";
        }

        foreach (var site in reach.Unreadable) yield return $"this reach can't be read from the assembly: {site}";
    }

    private string? Missing(Reach member)
    {
        if (release.FindType(member.Type) is not { } type) return $"type {member.Type} is missing";
        return member.Kind switch
        {
            ReachKind.Type => null,
            ReachKind.Method => FindMethod(member, out var reason) == null ? reason : null,
            ReachKind.Field => FieldProblem(type, member),
            ReachKind.PropertyGetter => Property(type, member)?.GetMethod != null ? null : $"property getter {member.Type}.{member.Name} is missing",
            ReachKind.PropertySetter => Property(type, member)?.SetMethod != null ? null : $"property setter {member.Type}.{member.Name} is missing",
            _ => null,
        };
    }

    private string? FieldProblem(TypeDefinition type, Reach member)
    {
        var field = Hierarchy(type, member.DeclaredOnly).SelectMany(owner => owner.Fields).FirstOrDefault(candidate => candidate.Name == member.Name);
        if (field == null) return $"field {member.Type}.{member.Name} is missing";
        if (member.FieldType != null && field.FieldType.FullName != member.FieldType) return $"field {member.Type}.{member.Name} is {field.FieldType.FullName}, we access {member.FieldType}";
        if (member.FieldStatic != null && field.IsStatic != member.FieldStatic) return $"field {member.Type}.{member.Name} is {(field.IsStatic ? "static" : "an instance field")}, we access it otherwise";
        return null;
    }

    private PropertyDefinition? Property(TypeDefinition type, Reach member) =>
        Hierarchy(type, member.DeclaredOnly).SelectMany(owner => owner.Properties).FirstOrDefault(property => property.Name == member.Name);

    private MethodDefinition? FindMethod(Reach member, out string reason)
    {
        reason = "";
        if (release.FindType(member.Type) is not { } type)
        {
            reason = $"type {member.Type} is missing";
            return null;
        }

        var name = member.Kind switch
        {
            ReachKind.PropertyGetter => "get_" + member.Name,
            ReachKind.PropertySetter => "set_" + member.Name,
            _ => member.Name,
        };

        foreach (var owner in Hierarchy(type, member.DeclaredOnly))
        {
            var named = owner.Methods.Where(method => method.Name == name).ToList();
            if (member.Parameters != null)
            {
                var exact = named.FirstOrDefault(method => Signature(method) == member.Parameters);
                if (exact != null) return exact;
            }
            else if (named.Count == 1 || (member.MayBeAbsent && named.Count > 1))
            {
                return named[0];
            }
            else if (named.Count > 1)
            {
                var parameterless = named.FirstOrDefault(method => method.Parameters.Count == 0);
                if (parameterless != null) return parameterless;
                reason = $"{member.Type}.{name} is ambiguous: {named.Count} overloads";
                return null;
            }
        }

        reason = member.Parameters == null ? $"method {member.Type}.{name} is missing" : $"method {member.Type}.{name}({member.Parameters}) is missing";
        return null;
    }

    private static string Signature(MethodDefinition method) => string.Join(",", method.Parameters.Select(parameter => parameter.ParameterType.FullName));

    private IEnumerable<string> Mismatches(Patch patch, MethodDefinition target)
    {
        if (patch.Kind == HandlerKind.Transpiler) yield break;

        var named = $"{target.DeclaringType.FullName}.{target.Name}";
        foreach (var parameter in patch.Handler.Parameters)
        {
            var name = parameter.Name;
            if (InjectedWithoutCheck.Contains(name) || IsPositional(name)) continue;

            if (name == "__instance")
            {
                if (!Fits(target.DeclaringType, parameter.ParameterType)) yield return $"{named}: __instance is not a {Plain(parameter.ParameterType).FullName}";
            }
            else if (name == "__result")
            {
                if (target.ReturnType.MetadataType == MetadataType.Void) yield return $"{named} returns nothing for __result";
                else if (!Fits(target.ReturnType, parameter.ParameterType)) yield return $"{named} returns {target.ReturnType.FullName}, the patch takes {parameter.ParameterType.FullName}";
            }
            else if (name.StartsWith("___"))
            {
                var field = Hierarchy(target.DeclaringType, false).SelectMany(owner => owner.Fields).FirstOrDefault(candidate => candidate.Name == name[3..]);
                if (field == null) yield return $"{named}: field {name[3..]} is missing";
                else if (!Fits(field.FieldType, parameter.ParameterType)) yield return $"{named}: field {name[3..]} is {field.FieldType.FullName}, the patch takes {parameter.ParameterType.FullName}";
            }
            else
            {
                var argument = target.Parameters.FirstOrDefault(candidate => candidate.Name == name);
                if (argument == null) yield return $"{named} has no parameter {name}";
                else if (!Fits(argument.ParameterType, parameter.ParameterType)) yield return $"{named}: parameter {name} is {argument.ParameterType.FullName}, the patch takes {parameter.ParameterType.FullName}";
            }
        }
    }

    private static bool IsPositional(string name) => name.StartsWith("__") && name.Skip(2).All(char.IsDigit);

    private static TypeReference Plain(TypeReference type) => type is ByReferenceType byReference ? byReference.ElementType : type;

    private bool Fits(TypeReference actual, TypeReference declared)
    {
        var (source, wanted) = (Plain(actual), Plain(declared));
        if (source.IsGenericParameter || wanted.FullName == "System.Object" || source.FullName == wanted.FullName) return true;
        return Resolve(source) is { } definition && Ancestry(definition).Any(ancestor => ancestor == wanted.FullName);
    }

    private IEnumerable<string> Ancestry(TypeDefinition type)
    {
        foreach (var owner in Hierarchy(type, false))
        {
            yield return owner.FullName;
            foreach (var implemented in owner.Interfaces) yield return implemented.InterfaceType.GetElementType().FullName;
        }
    }

    private IEnumerable<TypeDefinition> Hierarchy(TypeDefinition type, bool declaredOnly)
    {
        yield return type;
        if (declaredOnly) yield break;
        for (var owner = Resolve(type.BaseType); owner != null; owner = Resolve(owner.BaseType)) yield return owner;
    }

    private static TypeDefinition? Resolve(TypeReference? type)
    {
        try
        {
            return type?.Resolve();
        }
        catch (AssemblyResolutionException)
        {
            return null;
        }
    }
}
