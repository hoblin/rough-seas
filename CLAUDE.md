# rough-seas

Rough Seas: a Vintage Story mod that makes the ocean dangerous and worth going out on. Today the sea is flat, safe and empty, and crossing it means holding W on a raft; this mod gives it storms, surf, predators, boats that break, and things to find below.

No reinvented wheels: what the game already does first, then what Vintage Story's API and asset system offer (JSON assets and patches, behaviours, the mod system), then Harmony patches, then our own code. Anego publishes the source of the API and the vanilla mods (`anegostudios/vsapi`, `vssurvivalmod`, `vsessentialsmod`); read how vanilla does a thing before building beside it. The engine, `VintagestoryLib.dll`, is closed; decompile it with `dotnet ilspycmd` when you need to see what you patch. `bin/ref` fetches all four into the gitignored `ref/`. Read the current API docs (apidocs.vintagestory.at) and the modding wiki before planning; the game changes between versions.

## Write it like our own game

Treat the mod as a game we own, not a pile of patches. Each system (wind, boat damage, swimming stamina, wrecks…) is its own module behind a clear boundary. A system built this way can later leave as a mod of its own by moving its module, not by rewriting it. Swimming stamina is the first candidate.

Every new text a player sees ships in every locale the game officially supports (the codes in the install's `assets/game/lang/languages.json`), in that language's nautical terms.

The handbook's guide pages (`RoughSeas/assets/roughseas/config/handbook/`) explain the mod to a player, short and in vanilla's style. The wiki (`docs/wiki/`, published by CI on merge; `bin/wiki-links` checks it) holds the depth the guide leaves out. Both always match what the code does: a pull request that changes what a guide or wiki page describes updates that page, screenshots included, in the same pull request. Before each release both get one more check against what ships, the wiki's roadmap included, to catch what a pull request missed.

A pull request that makes Rough Seas work beside another mod adds or updates that mod's section on the wiki's page of tested mods (`docs/wiki/Other-Mods.md`), not a page of its own, in the same pull request. The section gives the mod's tested version and verdict, and says how the two play together in one mod set: what the other mod does beside ours, what we changed in how it behaves, and what someone building a server or another mod should know. It is written for server owners and modders, and it doesn't cover our own bugs fixed on the way. Otherwise this knowledge lives only in a closed pull request.

## Classes and their interfaces

Each class answers for one thing, and what it knows stays inside it, behind a small public interface. SOLID holds for a small mod as much as for a web backend, and it is what keeps later changes cheap.

A class asks another for what it needs through an interface made for that question, never by reaching into the other's internals. When one class starts knowing another's insides, its logic sits in the wrong place: move it to the class that owns it.

A kind of thing is a type. Its variants are subclasses, with what they share written once in their base, not flags that another class checks. What is general is written apart from the particular things it acts on, so that it can leave as a library of its own; each particular thing brings only its data and what makes it different.

## Physics decides

Every mechanic is derived from physics; that is why the game feels real. Model what physically happens, feed the model measured quantities with their sources, and let the outcome be whatever it gives. Never choose the outcome first and tune a number to reach it.

Randomness is part of the physics too: the real spread of the world the model describes, never a die rolled for variety.

When the game feels wrong, find the wrong input or the missing mechanism. Where nothing was measured for the thing itself, take the closest real analogue that was, and name it beside the number; never stand in an unrealistic model for missing data.

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

**The design.** `docs/design.md` is the project's architecture, and new work conforms to it. Read it in full before any work in this repository.

Only a maintainer changes it. An agent edits it only after a maintainer has explicitly approved that specific change, whatever its size, including a typo or a link. When work shows the design wrong or incomplete, say so to the maintainers. Don't edit it yourself. A pull request from anyone else that touches it fails CI.

Each holds what one kind of work needs; read it in full before that work. They are the rakes stepped on already.

- `docs/modding.md`: before writing mod code (patches, entities, behaviours, physics).
- `docs/vanilla-at-sea.md`: before working on boats, anchors, ropes, accessories or lightning.
- `docs/sea-and-weather.md`: before working on wind, rain, waves, storms, currents or the sea map.
- `docs/bench.md`: before testing in the game, with `bin/game help`.
- `docs/modeling.md`: before changing a shape, with `bin/model help`.
- `docs/qa.md`: before a release's QA session, with `bin/qa help`.

## Tickets

Tickets are GitHub issues in this repository. A bug your own change brought in is fixed in the same pull request. A small thing found along the way is fixed on the spot, in the same pull request. Only a bug that is work of its own, outside the ticket's scope, becomes its own issue, labelled `agent-filed`. Tickets the maintainers settled carry `decided`. The two are not equal: a finding from a review subagent sees a slice of the code and imagines what could go wrong; it is filed so it is not lost, and it is read with suspicion before anyone works on it. State the evidence in such an issue (what you saw, where) rather than the fear.

A `decided` ticket is ready for anyone, an outside contributor included: say on the issue that you take it. The rest wait for the maintainers: `agent-filed` until they confirm it, `draft` and `parked` until they settle.

An outside contributor proposing a wholly new feature, one that's neither in the design nor in a ticket, should consider opening an issue first and starting once a maintainer labels it `decided`. Work started without one may be closed if it goes against the mod's direction.

One ticket ships as one pull request. A big PR is fine here, since Copilot reviews it. When a ticket turns out to hold too much, the extra part becomes its own ticket, agreed with the maintainers; it doesn't become a second PR.

## Done means played

A player meets your work at sea, not in the diff. Before the PR leaves draft, load the mod into the game and see the change happen: the storm rises, the raft breaks, the shark comes. A test that passes is not a mechanic that plays well.

Verify it yourself, through the bench. Never hand an in-game check to a human; ask them for decisions only. When the bench can't drive a check, build the tool. Debug from the log, not the screen: every decision our code makes logs a debug line (`grep -a '\[seas\]\|\[storms\]\|\[vessels\]\|\[shipyard\]\|\[sounds\]\|\[ew\]' "$BENCH_DATA/Logs/server-debug.log"`; what a client decides, such as the sounds it plays itself, is in `client-debug.log`).

## Shipping

A pull request under 1000 lines of diff skips the `/rpi:feature` flow's multi-agent self-review: Copilot's review is enough for a small change, and five reviewing subagents cost more than they find. Over 1000 lines, run it.

Copilot's review, like any automated review, is advice, not a gate. Judge each finding on its merits: fix a real defect or a real risk; answer a nitpick, a style point or a speculative edge case in one line saying why the code stays, and resolve the thread. A small finding worth doing is fixed in the same push, not left for an issue. When Copilot keeps raising new minor points round after round, the maintainers decide when the PR is done.

The maintainers' agents merge their own pull requests; an outside contributor's waits for the code owner's review.

1. Mark the PR ready: `gh pr ready <number>`. Copilot reviews it on its own, a contributor's too, and again after every push.
2. Wait for the review (a few minutes), then triage every finding as above.
3. Push the fixes.
4. When Copilot recommends merging, squash with the PR's title and body. The ruleset lets us past the code owner's approval only with `--admin`: `gh pr merge <number> --admin --squash --subject "<title> (#<number>)" --body-file <body file>`.

A PR that conflicts with main gets no CI run and no Copilot review. Before every wait for a check or a review, ask `gh pr view <number> --json mergeable --jq .mergeable`: `MERGEABLE`, `CONFLICTING`, or `UNKNOWN` (ask again in a few seconds). On `CONFLICTING`, merge main in (a merge commit, never a rebase or force-push), push, and only then wait.
