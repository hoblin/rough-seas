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

    public static bool IsPort(this HullPlace place) => place is HullPlace.PortBow or HullPlace.PortSide or HullPlace.PortQuarter;

    public static HullPlace EndForEnd(this HullPlace place) => place switch
    {
        HullPlace.PortBow => HullPlace.StarboardQuarter,
        HullPlace.StarboardBow => HullPlace.PortQuarter,
        HullPlace.PortSide => HullPlace.StarboardSide,
        HullPlace.StarboardSide => HullPlace.PortSide,
        HullPlace.PortQuarter => HullPlace.StarboardBow,
        HullPlace.StarboardQuarter => HullPlace.PortBow,
        _ => place
    };
}

public readonly record struct HullShape(double HalfLength, double HalfBeam, double Depth, double CentreAlong, double CentreAcross, double Keel)
{
    public const double EndShare = 0.4;
    public const double BottomShare = 0.25;
    public const double VoxelsPerBlock = 16;
    private const double MarkReach = 0.8;
    private const double SideReach = 0.9;

    public static readonly HullPlace[] Places = Enum.GetValues<HullPlace>();

    public static HullShape FromBox(double fromX, double fromY, double fromZ, double toX, double toY, double toZ) => new(
        (toX - fromX) / 2, (toZ - fromZ) / 2, toY - fromY, (fromX + toX) / 2 - 0.5, (fromZ + toZ) / 2 - 0.5, fromY);

    public static HullShape FromVoxels(double[] from, double[] to) =>
        FromBox(from[0] / VoxelsPerBlock, from[1] / VoxelsPerBlock, from[2] / VoxelsPerBlock, to[0] / VoxelsPerBlock, to[1] / VoxelsPerBlock, to[2] / VoxelsPerBlock);

    public HullPlace At(double along, double height, double across)
    {
        along -= CentreAlong;
        across -= CentreAcross;
        if (height - Keel < BottomShare * Depth && Math.Abs(across) < HalfBeam / 2) return HullPlace.Bottom;
        return Side(along, across);
    }

    public bool Beyond(double across) => Math.Abs(across - CentreAcross) > HalfBeam;

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
            HullPlace.PortBow or HullPlace.StarboardBow => -MarkReach,
            HullPlace.PortQuarter or HullPlace.StarboardQuarter => MarkReach,
            _ => 0
        };
        double across = place is HullPlace.PortBow or HullPlace.PortSide or HullPlace.PortQuarter ? -SideReach : SideReach;
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
