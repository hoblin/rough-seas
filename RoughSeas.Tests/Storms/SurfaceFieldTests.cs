using RoughSeas.SeaAndWeather;
using RoughSeas.Storms;

namespace RoughSeas.Tests.Storms;

public class SurfaceFieldTests
{
    private const int RegionSize = 512;
    private const int CellBlocks = 32;
    private const double PlayerX = 512200;
    private const double PlayerZ = 512300;
    private const double OpenSeaX = 512000 + 16;

    private static SurfaceField Field()
    {
        var field = new SurfaceField();
        field.Update(new Lake(), PlayerX, PlayerZ, RegionSize, CellBlocks);
        return field;
    }

    [Fact]
    public void AimsAtTheHighestWavesAround()
    {
        var field = Field();

        Assert.Equal(Lake.OpenSea, field.Height);
        Assert.Equal(Lake.OpenSeaPeriod, field.Period);
    }

    [Fact]
    public void DrawsEachCellAtItsShareOfTheHighest()
    {
        var field = Field();

        Assert.Equal(1, field.Factor(OpenSeaX, PlayerZ), 2);
        Assert.Equal(Lake.Bay / Lake.OpenSea, field.Factor(OpenSeaX + CellBlocks, PlayerZ), 2);
        Assert.Equal(0, field.Factor(OpenSeaX + 10 * CellBlocks, PlayerZ), 2);
    }

    [Fact]
    public void BlendsBetweenCellsAsTheShaderDoes()
    {
        var field = Field();
        var between = field.Factor(OpenSeaX + CellBlocks / 2.0, PlayerZ);

        Assert.Equal((1 + Lake.Bay / Lake.OpenSea) / 2, between, tolerance: 1 / 255.0);
    }

    [Fact]
    public void ShipsTheSameSharesToTheShader()
    {
        var field = Field();
        var pixels = field.Pixels();

        Assert.Equal(field.Across * field.Across, pixels.Length);
        Assert.Equal((0, 255, 64), (pixels[15] & 0xff, pixels[16] & 0xff, pixels[17] & 0xff));
        Assert.Equal(255, pixels[field.Across + 16] & 0xff);
    }

    private sealed class Lake : ISeaSource
    {
        public const double OpenSea = 4;
        public const double OpenSeaPeriod = 8;
        public const double Bay = 1;

        public SeaSample Sample(double x, double z)
        {
            var column = (int)Math.Floor((x - 512000) / CellBlocks);
            var (height, period) = column switch
            {
                0 => (OpenSea, OpenSeaPeriod),
                1 => (Bay, 3.0),
                _ => (0.0, 0.0)
            };
            return new SeaSample(5, 0, 0, 0, height, period, 0, 0, 10);
        }

        public (double X, double Z) Wind(double x, double z) => (5, 0);

        public double StormPresence(double x, double z) => 0;

        public SeaState At(double x, double z) => default;

        public SeaCurrent CurrentAt(double x, double z) => default;

        public float? RainCloudness(double x, double z, double totalDays) => 0;

        public double RegionMps(int regionX, int regionZ) => 5;
    }
}
