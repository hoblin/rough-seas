using System;
using System.Collections.Generic;
using System.Linq;

namespace RoughSeas.Water;

/// <summary>
/// The wave energy over a bed, steady along each train's turning crests: div(E c_g n) = -D, by first-order upwind sweeps.
/// Shoaling and refraction are what this balance does to a train as its group speed and direction change; D is
/// depth-induced breaking (Battjes and Janssen 1978, shared among the trains by their energy as SWAN shares it), with
/// SWAN's limit of the root-mean-square height to what the depth carries, so a wave that breaks over a bar goes on
/// smaller past it. The other end of the gains is <c>seasurface.vsh</c>.
/// </summary>
public static class WaveEnergy
{
    private const int Passes = 6;
    private const int Rounds = 2;
    private const double Relaxation = 0.5;
    private const double NoBreakingBelow = 0.04;
    private const int BreakingSteps = 30;
    private const double QuietestSeaShare = 0.001;

    /// <param name="TurnRadius">The cells to either side over which its direction is averaged: a wave can't turn on a scale shorter than itself, and rays that would cross there spread as diffraction spreads them.</param>
    /// <param name="Entering">Its height over its height in deep water where it enters the grid, by column and row; none enters as from deep water.</param>
    public readonly record struct Train(double NumberX, double NumberZ, double Amplitude, float[] Lag, float[]? Felt = null, int TurnRadius = 0, Func<int, int, double>? Entering = null);

    /// <param name="seaShare">Each cell's sea against the height <paramref name="trains"/> are given at: breaking answers to the sea that stands there.</param>
    /// <param name="spacing">How many blocks a cell is across.</param>
    public static (float[][] Gains, float[] Breaking) Solve(int size, float[] depth, IReadOnlyList<Train> trains, double depthPerHeight, double dissipation, float[]? bedDepth = null, float[]? seaShare = null, double spacing = 1)
    {
        var cells = size * size;
        var standing = bedDepth ?? depth;
        var solved = trains.Select(train => new Solved(train, size, depth, trains.Sum(other => other.Amplitude * other.Amplitude / 2), spacing)).ToArray();
        var loss = new double[cells];
        var limit = new double[cells];
        for (var i = 0; i < cells; i++) limit[i] = standing[i] > 0 ? Math.Pow(standing[i] / depthPerHeight / Math.Max(seaShare?[i] ?? 1, QuietestSeaShare), 2) / 8 : 0;
        var breaking = new float[cells];

        for (var pass = 0; pass < Passes; pass++)
        {
            foreach (var train in solved) train.Sweep(size, depth, loss, limit);
            var settled = true;
            for (var i = 0; i < cells; i++)
            {
                if (!(depth[i] > 0)) continue;
                double energy = 0, weighted = 0;
                foreach (var train in solved)
                {
                    energy += train.Energy[i];
                    weighted += train.Energy[i] * train.Frequency;
                }
                foreach (var train in solved) train.Portion[i] = energy > 0 ? train.Energy[i] / energy : train.Portion[i];
                var share = energy / limit[i];
                var fraction = BrokenFraction(share);
                breaking[i] = (float)fraction;
                var target = energy > 0 && fraction > 0 ? dissipation * (weighted / energy) * fraction / (Math.PI * share) : 0;
                settled &= target == loss[i];
                loss[i] += (target - loss[i]) * Relaxation;
            }
            if (settled) break;
        }
        return (solved.Select(train => train.Gains()).ToArray(), breaking);
    }

    public static double BrokenFraction(double share)
    {
        if (share <= NoBreakingBelow) return 0;
        if (share >= 1) return 1;
        double low = 0, high = 1;
        for (var step = 0; step < BreakingSteps; step++)
        {
            var middle = (low + high) / 2;
            if (1 - middle + share * Math.Log(middle) > 0) high = middle;
            else low = middle;
        }
        return (low + high) / 2;
    }

    private sealed class Solved
    {
        private readonly double[] speed, alongX, alongZ, entering;
        private readonly double deepEnergy, deepFlux, spacing;

        public double[] Energy { get; }

        public double[] Portion { get; }

        public double Frequency { get; }

