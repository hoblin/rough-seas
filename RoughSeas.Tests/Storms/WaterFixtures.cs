using RoughSeas.Storms;

namespace RoughSeas.Tests.Storms;

public static class WaterFixtures
{
    public const double X = 512031.25;
    public const double Z = 512300.5;
    public const int SeaFloor = 40;
    public const int WaterTop = 62;
    public const double StillLevel = 7 / 8.0;
    public const double StillSurface = WaterTop + StillLevel;

    public sealed class Sea(double height, bool swell = true, bool local = true) : IDrawnSea
    {
        public bool Swell => swell;

        public bool SwellAt(double x, double z) => swell && local;

        public double Seconds => 0;

        public double HeightAt(double x, double z) => swell ? height : 0;

        public SeaPose PoseOf(double x, double z, double yaw, double length, double width) => default;
    }

    public sealed class Column(int top = WaterTop, int[]? solid = null, double level = StillLevel) : IWaterColumn
    {
        public bool Water(int y) => y > SeaFloor && y <= top;

        public double Level(int y) => y == top ? level : 1;

        public bool Solid(int y) => y <= SeaFloor || (solid?.Contains(y) ?? false);
    }
}
