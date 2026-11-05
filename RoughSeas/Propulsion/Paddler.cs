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
/// that turns her. He finishes the stroke he is in, driving, and then changes sides: one stroke without drive, the paddle
/// carried over the bow while his hands change grips. His strokes run from the moment he took up the side, which a change
/// also starts, so how far into a stroke he is follows from how long he has paddled on it. Told to rest, he finishes the
/// stroke he is in first, and takes up again from where it has got to if told to paddle before it ends.
/// </summary>
public sealed class Paddler(double veerDegrees)
{
    private readonly float veerRadians = (float)(veerDegrees * GameMath.DEG2RAD);
    private float? course;
    private int going;
    private double changing;
    private double onThisSide;
    private bool paddling;
    private bool resting;
    private double strokeSeconds;
    private (Side Side, double Seconds, double Change)? coming;

    public Side Side { get; private set; } = Side.Starboard;

    public bool Changing => changing > 0;

    /// <summary>The side he has decided on and goes over to once his stroke is done.</summary>
    public Side? Coming => coming?.Side;

    /// <returns>Whether he decides to change sides, once his stroke is done.</returns>
    public bool Paddle(int ahead, int turn, float yaw, double strokeSeconds)
    {
        var holding = ahead != 0 && turn == 0;
        (course, going, paddling, resting) = (holding ? (ahead == going ? course : null) ?? yaw : null, ahead, true, false);
        this.strokeSeconds = strokeSeconds;
        var wanted = Wanted(ahead, turn != 0 ? turn : Correction(yaw));
        if (Changing || coming != null || wanted == Side) return false;

        var into = onThisSide % strokeSeconds;
        coming = (wanted, into > 0 ? strokeSeconds - into : 0, strokeSeconds);
        Lap(0);
        return true;
    }

    public void Rest() => (course, coming, resting) = (null, null, true);

    public void Lap(double seconds)
    {
        changing = Math.Max(0, changing - seconds);
        if (paddling)
        {
            var before = onThisSide;
            onThisSide += seconds;
            if (resting && Math.Floor(onThisSide / strokeSeconds) > Math.Floor(before / strokeSeconds)) (paddling, onThisSide) = (false, 0);
        }
        if (coming is not { } next) return;
        var left = next.Seconds - seconds;
        if (left > 0)
        {
            coming = next with { Seconds = left };
            return;
        }
        (Side, changing, onThisSide, coming) = (next.Side, next.Change + left, -left, null);
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
