using System.Globalization;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace RoughSeas.Person;

public sealed class SwimWarning(PersonModSystem system)
{
    private const long ShownMilliseconds = 5000;

    private string? told;
    private long shownAt;

    public void Tell(Entity swimmer, Refusal? refusal, bool asking)
    {
        if (swimmer.Api is not ICoreClientAPI capi || swimmer != capi.World.Player?.Entity || !asking) return;
        if (refusal is not { } why)
        {
            if (told != null) system.Log(swimmer, "can again: {0} no longer holds", told);
            told = null;
            return;
        }

        var now = capi.InWorldEllapsedMilliseconds;
        if (told == why.Code && now - shownAt < ShownMilliseconds) return;
        if (told != why.Code) system.Log(swimmer, "can't: {0}", why.Log);
        (told, shownAt) = (why.Code, now);
        capi.TriggerIngameError(this, "roughseas-" + why.Code, Lang.Get("roughseas:" + why.Code, why.Args));
    }
}

public readonly record struct Refusal(string Code, string Log, params object[] Args)
{
    public static Refusal TooHeavyToRise(double carriedKg, double shortNewtons) =>
        new("person-too-heavy-to-swim-up",
            string.Format(CultureInfo.InvariantCulture, "too heavy to swim up, carrying {0:F1} kg: the stroke and what it displaces fall {1:F0} N short of its weight", carriedKg, shortNewtons),
            carriedKg.ToString("0.#", CultureInfo.InvariantCulture));
}
