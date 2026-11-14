using RoughSeas.Tests.Storms;
using RoughSeas.Wind;

namespace RoughSeas.Tests.Wind;

public class SquareRigTests
{
    private const double Breeze = 9;
    private const double Yaw = 0;
    private const double Waterline = 0;
    private const int FullSail = 2;
    private const double FiveDegrees = Math.PI / 36;
    private static readonly (double X, double Z) Still = (0, 0);

    private static (VesselInWind Body, Sail Sail) Sailboat()
    {
        var body = Fixtures.SailboatInWind();
        body.Sail!.Hoist(FullSail, 0);
        return (body, body.Sail);
    }

    private static (double X, double Z) FromOffHerBow(double degrees)
    {
        var a = degrees * Math.PI / 180;
        return (-Breeze * Math.Sin(a), -Breeze * Math.Cos(a));
    }

    private static double OffHerBeam(Sail sail)
    {
        var square = (SquareState)sail.State;
        var yard = square.ArmStarboard - square.ArmPort;
        return Math.Atan2(Math.Abs(yard.Along), Math.Abs(yard.Across));
    }

    [Theory]
    [InlineData(100)]
    [InlineData(135)]
    [InlineData(180)]
    public void OffTheWindHerSquareSailDrivesHerAhead(double offBow)
    {
        var (body, _) = Sailboat();
        Assert.True(body.Steady(FromOffHerBow(offBow), Still, Yaw, Waterline).Z > 0);
    }

    [Fact]
    public void RunningHerYardStandsSquareAcrossHer()
    {
        var (body, sail) = Sailboat();
        body.Steady(FromOffHerBow(180), Still, Yaw, Waterline);
        Assert.True(OffHerBeam(sail) < 0.1, sail.Rig.End.Describe(sail.State));
    }

    [Fact]
    public void ReachingSheBracesHerYardRoundClearOfHerStays()
    {
        var (body, sail) = Sailboat();
        body.Steady(FromOffHerBow(100), Still, Yaw, Waterline);
        Assert.True(OffHerBeam(sail) > 0.1, sail.Rig.End.Describe(sail.State));
        Assert.True(sail.Rig.End.Clear(sail.State));
    }

    [Fact]
    public void CloseHauledBeyondWhereHerStaysLetHerBraceHerSailIsTakenAback()
    {
        var (body, sail) = Sailboat();
        body.Steady(FromOffHerBow(30), Still, Yaw, Waterline);
        Assert.True(sail.Aback, sail.Rig.End.Describe(sail.State));
    }

    [Fact]
    public void WhatHerRigHandsHerHullAddsUpToTheSailsForceAndItsWeight()
    {
        var (body, sail) = Sailboat();
        body.Steady(FromOffHerBow(120), Still, Yaw, Waterline);
        var (end, state) = (sail.Rig.End, sail.State);
        var (at, force) = (state.Centre(sail.Set, sail.Fill), new HullPoint(-400, 0, 150));
        var weight = new HullPoint(0, -RoughSeas.Water.WaveTheory.Gravity, 0);
        var square = (SquareState)state;
        var (yard, cloth) = ((double)Fixtures.SailboatWindage["rig"]!["yardKilograms"]! * weight, sail.SquareMetres * Fixtures.Wind.ClothKgPerSquareMetre * weight);
        var load = end.Loads(state, at, force);

        var total = load.Points.Aggregate(default(HullPoint), (sum, push) => sum + push.Force);
        var moment = load.Points.Aggregate(default(HullPoint), (sum, push) => sum + push.At.Cross(push.Force) + push.Couple);
        Assert.Equal(0, (total - (force + yard + cloth)).Length, 6);
        Assert.Equal(0, (moment - (at.Cross(force + cloth) + (0.5 * (square.ArmPort + square.ArmStarboard)).Cross(yard))).Length, 6);
    }

    [Fact]
    public void HeadToWindHerSquareSailIsAbackAndPushesHerAstern()
    {
        var (body, sail) = Sailboat();
        var load = body.Steady(FromOffHerBow(10), Still, Yaw, Waterline);
        Assert.True(sail.Aback);
        Assert.True(load.Z < 0, $"ahead {load.Z:F0} N");
    }

    [Fact]
    public void CloseHauledHerYardStandsAsFarRoundAsHerStaysLetItAndDrawsOnlyWhatThatIncidenceGives()
    {
        var (body, sail) = Sailboat();
        var closeHauled = body.Steady(FromOffHerBow(75), Still, Yaw, Waterline).Z;
        var end = sail.Rig.End;
        var limits = new[] { end.Free.From, end.Free.From + end.Free.Span };
        Assert.True(limits.Any(limit => Math.Abs(limit - sail.State.Yard) < FiveDegrees),
            $"yard {sail.State.Yard * 180 / Math.PI:F1}°, free {end.Free.From * 180 / Math.PI:F1}° over {end.Free.Span * 180 / Math.PI:F1}°: {end.Describe(sail.State)}");
        Assert.True(closeHauled < body.Steady(FromOffHerBow(100), Still, Yaw, Waterline).Z);
    }

    [Theory]
    [InlineData(0.3)]
    [InlineData(-0.6)]
    public void BracedRoundHerMastHerYardKeepsItsHeightAndItsReach(double yard)
    {
        var end = Sailboat().Sail.Rig.End;
        var (rest, braced) = ((SquareState)end.Pose(0, 0), (SquareState)end.Pose(yard, 0));
        Assert.Equal(rest.ArmPort.Height, braced.ArmPort.Height, 9);
        Assert.Equal((rest.ArmStarboard - rest.ArmPort).Length, (braced.ArmStarboard - braced.ArmPort).Length, 9);
    }
}
