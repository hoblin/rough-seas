Vanilla sets each map region's wind by a wind pattern: a named strength, rolled by chance and held until it runs out. Rough Seas sets that pattern from the wind of its own model instead, so the wind vanilla reports is ours. This page is for a mod that reads or sets a region's pattern.

## Our patterns

We add 13 patterns, `roughseas-force0` to `roughseas-force12`, one per Beaufort force. Each region carries the one for the strongest wind in it, at that wind's exact strength, so a mod that reads the pattern's name learns the force, and one that reads its strength learns the wind.

We hold a region with a lease: we set its pattern to run for one game hour, and every two seconds, while the region still carries the pattern we set, we set it again to the wind of the moment and the hour starts over. So our pattern never runs out on its own, and vanilla's own rolls never reach a region we hold.

Within a region, the wind still differs from place to place: `GetWindSpeedAt` adds our local wind on top of the region's pattern, and gives it our direction ([Reading the weather](Reading-the-Weather)).

## Setting a pattern yourself

**Set any other pattern** with `SetWindPattern` or `/weather setw`, and we leave the region to it until its `ActiveUntilTotalHours`; then we take it back. Another of our own patterns counts as yours, too; only the one already blowing is renewed.

One limit: a lease lasts while vanilla keeps the region in memory, until the region unloads or the server restarts. When a region loads again, we can't tell your pattern from vanilla's random pick, and we take the region at once. A pattern you mean to keep, set again when its region loads.

**To freeze the regions**, turn off `autoChangePatterns` (`/weather acp`). As it stops vanilla's own rolls, it stops our leases and our storms' thunder ([Weather events](Weather-Events)): each region keeps the pattern it has. Our local wind and its direction still come through `GetWindSpeedAt` on top of it.
