using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace RoughSeas.Soundscape;

/// <param name="files">From the smallest splash to the fullest.</param>
public sealed class SplashPool(IReadOnlyList<AssetLocation> files, double fullFromMetres)
{
    public int Count => files.Count;

    public AssetLocation? Pick(double waveHeight, Random rand, AssetLocation? previous = null)
    {
        if (files.Count == 0) return null;

        var aim = (int)Math.Round(Math.Clamp(waveHeight / fullFromMetres, 0, 1) * (files.Count - 1));
        var near = Enumerable.Range(aim - 1, 3).Where(i => i >= 0 && i < files.Count && !files[i].Equals(previous)).ToArray();
        return files[near.Length > 0 ? near[rand.Next(near.Length)] : aim];
    }
}
