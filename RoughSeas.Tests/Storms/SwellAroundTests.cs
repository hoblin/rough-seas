using RoughSeas.SeaAndWeather;
using RoughSeas.Storms;
using Sea = RoughSeas.Tests.SeaAndWeather.Fixtures;

namespace RoughSeas.Tests.Storms;

public class SwellAroundTests
{
    private const int RegionSize = 512;
    private const double X = 512200.5;
    private const double Z = 512300.25;
    private const double Frame = 1 / 60.0;
    private const int FramesPerAim = 30;
    private const double SnapshotSeconds = 5;

    private double hours;
    private readonly SeaClock clock;
    private readonly SeaMap map;

    public SwellAroundTests()
    {
        clock = new SeaClock(() => hours, () => 0.5, () => 60);
        map = new SeaMap(Sea.Config(), clock, () => RegionSize);
    }

    private double SlotSeconds => clock.SecondsAt(Sea.Config().SeaSampleHours);

    [Theory]
    [InlineData(12)]
    [InlineData(200)]
    public void StandsAtTheHeightTheClientDrawsAtTheSamePointAndTime(double intoSlot)
    {
        var slot = 1000;
        var end = slot * SlotSeconds + intoSlot;
        var (client, clientSea) = ClientSurface();
        var server = new SwellAround(Fixtures.Config().Surface, new Waves(Fixtures.Config().Waves), Sea.Config(), clock);
        var serverFrom = end - 3;
        var sentAt = double.NegativeInfinity;
        var nextCheck = serverFrom;
        var compared = 0;
        var highest = 0.0;

        // the client has drawn the sea here since the slot before; the server builds it once the body is in the water
        var start = end - SlotSeconds / 2;
        for (var frame = 0; ; frame++)
        {
            var seconds = start + frame * Frame;
            if (seconds > end + 1e-9) break;
            hours = clock.HoursAt(seconds);
            if (seconds - sentAt >= SnapshotSeconds)
            {
                Send(seconds);
                sentAt = seconds;
            }
            var aimed = frame % FramesPerAim == 0;
            if (aimed) client.Aim.Aim(map, X, Z, seconds, RegionSize, foam: true);
            client.Surface.Advance(seconds);

            if (seconds < nextCheck - 1e-9 || !aimed) continue;
            nextCheck += 1;
            server.At(map, X, Z, seconds, RegionSize);
            for (var i = 0; i < 12; i++)
            {
                var (x, z) = (X + i * 3.7 - 20, Z - i * 2.3 + 10);
                Assert.Equal(clientSea.HeightAt(x, z), server.Sea.HeightAt(x, z), 9);
                highest = Math.Max(highest, Math.Abs(clientSea.HeightAt(x, z)));
            }
            compared++;
        }

        Assert.Equal(4, compared);
        Assert.InRange(highest, 0.3, 3);
    }

    [Fact]
    public void DrawsNoSwellWhereTheMapHasNoSea()
    {
        var server = new SwellAround(Fixtures.Config().Surface, new Waves(Fixtures.Config().Waves), Sea.Config(), clock);
        hours = clock.HoursAt(1000 * SlotSeconds);

        server.At(map, X, Z, 1000 * SlotSeconds, RegionSize);

        Assert.False(server.Sea.SwellAt(X, Z));
    }

    private ((SeaSurface Surface, SurfaceAim Aim), IDrawnSea) ClientSurface()
    {
        var surface = new SeaSurface(Fixtures.Config().Surface, new Waves(Fixtures.Config().Waves));
        var field = new SurfaceField();
        return ((surface, new SurfaceAim(surface, field, Fixtures.Config().Surface, Sea.Config(), clock)), new DrawnSea(surface, field));
    }

    // the snapshots the server sends around the player: a sea rising slowly, calmer toward one side, its waves turning each slot
    private void Send(double seconds)
    {
        var side = RegionSize / Sea.Config().Map.CellBlocks;
        var firstSlot = (long)Math.Floor(hours / Sea.Config().SeaSampleHours) - 1;
        var generations = Enumerable.Range(0, 3).Select(i => new WaveGeneration((firstSlot + i) * 0.7 % 6, 6 + (firstSlot + i) % 3)).ToArray();
        var rise = 1 + 0.1 * Math.Sin(seconds / 600);
        var (regionX, regionZ) = ((int)Math.Floor(X / RegionSize), (int)Math.Floor(Z / RegionSize));
        for (var dx = -1; dx <= 1; dx++)
        for (var dz = -1; dz <= 1; dz++)
        {
            var samples = Enumerable.Range(0, side * side)
                .Select(i => new SeaSample(8, -3, 0, 0, Math.Max(0, 2.2 - 0.04 * (i % side + dx * side)) * rise, 7, 0, 0, 12))
                .ToArray();
            map.Receive(SeaMapSnapshot.Of(regionX + dx, regionZ + dz, hours, 9, side, samples, firstSlot, generations));
        }
    }
}
