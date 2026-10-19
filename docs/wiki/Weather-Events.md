Vanilla's weather events (thunder, hail, a light shower and the rest, its own and every mod's) still roll in vanilla's machinery beside Rough Seas. This page is for a mod that adds weather events.

## They fire by their own conditions

Vanilla's event roll runs as usual. Rough Seas fixes two things in it:

- It reads the current rain instead of the rain worldgen laid down.
- It picks by each event's own conditions (`weightFunction`, `minTemp`, `minRain` and so on), not by its bare weight.

So an event of yours plays wherever its conditions hold against our rain and the live temperature: hail in a cold downpour, thunder in a warm storm, nothing with lightning under a dry, calm sea. There is no list of other mods' events in our code. [Better and Improved Weather](Other-Mods#better-and-improved-weather-continued)'s events work this way today.

## Storms hold their thunder

Where a storm covers a region and the storm's own event can play there (`heavythunder` or `lightthunder`), we hold that event for as long as the storm does. Vanilla's calm event outweighs all the others, so without the hold a storm would rarely thunder.

Every event a region takes is logged with the rain and warmth at its centre, in the server's debug log: `grep -a 'weather event'`.
