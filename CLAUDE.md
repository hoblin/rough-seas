# rough-seas

Rough Seas: a Vintage Story mod that makes the ocean dangerous and worth going out on. Today the sea is flat, safe and empty, and crossing it means holding W on a raft; this mod gives it storms, surf, predators, boats that break, and things to find below.

The design note is `thoughts/shared/notes/2026-10-16/rough-seas-design.md`. It is normative; new work conforms to it. It is a living document: when work shows it wrong or incomplete, say so to the manager, and it gets its next version. The brainstorm it came from sits next to it and holds the reasoning. `thoughts/shared/research/2026-10-16-vanilla-seafaring-and-test-bench.md` maps what vanilla 1.22.7 does at sea and what each idea can build on, with file and symbol references.

No reinvented wheels: what the game already does first, then what Vintage Story's API and asset system offer (JSON assets and patches, behaviours, the mod system), then Harmony patches, then our own code. Anego publishes the source of the API and the vanilla mods (`anegostudios/vsapi`, `vssurvivalmod`, `vsessentialsmod`); read how vanilla does a thing before building beside it. The engine, `VintagestoryLib.dll`, is closed; decompile it with `dotnet ilspycmd` when you need to see what you patch. `bin/ref` fetches all four into the gitignored `ref/`. Read the current API docs (apidocs.vintagestory.at) and the modding wiki before planning; the game changes between versions.

## Write it like our own game

Treat the mod as a game we own, not a pile of patches. Each system (wind, boat damage, swimming stamina, wrecks…) is its own module behind a clear boundary. Each class has one responsibility, a small public interface and private internals; SOLID holds for a small mod as much as for a web backend, and it is what keeps later changes cheap. A system built this way can later leave as a mod of its own by moving its module, not by rewriting it. Swimming stamina is the first candidate.

## Tools first

When a task has no tool that fits, stop and build one: a little time spent now makes this task and every one after it simpler and faster. Keep it in proportion, though. We are building a mod, not a mod-building toolkit, so build the tool a task in front of you needs, not one for a task you imagine.

> A man comes across a woodcutter who has been sawing at a tree for hours and getting nowhere. "Your saw is blunt. Why don't you stop and sharpen it?" "No time. I've got sawing to do."

## Toolchain

All of it is scoped to this repository.

- **The test bench.** `bin/client` and `bin/server` start the game with the mod; each clone of the repository runs its own, side by side. `bin/game` drives the running client from the shell, through the dev-only `AgentBridge` mod: it can run commands, read state, aim, press keys and take screenshots, with no window focus. **`bin/game help` explains all of it**; read it before you first test in the game.
- `RoughSeas/` is the mod; `./build.sh` packages a release into `Releases/`.
- `mise.toml` pins the .NET SDK and Node, and sets the bench's environment: the 1.22.7 install, and this clone's name, data folder and ports. A clone's own values go in its gitignored `mise.local.toml`. mise puts all of it on PATH and in the environment here.
- `ilspycmd` is a local .NET tool (`dotnet-tools.json`): `dotnet ilspycmd <assembly>`.
- The `rea` MCP (Reverse Engineer Anything) inspects .NET assemblies; its skill is `reverse-engineer-anything`.
- The `universal-modder` plugin carries skills for recon, reverse engineering, asset generation and testing in the running game.
- The `fal` MCP generates images, 3D models and sound. Every generation is paid from hoblin's fal balance: generate what a ticket needs, and settle a prompt on a cheap model before running an expensive one.
- The ArtCraft applications (`artcraft` skill: photocraft, vectorcraft and the rest) draw and edit textures and other images by hand. Editing an existing texture is often cheaper and simpler than generating one from scratch.

## Tickets

Tickets are GitHub issues in this repository. A bug or a follow-up found while working a ticket becomes its own issue, not an out-of-scope fix, labelled `agent-filed`. Tickets hoblin and the manager settled together carry `decided`. The two are not equal: a finding from a review subagent sees a slice of the code and imagines what could go wrong; it is filed so it is not lost, and it is read with suspicion before anyone works on it. State the evidence in such an issue (what you saw, where) rather than the fear.

## Done means played

A player meets your work at sea, not in the diff. Before the PR leaves draft, load the mod into the game and see the change happen: the storm rises, the raft breaks, the shark comes. A test that passes is not a mechanic that plays well.

## Review feedback

Copilot reviews our pull requests when asked; outside contributors' pull requests get no automatic review. Copilot's review, like any automated review, is advice, not a gate. You know this code better than the reviewer does, so judge each finding on its merits. Fix it if it is a real defect or a real risk. If it is a nitpick, a minor style point or a speculative edge case, reply in one line saying why the code stays as it is, and resolve the thread. A minor finding worth doing later becomes an issue, not another round.

**Why:** every round of fixes triggers a new review, and that review finds new minor points. Five or six rounds cost hours and tokens and buy nothing.

## Self-review

The `/rpi:feature` flow ends with a multi-agent self-review. A pull request under 1000 lines of diff skips it: Copilot's review is enough for a small change, and five reviewing subagents cost more than they find. Over 1000 lines, run it.

## Merging

hoblin does not review this repository's pull requests. The loop is yours:

1. Mark the PR ready for review and request Copilot's review: `gh pr edit <number> --add-reviewer copilot-pull-request-reviewer`.
2. Wait for the review to land (watch the PR's review comments; a few minutes), then triage every finding as above: fix, or answer and resolve.
3. Push the fixes and request Copilot's review again, the same way.
4. When Copilot's review recommends merging, merge it yourself: squash, with the PR's title and body. The ruleset asks outside contributors for the code owner's approval and lets us past it, but only with `--admin`: `gh pr merge <number> --admin --squash --subject "<title> (#<number>)" --body-file <body file>`.

A PR that conflicts with main gets no CI run and no Copilot review; GitHub queues neither. So before you start waiting for a check or a review, every time, ask GitHub whether the PR still merges: `gh pr view <number> --json mergeable --jq .mergeable` answers `MERGEABLE`, `CONFLICTING` or `UNKNOWN` (still computing; ask again in a few seconds). On `CONFLICTING`, merge main in first (a merge commit, never a rebase or force-push), push, and only then wait.

If Copilot keeps raising new minor points round after round, you and the manager decide together when the PR is done. Copilot helps, but it does not have the last word.
