# rough-seas

Rough Seas: a Vintage Story mod that makes the ocean dangerous and worth going out on. Today the sea is flat, safe and empty, and crossing it means holding W on a raft; this mod gives it storms, surf, predators, boats that break, and things to find below.

The brainstorm is `thoughts/shared/notes/2026-10-16/rough-seas-brainstorm.md`: the origin video, every idea so far, the survey of existing mods, the tooling. Read it in full before planning. A design note will follow it, and once it exists it is normative.

No reinvented wheels: what the game already does first, then what Vintage Story's API and asset system offer (JSON assets and patches, behaviours, the mod system), then Harmony patches, then our own code. Anego publishes the source of the API and the vanilla mods (`anegostudios/vsapi`, `vssurvivalmod`, `vsessentialsmod`); read how vanilla does a thing before building beside it. The engine, `VintagestoryLib.dll`, is closed; decompile it with `dotnet ilspycmd` when you need to see what you patch. Read the current API docs (apidocs.vintagestory.at) and the modding wiki before planning; the game changes between versions.

## Toolchain

All of it is scoped to this repository.

- `mise.toml` pins the .NET SDK and Node; mise puts them on PATH here.
- `ilspycmd` is a local .NET tool (`dotnet-tools.json`): `dotnet ilspycmd <assembly>`.
- The `rea` MCP (Reverse Engineer Anything) inspects .NET assemblies; its skill is `reverse-engineer-anything`.
- The `universal-modder` plugin carries skills for recon, reverse engineering, asset generation and testing in the running game.
- The `fal` MCP generates images, 3D models and sound. Every generation is paid from hoblin's fal balance: generate what a ticket needs, and settle a prompt on a cheap model before running an expensive one.

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
4. When Copilot's review recommends merging, merge it yourself: squash, with the PR's title and body.

A PR that conflicts with main gets no CI run and no Copilot review; GitHub queues neither. So before you start waiting for a check or a review, every time, ask GitHub whether the PR still merges: `gh pr view <number> --json mergeable --jq .mergeable` answers `MERGEABLE`, `CONFLICTING` or `UNKNOWN` (still computing; ask again in a few seconds). On `CONFLICTING`, merge main in first (a merge commit, never a rebase or force-push), push, and only then wait.

If Copilot keeps raising new minor points round after round, you and the manager decide together when the PR is done. Copilot helps, but it does not have the last word.
