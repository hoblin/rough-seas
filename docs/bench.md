# Testing on the bench

Read before testing in the game, together with `bin/game help`. The bench's defaults carry most of the old rakes: the player arrives flying, restarts save the world, unridden vessels hold still, temporal effects are off, every wait has a timeout. What follows is what no tool can carry.

- To look at anything, fly there: `bin/scene look ID` from any side (`0 6 0.01` looks down from above the water's tint), `/bridge fly` anywhere. Never stand in water or build posts and slabs.
- Leave the desktop alone: no real keys, no window moves, no new outputs (`hyprmoncfg` writes any monitor it sees into hoblin's config). The bench places its own window.
- Modifier clicks don't reach an unfocused client: use `/bridge interact` and `/bridge dismount`.
- `/we` takes coordinates relative to the map centre, fills with the held block and needs creative. `/time set` and `/time add` move storms. `/debug lntest spawn` strikes along the body's yaw, not the camera's.
- After a `/tp`, `~` commands use the client's stale position: `bin/game arrive` first (the bridge's own flights wait by themselves).
- `/bridge sea` fails in a world with no open sea in its generated regions; open another world.
- Spectators don't pick: hover text and entity selection need creative or survival.
- Clean up after each test with `bin/scene clear` instead of opening new worlds; it removes entities only.
- Launching an existing world with `--playStyle` crashed the client on reopen (#24); `bin/client` passes the preset only to new worlds. A crash on a stable build is ours until a vanilla run (`BENCH_MODS=`) reproduces it, and the log names the failing entity.
- `pkill -f "bin/game …"` matches, and kills, your own shell.
- The desktop runs Hyprland with a Lua config: `hyprctl keyword` and the old `dispatch` syntax fail; use `hyprctl eval 'hl.dispatch(hl.dsp.window.move({ ... }))'`. bin/client puts the game on this clone's workspace (`BENCH_WORKSPACE` on `BENCH_MONITOR`), hands focus back, and turns vsync off: a hidden window with vsync gets one frame a second.
- Board a vessel (`/bridge mount ID`) to work on it and leave with `/bridge dismount` (a `/tp` while seated takes the vessel along); tie it with `/bridge tie`, read its ropes with `/bridge ropes`.
- When something fails twice by hand, build the tool before the third try.
