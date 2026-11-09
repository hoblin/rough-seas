using Newtonsoft.Json;
using RoughSeas.Shore;

namespace RoughSeas.Tests.Shore;

public static class ShoreFixtures
{
    private static readonly Lazy<ShoreConfig> config =
        new(() => JsonConvert.DeserializeObject<ShoreConfig>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "shore.json")))!);

    public static ShoreConfig Config => config.Value;

    public static BedConfig Bed(string material) => Config.Beds.Single(bed => bed.Material == material);

    public static RunUp RunUp => new(Config.RunUp);

    public static Band Band => new(RunUp, Config.Sheet, Config.RunUp.SlopeSearchBlocks);
}

public sealed class Ground(params string[] rows) : IShoreGround
{
    public ShoreCell At(int x, int z)
    {
        if (z < 0 || z >= rows.Length || x < 0 || x >= rows[z].Length) return default;
        return rows[z][x] switch
        {
            '~' => new ShoreCell(ShoreKind.Water, 0, ShoreFixtures.Bed("sand"), 1),
            >= '0' and <= '9' and var height => new ShoreCell(ShoreKind.Land, height - '0' + 0.125, ShoreFixtures.Bed("sand"), 0),
            >= 'a' and <= 'j' and var height => new ShoreCell(ShoreKind.Land, height - 'a' + 0.125, ShoreFixtures.Bed("stone"), 0),
            >= 'A' and <= 'J' and var height => new ShoreCell(ShoreKind.Land, height - 'A' + 0.125, ShoreFixtures.Bed("gravel"), 0),
            _ => default,
        };
    }

    public IEnumerable<ShoreSeed> Seeds()
    {
        for (var z = 0; z < rows.Length; z++)
        for (var x = 0; x < rows[z].Length; x++)
        {
            if (rows[z][x] == '~') yield return new ShoreSeed(x, z, 1);
        }
    }
}
