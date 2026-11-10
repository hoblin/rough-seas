using System;

namespace RoughSeas.Water;

/// <summary>The mean of a square grid over a box around each cell, by a summed-area table.</summary>
public static class BoxAverage
{
    public static float[] Over(float[] values, int size, int radius)
    {
        if (radius <= 0) return values;
        var across = size + 1;
        var totals = new double[across * across];
        for (var row = 0; row < size; row++)
        for (var column = 0; column < size; column++)
        {
            totals[(row + 1) * across + column + 1] = values[row * size + column] + totals[row * across + column + 1] + totals[(row + 1) * across + column] - totals[row * across + column];
        }

        var averaged = new float[values.Length];
        for (var row = 0; row < size; row++)
        for (var column = 0; column < size; column++)
        {
            var (left, right, top, bottom) = (Math.Max(column - radius, 0), Math.Min(column + radius + 1, size), Math.Max(row - radius, 0), Math.Min(row + radius + 1, size));
            var sum = totals[bottom * across + right] - totals[top * across + right] - totals[bottom * across + left] + totals[top * across + left];
            averaged[row * size + column] = (float)(sum / ((right - left) * (bottom - top)));
        }
        return averaged;
    }
}
