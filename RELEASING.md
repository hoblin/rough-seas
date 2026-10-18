# Releasing

Versions are our own: 1.0, 1.1, 1.2 for features, a third part only for a bug fix (1.0.1). The mod DB accepts only three parts, so `modinfo.json` and the tag carry them: 1.0 is `1.0.0`, tagged `v1.0.0`.

The game version the mod supports lives in one place, `dependencies.game` in `RoughSeas/modinfo.json`. CI builds and tests against that server build, and the mod DB ticks the compatible game versions from it.

## Steps

1. Play the release build first, in a QA session: `docs/qa.md`.
2. In a pull request, set `version` (and `dependencies.game`, when the game moved) in `RoughSeas/modinfo.json`. In the same pull request, check the handbook guide and the wiki (`docs/wiki/`) against what ships, and move what this release brings from the wiki's roadmap into its own section there. Write the changelog as `docs/release/changelog-<version>.html`, and bring the mod page, `docs/release/moddb-page.html`, in line with the release. Both are HTML for the mod DB's editor, which strips scripts, forms and video tags, keeps YouTube embeds and takes about 64 KB; the page stays short and links the wiki for depth. Merge it.
3. Tag the merge commit and push the tag:
   ```sh
   git switch main && git pull
   git tag v1.0.0 && git push origin v1.0.0
   ```
4. The Release workflow checks that the tag matches `modinfo.json` and sits on `main`, runs the tests, builds the zip and creates a draft GitHub Release with it and the generated notes.
5. Read the draft, then approve the `publish` job (the `release` environment). The release goes public.
6. Upload to the mod DB by hand. The site has no upload API and takes forms only from its own pages.
   1. Download `roughseas_<version>.zip` from the GitHub Release.
   2. On https://mods.vintagestory.at, open Rough Seas, then **Edit** → **Add release**.
   3. Drop the zip. The site reads the mod id and version from its `modinfo.json`.
   4. Check the compatible game versions it ticked, paste `docs/release/changelog-<version>.html` into the changelog's source view (`<>`), save.
   5. On the description tab, paste `docs/release/moddb-page.html` into the source view the same way, save.

A release zip without a release: run the CI workflow by hand (**Actions** → **CI** → **Run workflow**, or `gh workflow run ci.yml`) and download the `release` artifact.

## First release only

The mod page does not exist until someone creates it by hand:

1. Search the mod DB for "Rough Seas" to make sure the name and the `roughseas` id are free.
2. Log in at https://mods.vintagestory.at with the game account and click **Submit a mod**: name "Rough Seas", a summary, save.
3. Add the 1.0.0 release as in step 6 above.
4. On the description tab, write the page, credit Mahjong Blonsky with his video (https://youtu.be/NPrPEQdM14I), switch the status from Draft to Published, save.
