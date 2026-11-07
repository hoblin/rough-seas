using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace RoughSeas.Shipyard;

/// <summary>Where a build stands: what a look at her says of it, and the work it waits on with what that work is done with.</summary>
public sealed record BuildStep(string? Progress, string? Action = null, Func<IEnumerable<ItemStack>>? With = null)
{
    public string Hint => Action == null ? Progress ?? "" : Progress == null ? Lang.Get(Action) : $"{Lang.Get(Action)} ({Progress})";
}
