# rough-seas

Rough Seas: a Vintage Story mod that makes the ocean dangerous and worth going out on. Today the sea is flat, safe and empty, and crossing it means holding W on a raft; this mod gives it storms, surf, predators, boats that break, and things to find below.

No reinvented wheels: what the game already does first, then what Vintage Story's API and asset system offer (JSON assets and patches, behaviours, the mod system), then Harmony patches, then our own code. Anego publishes the source of the API and the vanilla mods (`anegostudios/vsapi`, `vssurvivalmod`, `vsessentialsmod`); read how vanilla does a thing before building beside it. The engine, `VintagestoryLib.dll`, is closed; decompile it with `dotnet ilspycmd` when you need to see what you patch. `bin/ref` fetches all four into the gitignored `ref/`. Read the current API docs (apidocs.vintagestory.at) and the modding wiki before planning; the game changes between versions.

## Write it like our own game

Treat the mod as a game we own, not a pile of patches. Each system (wind, boat damage, swimming stamina, wrecks…) is its own module behind a clear boundary. Each class has one responsibility, a small public interface and private internals; SOLID holds for a small mod as much as for a web backend, and it is what keeps later changes cheap. A system built this way can later leave as a mod of its own by moving its module, not by rewriting it. Swimming stamina is the first candidate.

Every new text a player sees ships in every locale the game officially supports (the codes in the install's `assets/game/lang/languages.json`), in that language's nautical terms.

The handbook's guide pages (`RoughSeas/assets/roughseas/config/handbook/`) explain the mod to a player, short and in vanilla's style. A mechanic's own pull request leaves them alone; they are checked against what ships once before each release. The wiki (`docs/wiki/`, published by CI on merge; `bin/wiki-links` checks it) holds the depth the guide leaves out, and gets the same check before each release, its roadmap included.

## Tools first

When a task has no tool that fits, stop and build one: a little time spent now makes this task and every one after it simpler and faster. Keep it in proportion, though. We are building a mod, not a mod-building toolkit, so build the tool a task in front of you needs, not one for a task you imagine.

> A man comes across a woodcutter who has been sawing at a tree for hours and getting nowhere. "Your saw is blunt. Why don't you stop and sharpen it?" "No time. I've got sawing to do."

## Toolchain

All of it is scoped to this repository. [CONTRIBUTING.md](CONTRIBUTING.md) covers setup, building and testing.

- **The test bench.** `bin/client` and `bin/server` start the game with the mod; each clone of the repository runs its own, side by side. `bin/game` drives the running client from the shell, through the dev-only `AgentBridge` mod: it can run commands, read state, aim, press keys, follow handbook links and take screenshots, with no window focus. **`bin/game help` explains all of it**; read it before you first test in the game.
- `RoughSeas/` is the mod; `./build.sh` packages a release into `Releases/`.
- **Modeling.** `bin/model` edits, previews and checks shape JSON in Blender, headless; **`bin/model help`** explains it.
- `mise.toml` pins the .NET SDK, Node and Blender, and sets the bench's environment: the 1.22.7 install, and this clone's name, data folder and ports. A clone's own values (another install path, ports, a desktop workspace) go in its gitignored `mise.local.toml`. mise puts all of it on PATH and in the environment here; in a shell that doesn't load mise, run tools through `mise exec --`.
- `ilspycmd` is a local .NET tool (`dotnet-tools.json`): `dotnet ilspycmd <assembly>`.

## References

Each holds what one kind of work needs; read it in full before that work. They are the rakes stepped on already.

- `docs/modding.md`: before writing mod code (patches, entities, behaviours, physics).
- `docs/vanilla-at-sea.md`: before working on boats, anchors, ropes, accessories or lightning.
- `docs/sea-and-weather.md`: before working on wind, rain, waves, storms, currents or the sea map.
- `docs/bench.md`: before testing in the game, with `bin/game help`.
- `docs/modeling.md`: before changing a shape, with `bin/model help`.
- `docs/qa.md`: before a release's QA session, with `bin/qa help`.

## Tickets

Tickets are GitHub issues in this repository. A bug or a follow-up found while working a ticket becomes its own issue, not an out-of-scope fix, labelled `agent-filed`. Tickets the maintainers settled carry `decided`. The two are not equal: a finding from a review subagent sees a slice of the code and imagines what could go wrong; it is filed so it is not lost, and it is read with suspicion before anyone works on it. State the evidence in such an issue (what you saw, where) rather than the fear.

One ticket ships as one pull request. A big PR is fine here, since Copilot reviews it. When a ticket turns out to hold too much, the extra part becomes its own ticket, agreed with the maintainers; it doesn't become a second PR.

## Done means played

A player meets your work at sea, not in the diff. Before the PR leaves draft, load the mod into the game and see the change happen: the storm rises, the raft breaks, the shark comes. A test that passes is not a mechanic that plays well.

Verify it yourself, through the bench. Never hand an in-game check to a human; ask them for decisions only. When the bench can't drive a check, build the tool. Debug from the log, not the screen: every decision our code makes logs a debug line (`grep -a '\[seas\]\|\[storms\]\|\[vessels\]\|\[ew\]' "$BENCH_DATA/Logs/server-debug.log"`).

## Shipping

A pull request under 1000 lines of diff skips the `/rpi:feature` flow's multi-agent self-review: Copilot's review is enough for a small change, and five reviewing subagents cost more than they find. Over 1000 lines, run it.

Copilot's review, like any automated review, is advice, not a gate. Judge each finding on its merits: fix a real defect or a real risk; answer a nitpick, a style point or a speculative edge case in one line saying why the code stays, and resolve the thread. A minor finding worth doing later becomes an issue, not another round: every round of fixes brings a new review with new minor points, and five rounds cost hours and buy nothing. When Copilot keeps raising them round after round, the maintainers decide when the PR is done.

The maintainers' agents merge their own pull requests; an outside contributor's waits for the code owner's review.

1. Mark the PR ready and request Copilot: `gh pr edit <number> --add-reviewer copilot-pull-request-reviewer`.
2. Wait for the review (a few minutes), then triage every finding as above.
3. Push the fixes and request Copilot again, the same way.
4. When Copilot recommends merging, squash with the PR's title and body. The ruleset lets us past the code owner's approval only with `--admin`: `gh pr merge <number> --admin --squash --subject "<title> (#<number>)" --body-file <body file>`.

A PR that conflicts with main gets no CI run and no Copilot review. Before every wait for a check or a review, ask `gh pr view <number> --json mergeable --jq .mergeable`: `MERGEABLE`, `CONFLICTING`, or `UNKNOWN` (ask again in a few seconds). On `CONFLICTING`, merge main in (a merge commit, never a rebase or force-push), push, and only then wait.
