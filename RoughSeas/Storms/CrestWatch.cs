using System;

namespace RoughSeas.Storms;

public sealed class CrestWatch
{
    private double last = double.NaN;
    private double trough = double.NaN;
    private int heading;
    private double waveCrest, waveTrough = double.NaN;
    private bool aboveStill;

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

    /// <summary>
    /// Takes the surface's next height above still water, counting whole waves as oceanographers do, from one crossing of
    /// still water to the next: when the surface has just fallen through it, returns that wave's height, its highest crest
    /// over the lowest trough before it. The short trains' ripples on a longer wave are not waves of their own.
    /// </summary>
    public double? WatchWave(double height)
    {
        if (height >= 0)
        {
            waveCrest = aboveStill ? Math.Max(waveCrest, height) : height;
            aboveStill = true;
            return null;
        }
        double? wave = aboveStill && !double.IsNaN(waveTrough) ? waveCrest - waveTrough : null;
        waveTrough = aboveStill || double.IsNaN(waveTrough) ? height : Math.Min(waveTrough, height);
        aboveStill = false;
        return wave;
    }

    public void Forget()
    {
        (last, trough, heading) = (double.NaN, double.NaN, 0);
        (waveCrest, waveTrough, aboveStill) = (0, double.NaN, false);
    }
}
