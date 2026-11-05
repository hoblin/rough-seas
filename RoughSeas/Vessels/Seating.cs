using System;
using System.Collections.Generic;
using System.Linq;

namespace RoughSeas.Vessels;

/// <summary>The people aboard a vessel as her hull bears them, while each sits down or stands up.</summary>
public class Seating(double sitDownSeconds, double standUpSeconds)
{
    private readonly Dictionary<long, Sitter> sitters = [];
    private bool weighed;

    public bool Settled(IEnumerable<long> seated, double seconds) =>
        sitters.Values.All(sitter => sitter.Share(seconds, sitDownSeconds, standUpSeconds) is 0 or 1) &&
        seated.ToHashSet().SetEquals(sitters.Where(sitter => sitter.Value.Seated).Select(sitter => sitter.Key));

    public IReadOnlyList<Person> Bear(IEnumerable<Person> seated, double seconds)
    {
        var now = seated.ToDictionary(person => person.Id);
        foreach (var (id, sitter) in sitters.ToList())
        {
            if (sitter.Seated && !now.ContainsKey(id)) sitters[id] = sitter.StandUp(seconds, sitDownSeconds, standUpSeconds);
        }
        foreach (var person in now.Values)
        {
            sitters[person.Id] = sitters.TryGetValue(person.Id, out var sitter)
                ? sitter.SitDown(person, seconds, sitDownSeconds, standUpSeconds)
                : new Sitter(person, true, weighed ? seconds : double.NegativeInfinity);
        }
        weighed = true;

        var borne = new List<Person>();
        foreach (var (id, sitter) in sitters.ToList())
        {
            double share = sitter.Share(seconds, sitDownSeconds, standUpSeconds);
            if (share > 0) borne.Add(sitter.Person.Borne(share));
            else if (!sitter.Seated) sitters.Remove(id);
        }
        return borne;
    }

    private readonly record struct Sitter(Person Person, bool Seated, double Since)
    {
        public double Share(double seconds, double sitDown, double standUp) => Seated
            ? Math.Clamp((seconds - Since) / sitDown, 0, 1)
            : Math.Clamp(1 - (seconds - Since) / standUp, 0, 1);

        public Sitter SitDown(Person person, double seconds, double sitDown, double standUp) => Seated
            ? this with { Person = person }
            : new Sitter(person, true, seconds - Share(seconds, sitDown, standUp) * sitDown);

        public Sitter StandUp(double seconds, double sitDown, double standUp) =>
            new(Person, false, seconds - (1 - Share(seconds, sitDown, standUp)) * standUp);
    }
}

public readonly record struct Person(long Id, double BodyKg, double KitKg)
{
    public Person Borne(double share) => this with { BodyKg = BodyKg * share, KitKg = KitKg * share };
}
