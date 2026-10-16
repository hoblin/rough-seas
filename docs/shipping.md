# Shipping a pull request

Read when a pull request is ready to leave draft: review, self-review and merging.

## Review feedback

Copilot reviews our pull requests when asked; outside contributors' pull requests get no automatic review. Copilot's review, like any automated review, is advice, not a gate. You know this code better than the reviewer does, so judge each finding on its merits. Fix it if it is a real defect or a real risk. If it is a nitpick, a minor style point or a speculative edge case, reply in one line saying why the code stays as it is, and resolve the thread. A minor finding worth doing later becomes an issue, not another round.

**Why:** every round of fixes triggers a new review, and that review finds new minor points. Five or six rounds cost hours and tokens and buy nothing.

## Self-review

The `/rpi:feature` flow ends with a multi-agent self-review. A pull request under 1000 lines of diff skips it: Copilot's review is enough for a small change, and five reviewing subagents cost more than they find. Over 1000 lines, run it.

## Merging

The maintainers' agents run this loop themselves; an outside contributor's pull request waits for the code owner's review instead.

1. Mark the PR ready for review and request Copilot's review: `gh pr edit <number> --add-reviewer copilot-pull-request-reviewer`.
2. Wait for the review to land (watch the PR's review comments; a few minutes), then triage every finding as above: fix, or answer and resolve.
3. Push the fixes and request Copilot's review again, the same way.
4. When Copilot's review recommends merging, merge it yourself: squash, with the PR's title and body. The ruleset asks outside contributors for the code owner's approval and lets us past it, but only with `--admin`: `gh pr merge <number> --admin --squash --subject "<title> (#<number>)" --body-file <body file>`.

A PR that conflicts with main gets no CI run and no Copilot review; GitHub queues neither. So before you start waiting for a check or a review, every time, ask GitHub whether the PR still merges: `gh pr view <number> --json mergeable --jq .mergeable` answers `MERGEABLE`, `CONFLICTING` or `UNKNOWN` (still computing; ask again in a few seconds). On `CONFLICTING`, merge main in first (a merge commit, never a rebase or force-push), push, and only then wait.

If Copilot keeps raising new minor points round after round, the maintainers decide when the PR is done. Copilot helps, but it does not have the last word.
