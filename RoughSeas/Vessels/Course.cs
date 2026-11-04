using System.Collections.Generic;
using System.Linq;

namespace RoughSeas.Vessels;

/// <summary>
/// Where a hull has really been over the last moment, and so how fast she really moves, in m/s. Physics runs its steps
/// in batches under one timestamp, a frame's or a tick's, so a place is timed only once its batch is over, by where
/// the batch left her.
/// </summary>
public class Course
{
    private const long WindowMs = 100;
    private const double TeleportBlocks = 20;
    private const double MillisecondsPerSecond = 1000;

    private readonly Queue<(long Ms, double X, double Z)> track = new();
    private readonly object gate = new();
    private (long Ms, double X, double Z)? batch;

    public (double X, double Z) Velocity
    {
        get
        {
            lock (gate)
            {
                if (track.Count < 2) return (0, 0);
                var (since, fromX, fromZ) = track.Peek();
                var (until, toX, toZ) = track.Last();
                double seconds = (until - since) / MillisecondsPerSecond;
                return ((toX - fromX) / seconds, (toZ - fromZ) / seconds);
            }
        }
    }

    public void Record(double x, double z, long milliseconds)
    {
        lock (gate)
        {
            if (batch is { } last && Jumped(last, x, z))
            {
                track.Clear();
            }
            else if (batch is { } ended && ended.Ms != milliseconds)
            {
                track.Enqueue(ended);
                while (track.Count > 2 && ended.Ms - track.Peek().Ms > WindowMs) track.Dequeue();
            }
            batch = (milliseconds, x, z);
        }
    }

    public void Forget()
    {
        lock (gate)
        {
            track.Clear();
            batch = null;
        }
    }

    private static bool Jumped((long Ms, double X, double Z) last, double x, double z) =>
        (x - last.X) * (x - last.X) + (z - last.Z) * (z - last.Z) > TeleportBlocks * TeleportBlocks;
}
