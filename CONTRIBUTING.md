# Contributing to Rough Seas

Thank you for wanting to help. This page shows how to set up, build and test the mod, and how a change gets in.

## Set up

You need:

- Vintage Story **1.22.7**, the version in `dependencies.game` of `RoughSeas/modinfo.json`.
- The **.NET 10 SDK**.
- [mise](https://mise.jdx.dev) (recommended). It installs the pinned SDK and sets the environment from `mise.toml`. Run `mise trust` once in the clone.

The build finds the game through `VINTAGE_STORY`. The default is `~/.local/share/vintagestory-1.22.7`. If your install is elsewhere, put the path in a gitignored `mise.local.toml` at the root of the clone:

```toml
[env]
VINTAGE_STORY = "/path/to/vintagestory"
```

Without mise, export `VINTAGE_STORY` in your shell instead.

## Build and test

```sh
dotnet build RoughSeas      # the mod, into RoughSeas/bin/Debug/Mods
dotnet test RoughSeas.Tests # unit tests
./build.sh                  # a release zip in Releases/
```

`bin/ref` fetches the sources of Vintage Story's API and vanilla mods into `ref/`, and decompiles the engine there too. Read how vanilla does a thing before you build next to it.

## Play it

Unit tests do not tell you whether a storm feels right. A change is done when you have seen it happen in the game.

The test bench in `bin/` runs the game with the mod and drives it from the shell. It needs Linux with `tmux`, `jq`, `curl` and ImageMagick, and a game account that has logged in once with the launcher. Its first run copies that login from `~/.config/VintagestoryData`.

```sh
bin/client      # build, start the game in tmux, open a test world
bin/game help   # everything the bench can do
```

`bin/game` talks to the dev-only `AgentBridge` mod. Through it you can run commands, read the game's state, aim, press keys and take screenshots. [docs/bench.md](docs/bench.md) lists what to look out for. On a Hyprland desktop, set `BENCH_WORKSPACE`, and optionally `BENCH_MONITOR`, in `mise.local.toml`. The game window then opens on that workspace and stays out of your way.

Without the bench, run `./build.sh` and drop the zip from `Releases/` into your game's `Mods` folder.

## Propose a change

1. Open an issue first, or comment on an existing one. Say what you want to change and why, so we can agree on it before you spend time on code. An issue labelled `decided` is settled and ready to take; say on it that you're taking it. The others (`agent-filed`, `draft`, `parked`) aren't ready yet: ask on the issue first. Proposing a wholly new feature, one that's neither in [the design](docs/design.md) nor in an issue? Consider opening an issue first, and start once we label it `decided`. If you start without one, be ready for the feature to be closed if it goes against the mod's direction.
2. Branch from `main`. Keep one issue to one pull request.
3. Open a pull request against `main`. Title it like `30 feat: in-game guide`: the issue number, then a [Conventional Commits](https://www.conventionalcommits.org) type. Link the issue in the description.

## What a pull request needs

- **CI passes.** The workflow runs the unit tests and builds the release zip.
- **It was played.** Say in the description what you did in the game and what you saw.
- **Every text a player sees is in every locale** the game supports: the codes in the install's `assets/game/lang/languages.json`. Use that language's own nautical terms, not a word-for-word translation.
- **No reinvented wheels.** Use what the game already does first. Then JSON assets and patches, behaviours and the mod system. Then Harmony patches. Write new code last.
- **Clean modules.** Each system has a clear boundary and a small public interface. Numbers go in JSON assets, not in code.
- **A maintainer approves it.** The code owner reviews outside contributions before merge.

The full set of conventions is in [CLAUDE.md](CLAUDE.md), and the `docs/` folder holds the lessons learned the hard way. Both are written for AI coding agents and people alike. If you work with an agent, point it at them.

The player's [wiki](https://github.com/hoblin/rough-seas/wiki) is written in `docs/wiki/` and published there on every merge to `main`, so change it by pull request, not on the wiki. Pages link each other by name (`[Storms](Storms)`); `bin/wiki-links` checks that every link and image resolves.

## Licence

By contributing, you agree that your work is released under the [MIT licence](LICENSE).