        public Solved(Train train, int size, float[] depth, double totalEnergy, double spacing)
        {
            this.spacing = spacing;
            var felt = train.Felt ?? depth;
            var deepNumber = Math.Sqrt(train.NumberX * train.NumberX + train.NumberZ * train.NumberZ);
            var cells = size * size;
            (Energy, speed, alongX, alongZ, entering) = (new double[cells], new double[cells], new double[cells], new double[cells], new double[cells]);
            Frequency = WaveTheory.DeepFrequency(deepNumber);
            deepEnergy = train.Amplitude * train.Amplitude / 2;
            Portion = new double[cells];
            Array.Fill(Portion, totalEnergy > 0 ? deepEnergy / totalEnergy : 0);
            deepFlux = deepEnergy * (deepNumber > 0 ? 0.5 * Math.Sqrt(WaveTheory.Gravity / deepNumber) : 0);
            if (deepNumber <= 0) return;
            for (var i = 0; i < cells; i++)
            {
                if (!(depth[i] > 0)) continue;
                speed[i] = WaveTheory.GroupSpeed(deepNumber, felt[i]);
                var (x, z) = (i % size, i / size);
                var towardX = train.NumberX + PhaseField.Slope(train.Lag, depth, size, x, z, 1, 0) / spacing;
                var towardZ = train.NumberZ + PhaseField.Slope(train.Lag, depth, size, x, z, 0, 1) / spacing;
                var length = Math.Sqrt(towardX * towardX + towardZ * towardZ);
                if (length > 0) (alongX[i], alongZ[i]) = (towardX / length, towardZ / length);
                if (x > 0 && z > 0 && x < size - 1 && z < size - 1) continue;
                var gain = train.Entering?.Invoke(x, z);
                entering[i] = gain is { } share ? deepEnergy * share * share * speed[i] : deepFlux;
            }
            if (train.TurnRadius <= 0) return;
            var smoothX = BoxAverage.Over(alongX.Select(value => (float)value).ToArray(), size, train.TurnRadius);
            var smoothZ = BoxAverage.Over(alongZ.Select(value => (float)value).ToArray(), size, train.TurnRadius);
            for (var i = 0; i < cells; i++)
            {
                var length = Math.Sqrt(smoothX[i] * smoothX[i] + smoothZ[i] * smoothZ[i]);
                if (depth[i] > 0 && length > 0) (alongX[i], alongZ[i]) = (smoothX[i] / length, smoothZ[i] / length);
            }
        }

        public void Sweep(int size, float[] depth, double[] loss, double[] limit)
        {
            if (deepFlux <= 0) return;
            for (var round = 0; round < Rounds; round++)
            {
                Pass(size, depth, loss, limit, 0, size, 1, 0, size, 1);
                Pass(size, depth, loss, limit, size - 1, -1, -1, 0, size, 1);
                Pass(size, depth, loss, limit, 0, size, 1, size - 1, -1, -1);
                Pass(size, depth, loss, limit, size - 1, -1, -1, size - 1, -1, -1);
            }
        }

        public float[] Gains()
        {
            var gains = new float[Energy.Length];
            if (deepEnergy <= 0)
            {
                Array.Fill(gains, 1f);
                return gains;
            }
            for (var i = 0; i < gains.Length; i++) gains[i] = (float)Math.Sqrt(Energy[i] / deepEnergy);
            return gains;
        }

        private void Pass(int size, float[] depth, double[] loss, double[] limit, int fromX, int toX, int stepX, int fromZ, int toZ, int stepZ)
        {
            for (var z = fromZ; z != toZ; z += stepZ)
            for (var x = fromX; x != toX; x += stepX)
            {
                var i = z * size + x;
                if (!(depth[i] > 0)) continue;
                var (acrossX, acrossZ) = (Math.Abs(alongX[i]), Math.Abs(alongZ[i]));
                var inflow = Inflow(size, depth, x - Math.Sign(alongX[i]), z, alongX, alongX[i], speed[i], entering[i]) + Inflow(size, depth, x, z - Math.Sign(alongZ[i]), alongZ, alongZ[i], speed[i], entering[i]);
                var outflow = speed[i] * (acrossX + acrossZ) + loss[i] * spacing;
                Energy[i] = Math.Min(outflow > 0 ? inflow / outflow : 0, limit[i] * Portion[i]);
            }
        }

        private double Inflow(int size, float[] depth, int x, int z, double[] along, double here, double speedHere, double enteringFlux)
        {
            if (here == 0) return 0;
            if (x < 0 || z < 0 || x >= size || z >= size) return enteringFlux * Math.Abs(here);
            var i = z * size + x;
            if (!(depth[i] > 0)) return deepEnergy * speedHere * Math.Abs(here);
            return Math.Sign(along[i]) == Math.Sign(here) ? Energy[i] * speed[i] * Math.Abs(along[i]) : 0;
        }
    }
}
