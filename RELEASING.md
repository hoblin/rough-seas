# Releasing

Versions are our own: 1.0, 1.1, 1.2 for features, a third part only for a bug fix (1.0.1). The mod DB accepts only three parts, so `modinfo.json` and the tag carry them: 1.0 is `1.0.0`, tagged `v1.0.0`.

The game version the mod supports lives in one place, `dependencies.game` in `RoughSeas/modinfo.json`. CI builds and tests against that server build, and the mod DB ticks the compatible game versions from it.

## Steps

1. In a pull request, set `version` (and `dependencies.game`, when the game moved) in `RoughSeas/modinfo.json`. Merge it.
2. Tag the merge commit and push the tag:
   ```sh
   git switch main && git pull
   git tag v1.0.0 && git push origin v1.0.0
   ```
3. The Release workflow checks that the tag matches `modinfo.json` and sits on `main`, runs the tests, builds the zip and creates a draft GitHub Release with it and the generated notes.
4. Read the draft, then approve the `publish` job (the `release` environment). The release goes public.
5. Upload to the mod DB by hand. The site has no upload API and takes forms only from its own pages.
   1. Download `roughseas_<version>.zip` from the GitHub Release.
   2. On https://mods.vintagestory.at, open Rough Seas, then **Edit** → **Add release**.
   3. Drop the zip. The site reads the mod id and version from its `modinfo.json`.
   4. Check the compatible game versions it ticked, paste the GitHub Release notes as the changelog, save.

A release zip without a release: run the CI workflow by hand (**Actions** → **CI** → **Run workflow**, or `gh workflow run ci.yml`) and download the `release` artifact.

## First release only

The mod page does not exist until someone creates it by hand:

1. Search the mod DB for "Rough Seas" to make sure the name and the `roughseas` id are free.
2. Log in at https://mods.vintagestory.at with the game account and click **Submit a mod**: name "Rough Seas", a summary, save.
3. Add the 1.0.0 release as in step 5 above.
4. On the description tab, write the page, credit Mahjong Blonsky with his video (https://youtu.be/NPrPEQdM14I), switch the status from Draft to Published, save.
