using System.Linq;
using System.Text.RegularExpressions;

namespace RoughSeas.Storms;

public static class ShaderNeeds
{
    public static string[] Missing(string source, (string Type, string Name)[] declarations, params string[] anchors) =>
        anchors.Where(anchor => !source.Contains(anchor)).Select(anchor => $"'{anchor}'")
            .Concat(declarations.Where(declaration => !Regex.IsMatch(source, $@"\b{declaration.Type}\s+{declaration.Name}\s*[;=]"))
                .Select(declaration => $"{declaration.Type} {declaration.Name}"))
            .ToArray();
}
