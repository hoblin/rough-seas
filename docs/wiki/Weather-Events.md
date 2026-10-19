Vanilla's weather events (thunder, hail, a light shower and the rest, its own and every mod's) still roll in vanilla's machinery beside Rough Seas: every region rolls one, by weight, now and then. This page is for a mod that adds weather events.

## They fire by their own conditions

Vanilla's roll has two habits that would leave our weather without its events. It reads the rain worldgen laid down for the region, not the rain falling now, so a storm we bring to a dry coast rolls as a dry coast. And it picks by each event's bare weight, not by the conditions the event names, so the calm event, `noevent`, which outweighs the others, wins nearly every roll. Rough Seas changes both:

- the roll reads the current rain;
- it picks by each event's own conditions (`weightFunction`, `minTemp`, `minRain` and so on).

So an event of yours plays wherever its conditions hold against our rain and the live temperature: hail in a cold downpour, thunder in a warm storm, nothing with lightning under a dry, calm sea. You configure it as you would for vanilla, and nothing of ours needs to know of it. [Better and Improved Weather](Other-Mods#better-and-improved-weather-continued)'s events work this way today.

If a game update changes the roll so that our patch no longer fits, we leave the roll as vanilla wrote it and log an error.

## Storms hold their thunder

A storm's thunder shouldn't wait on a lucky roll. Every two seconds we look for the stormiest of five points in each region. Where a storm is strong there and its own event can play by that event's conditions (`heavythunder` for a squall or a hurricane, `lightthunder` for a gale), we set that event and hold it. The hold ends when the storm weakens or moves on, or when the event's conditions stop holding there; the event then plays out its time, and the region rolls as usual again.

While we hold a region's event, an event your mod sets there with `SetWeatherEvent` is replaced within two seconds. Set it where no storm is, or after the storm.

Every event a region takes is logged with the rain and warmth at its centre, in the server's debug log: `grep -a 'weather event'`.
