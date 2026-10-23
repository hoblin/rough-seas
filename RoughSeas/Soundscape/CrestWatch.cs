using System;

namespace RoughSeas.Soundscape;

public sealed class CrestWatch
{
    private double last = double.NaN;
    private double trough = double.NaN;
    private int heading;

    /// <summary>Takes the surface's next height at one point; when a crest has just passed, returns its wave's height over the trough before it.</summary>
    public double? Watch(double height)
    {
        double? crest = null;
        var turn = double.IsNaN(last) ? 0 : Math.Sign(height - last);
        if (turn != 0 && turn != heading)
        {
            if (turn > 0 && heading < 0) trough = last;
            else if (turn < 0 && heading > 0 && !double.IsNaN(trough)) crest = last - trough;
            heading = turn;
        }
        last = height;
        return crest;
    }

    public void Forget() => (last, trough, heading) = (double.NaN, double.NaN, 0);
}
