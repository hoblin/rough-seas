using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GameCompat;

internal static class MethodSource
{
    public static string Diff(Reliance reliance, string fromFolder, string toFolder)
    {
        var from = Text(reliance, fromFolder);
        var to = Text(reliance, toFolder);
        if (from == null || to == null) return "";

        var scratch = Directory.CreateTempSubdirectory("rough-seas-compat").FullName;
        try
        {
            var (before, after) = (Path.Combine(scratch, "before"), Path.Combine(scratch, "after"));
            File.WriteAllText(before, from);
            File.WriteAllText(after, to);
            var start = new ProcessStartInfo("git", ["diff", "--no-index", "--no-color", "-U3", before, after]) { RedirectStandardOutput = true };
            using var diff = Process.Start(start)!;
            var output = diff.StandardOutput.ReadToEnd();
            diff.WaitForExit();
            return output[Math.Max(0, output.IndexOf("@@", StringComparison.Ordinal))..];
        }
        finally
        {
            Directory.Delete(scratch, recursive: true);
        }
    }

    internal static string? Text(Reliance reliance, string folder)
    {
        var names = reliance.Type.Split('/');
        var dot = names[0].LastIndexOf('.');
        var (space, outer) = (dot < 0 ? "" : names[0][..dot], names[0][(dot + 1)..]);

        foreach (var file in Directory.EnumerateFiles(folder, outer + ".cs", SearchOption.AllDirectories))
        {
            var type = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>()
                .FirstOrDefault(candidate => candidate.Identifier.Text == (names.Length == 1 ? outer : names[^1]) && Namespace(candidate) == space);
            if (type == null) continue;

            var members = Named(type, reliance.Method).Select(member => member.ToFullString()).ToList();
            return members.Count == 0 ? null : string.Concat(members);
        }
        return null;
    }

    private static IEnumerable<MemberDeclarationSyntax> Named(TypeDeclarationSyntax type, string method) => method.StartsWith("get_") || method.StartsWith("set_")
        ? type.Members.OfType<PropertyDeclarationSyntax>().Where(property => property.Identifier.Text == method[4..])
        : type.Members.OfType<MethodDeclarationSyntax>().Where(candidate => candidate.Identifier.Text == method);

    private static string Namespace(SyntaxNode node) =>
        node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Select(space => space.Name.ToString()).FirstOrDefault() ?? "";
}
