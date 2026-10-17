# Testing on the bench

Read before testing in the game, together with `bin/game help`. The bench's defaults carry most of the old rakes: the player arrives flying, restarts save the world, unridden vessels hold still, temporal effects are off, every wait has a timeout. What follows is what no tool can carry.

- To look at anything, fly there: `bin/scene look ID` from any side (`0 6 0.01` looks down from above the water's tint), `/bridge fly` anywhere. Never stand in water or build posts and slabs.
- Leave the desktop alone: no real keys, no window moves, no new monitors or outputs. The bench places its own window.
- Modifier clicks don't reach an unfocused client: use `/bridge interact` and `/bridge dismount`.
- `/we` takes coordinates relative to the map centre, fills with the held block and needs creative. `/time set` and `/time add` move storms. `/debug lntest spawn` strikes along the body's yaw, not the camera's.
- After a `/tp`, `~` commands use the client's stale position: `bin/game arrive` first (the bridge's own flights wait by themselves).
- `/bridge sea` fails in a world with no open sea in its generated regions; open another world.
- Spectators don't pick: hover text and entity selection need creative or survival.
- To see a handbook page: `bin/game hotkey handbook` opens the handbook, then `bin/game link handbook://PAGECODE` and `bin/game shot FILE gui`; `bin/game scroll N` brings the end of a long page into view. The list stays empty while the handbook builds its pages, for half a minute after the world loads, and when a link opens a closed handbook: wait, open it by hotkey, and search again.
- The wiki's recipe images come from the handbook: `bin/scene recipes docs/wiki/images/recipes` retakes them all. A changed recipe is retaken this way, and a new one gets its line in that command's list first.
- Clean up after each test with `bin/scene clear` instead of opening new worlds; it removes entities only.
- Launching an existing world with `--playStyle` crashed the client on reopen (#24); `bin/client` passes the preset only to new worlds. A crash on a stable build is ours until a vanilla run (`BENCH_MODS=`) reproduces it, and the log names the failing entity.
- `pkill -f "bin/game …"` matches, and kills, your own shell.
- bin/client turns vsync off: a hidden window with vsync gets one frame a second. On Hyprland (Lua config) with `BENCH_WORKSPACE` set in `mise.local.toml`, it also puts the game on that workspace (on `BENCH_MONITOR`, when set) and hands focus back.
- Board a vessel (`/bridge mount ID`) to work on it and leave with `/bridge dismount` (a `/tp` while seated takes the vessel along); tie it with `/bridge tie`, read its ropes with `/bridge ropes`.
- `/bridge set ID tree/key VALUE` reaches into a tree and keeps the type it finds: `roughseas:vessel/integrity 0.4` for a hull at a stage, `roughseas:bilge/litres 450` for a swamped one.
- `bin/scene seats` seats the player in every seat of every vessel and shoots each from the side, in third person; `/bridge mount ID SEAT` takes one seat. The camera mode outlives a shot and a restart: after third-person work, `cyclecamera` back to first person before `look` or `photo`.
- An accessory goes on with `/bridge interact ID interact ctrl BOX`, BOX being its selection box. A basket attaches only with its type: `/giveblock stationarybasket-east 1 <player> {type:"reed"}`.
- A bowl held over the sea fills from it, and a full bowl bails nothing: `/clearinv` and a fresh bowl before a bailing test, aimed into the boat.
- An open handbook pauses the game, and a paused client answers no `entity` request: `bin/game close`.
- When something fails twice by hand, build the tool before the third try.
