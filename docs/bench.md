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
- `/weather setprecip` overrides all our rain, storms' included, and the world keeps it: `bin/scene`'s calm scenes set it to 0. `/weather setprecipa` hands the rain back to our weather.
- Storms pass a spot within minutes of game time: `/time stop` holds them while you look, `/bridge approach` puts you outside one, facing it. Look at the sky and the sea in spectator (`/gamemode spectator`), which draws no player; it picks nothing, so return to creative to board.
- `.surface waves|foam on|off` leaves the sea's waves or foam out, for a side-by-side shot or a frame-time comparison. `.surface reload` recompiles the shader includes after `dotnet build RoughSeas`, without a restart; `.surface trace` logs every vessel's pose each frame, to tell a jump in our pose from a stall in the recording.
- `/bridge force N` finds a sea where the wind and the waves both stand at Beaufort N. Seated, it gives the `/tp` that takes the vessel along: the server can't move a vessel its rider's client steers. After any move, wait half a minute for the chunks before looking or recording, and record over a long take: the bench stalls while chunks load, and a recording caught then looks like our stutter.
- When something fails twice by hand, build the tool before the third try.
