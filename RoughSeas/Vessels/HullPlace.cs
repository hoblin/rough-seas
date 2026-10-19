using System;
using Vintagestory.API.MathTools;

namespace RoughSeas.Vessels;

public enum HullPlace
{
    PortBow,
    StarboardBow,
    PortSide,
    StarboardSide,
    PortQuarter,
    StarboardQuarter,
    Bottom
}

public static class HullPlaces
{
    public static string Code(this HullPlace place) => place.ToString().ToLowerInvariant();
}

public readonly record struct HullShape(double HalfLength, double HalfBeam, double Depth, double CentreAlong, double CentreAcross, double Keel)
{
    public const double EndShare = 0.4;
    public const double BottomShare = 0.25;

    public static readonly HullPlace[] Places = Enum.GetValues<HullPlace>();

    public static HullShape FromBox(double fromX, double fromY, double fromZ, double toX, double toY, double toZ) => new(
        (toX - fromX) / 2, (toZ - fromZ) / 2, toY - fromY, (fromX + toX) / 2 - 0.5, (fromZ + toZ) / 2 - 0.5, fromY);

    public HullPlace At(double along, double height, double across)
    {
        along -= CentreAlong;
        across -= CentreAcross;
        if (height - Keel < BottomShare * Depth && Math.Abs(across) < HalfBeam / 2) return HullPlace.Bottom;
        return Side(along, across);
    }

    public HullPlace Toward(double along, double across) =>
        Side(Math.Abs(along) > Math.Abs(across) ? Math.Sign(along) * HalfLength : 0, across);

    public static (double Along, double Across) ToHull(double worldX, double worldZ, double yaw)
    {
        double turn = yaw + GameMath.PIHALF;
        return (worldX * Math.Cos(turn) - worldZ * Math.Sin(turn), worldX * Math.Sin(turn) + worldZ * Math.Cos(turn));
    }

    public static (double X, double Z) ToWorld(double along, double across, double yaw)
    {
        double turn = yaw + GameMath.PIHALF;
        return (along * Math.Cos(turn) + across * Math.Sin(turn), -along * Math.Sin(turn) + across * Math.Cos(turn));
    }

    public (double Along, double Height, double Across) PointOf(HullPlace place)
    {
        if (place == HullPlace.Bottom) return (CentreAlong, Keel, CentreAcross);

        double along = place switch
        {
            HullPlace.PortBow or HullPlace.StarboardBow => -0.8,
            HullPlace.PortQuarter or HullPlace.StarboardQuarter => 0.8,
            _ => 0
        };
        double across = place is HullPlace.PortBow or HullPlace.PortSide or HullPlace.PortQuarter ? -0.9 : 0.9;
        return (CentreAlong + along * HalfLength, Keel + Depth / 2, CentreAcross + across * HalfBeam);
    }

    private HullPlace Side(double along, double across)
    {
        bool port = across < 0;
        if (along < -EndShare * HalfLength) return port ? HullPlace.PortBow : HullPlace.StarboardBow;
        if (along > EndShare * HalfLength) return port ? HullPlace.PortQuarter : HullPlace.StarboardQuarter;
        return port ? HullPlace.PortSide : HullPlace.StarboardSide;
    }
}
