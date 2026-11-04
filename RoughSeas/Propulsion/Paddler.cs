using System;
using Vintagestory.API.MathTools;

namespace RoughSeas.Propulsion;

public enum Side
{
    Starboard = -1,
    Port = 1
}

/// <summary>
/// The side a single blade works on. Paddled on one side she turns away from it, so the paddler holds a course by
/// changing sides once she has veered off it by <paramref name="veerDegrees"/>, and turns her by paddling on the side
/// that turns her. A change of sides takes one stroke, the paddle carried over the bow while her hands change grips.
/// </summary>
public sealed class Paddler(double veerDegrees)
{
    private readonly float veerRadians = (float)(veerDegrees * GameMath.DEG2RAD);
    private float? course;
    private double changing;
    private double onThisSide;

    public Side Side { get; private set; } = Side.Starboard;

    public bool Changing => changing > 0;

    /// <returns>Whether she starts changing sides.</returns>
    public bool Paddle(int ahead, int turn, float yaw, double strokeSeconds)
    {
        course = ahead == 0 || turn != 0 ? null : course ?? yaw;
        var wanted = Wanted(ahead, turn != 0 ? turn : Correction(yaw));
        if (Changing || wanted == Side) return false;

        (Side, changing, onThisSide) = (wanted, strokeSeconds, 0);
        return true;
    }

    public void Rest() => (course, onThisSide) = (null, 0);

    public void Lap(double seconds)
    {
        changing = Math.Max(0, changing - seconds);
        onThisSide += seconds;
    }

    public double Strokes(double strokesPerMinute) => onThisSide * strokesPerMinute / 60;

    public double OffCourseDegrees(float yaw) => course is { } held ? GameMath.AngleRadDistance(held, yaw) * GameMath.RAD2DEG : 0;

    private Side Wanted(int ahead, int toward) =>
        toward == 0 ? Side : (Side)(-toward * (ahead < 0 ? -1 : 1));

    private int Correction(float yaw)
    {
        var off = course is { } held ? GameMath.AngleRadDistance(held, yaw) : 0;
        return Math.Abs(off) > veerRadians ? -Math.Sign(off) : 0;
    }
}
