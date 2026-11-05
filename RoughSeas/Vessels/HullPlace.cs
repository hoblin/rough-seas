using System;
using RoughSeas.Units;
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

public readonly record struct HullShape(double HalfLengthMetres, double HalfBeamMetres, double DepthMetres, double CentreAlongMetres, double CentreAcrossMetres, double KeelMetres)
{
    public const double EndShare = 0.4;
    public const double BottomShare = 0.25;
    private const double MarkReach = 0.7;
    private const double Stagger = 0.15;
    private const double SideReach = 0.9;

    public static readonly HullPlace[] Places = Enum.GetValues<HullPlace>();

    public static HullShape FromBox(double fromX, double fromY, double fromZ, double toX, double toY, double toZ) => new(
        (toX - fromX) / 2, (toZ - fromZ) / 2, toY - fromY, (fromX + toX) / 2 - 0.5, (fromZ + toZ) / 2 - 0.5, fromY);

    public static HullShape FromVoxels(double[] from, double[] to) =>
        FromBox(Voxels.ToMetres(from[0]), Voxels.ToMetres(from[1]), Voxels.ToMetres(from[2]), Voxels.ToMetres(to[0]), Voxels.ToMetres(to[1]), Voxels.ToMetres(to[2]));

    public HullPlace At(double along, double height, double across)
    {
        along -= CentreAlongMetres;
        across -= CentreAcrossMetres;
        if (height - KeelMetres < BottomShare * DepthMetres && Math.Abs(across) < HalfBeamMetres / 2) return HullPlace.Bottom;
        return Side(along, across);
    }

    public bool Beyond(double across) => Math.Abs(across - CentreAcrossMetres) > HalfBeamMetres;

    public HullShape Spanning(double across)
    {
        double low = Math.Min(CentreAcrossMetres - HalfBeamMetres, across), high = Math.Max(CentreAcrossMetres + HalfBeamMetres, across);
        return this with { HalfBeamMetres = (high - low) / 2, CentreAcrossMetres = (low + high) / 2 };
    }

    public HullPlace Toward(double along, double across) =>
        Side(Math.Abs(along) > Math.Abs(across) ? Math.Sign(along) * HalfLengthMetres : 0, across);

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
        if (place == HullPlace.Bottom) return (CentreAlongMetres, KeelMetres, CentreAcrossMetres);

        double along = place switch
        {
            HullPlace.PortBow or HullPlace.StarboardBow => -MarkReach,
            HullPlace.PortQuarter or HullPlace.StarboardQuarter => MarkReach,
            _ => 0
        };
        bool port = place.IsPort();
        along += port ? -Stagger : Stagger;
        double across = port ? -SideReach : SideReach;
        return (CentreAlongMetres + along * HalfLengthMetres, KeelMetres + DepthMetres / 2, CentreAcrossMetres + across * HalfBeamMetres);
    }

    private HullPlace Side(double along, double across)
    {
        bool port = across < 0;
        if (along < -EndShare * HalfLengthMetres) return port ? HullPlace.PortBow : HullPlace.StarboardBow;
        if (along > EndShare * HalfLengthMetres) return port ? HullPlace.PortQuarter : HullPlace.StarboardQuarter;
        return port ? HullPlace.PortSide : HullPlace.StarboardSide;
    }
}
