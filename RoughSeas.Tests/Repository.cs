namespace RoughSeas.Tests;

public static class Repository
{
    public static string Root { get; } = Find();

    public static string Mod => Path.Combine(Root, "RoughSeas");

    private static string Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "RoughSeas", "assets"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("the repository above the test binaries");
    }
}
