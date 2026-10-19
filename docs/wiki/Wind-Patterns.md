Vanilla gives each map region a wind pattern. Rough Seas sets that pattern from its own wind, so a mod that reads or sets a region's pattern meets ours. This page is for that mod.

## Our patterns

Each map region carries one of our 13 wind patterns, `roughseas-force0` to `roughseas-force12`, one per Beaufort force. They hold the region's wind at its exact strength, renewed every game hour.

## Setting a pattern yourself

**If you set a region's pattern yourself,** with `SetWindPattern` or `/weather setw`, the region is yours until that pattern's `ActiveUntilTotalHours`; then our map takes it back. One limit: when a region is first visited, or after the server restarts, we can't tell your pattern from vanilla's random pick, and we take the region at once.

Turning off `autoChangePatterns` (`/weather acp`) stops us driving regions at all, as it stops vanilla's own rolls.

Setting one of ours by hand works, but the next lease renews it.
