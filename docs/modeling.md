# Modeling

Read before changing a shape, with `bin/model help`. Shapes are edited in Blender 4.5, headless, through the `phonon/blender-vintagestory-json` add-on. `bin/model` installs the add-on at a pinned commit into the gitignored `.blender/`, applies `bin/lib/vsmodel/addon.patch`, and runs Blender with no window. After changing anything under `bin/lib/vsmodel`, run `bin/model test`.

## Our vessels are built by code

A hull of a few hundred tilted boards is no shape to place by hand, so each vessel of ours has a builder in `bin/lib/hulls/` (Python, no Blender), and `bin/model build [VESSEL...]` writes its shape JSON from it. The builder is the source: change the vessel there and rebuild; an edit made to the JSON alone is lost on the next build. Look at the result with `bin/model preview` and in the game.

- `shape.py` holds the parts, the way vanilla builds its boats: a `Hull` lofted through stations, each station a half-section from keel to sheer, whose strakes are boards nested yaw → pitch → flare like the sailboat's prow chains, with ribs, rails, a transom and the water mask; `beam` and `path` for anything along a line (bundles, logs, booms, oars, stays); `box`, `run` and `group` for the rest. Every face gets an explicit UV at a spot of the texture picked from its element's name, so boards rarely repeat one grain.
- A vessel's bow is at −x: at yaw 0 it moves toward +z, which is model −x, and its front seat sits there. A builder drawn the other way round turns its elements about the hull's middle with `Shape.turn_about` (the currach does).
- The elements the mechanics own (`Hull`, the seats, the outrigger's `LowerMastStorage`) are carried over unchanged from the current file by `Shape.keep`: their bounds are what reach and seats measure. The outrigger keeps vanilla's `SailUnfurled`, `SailHalf` and `SailFurled` names, which `unfurlSails` switches between.
- A hollow hull carries `hideWater`, flat faces just under the rim that the `hidewatersurface` behaviour draws into depth before the sea, as vanilla's sailboat does, so the sea doesn't show inside the boat. The entity and item shapes leave it out with `ignoreElements`. The mask is tessellated a few seconds after the vessel appears; a shot taken sooner still shows water inside.
- `coracle.py` also writes `coracle-back.json`: the same bowl under the backpack step-parent transform the worn shape keeps.
- A hull that holds water carries `Bilge`: eight levels, `Bilge1` to `Bilge8`, from the floor to just under `hideWater`, each sized to the hull at its height. The entity and item leave the group out of their mesh; the bilge draws the one level as full as it is, level with the sea while the hull rolls (`BilgeRenderer`).
- A vessel built in place carries its build stages as groups: `Blank` the wood still uncut, `Char` its charred top and `Fire` the fire burning on it, each with one child per hollowing from the rim down, and `Clay` along the rim, one group. `roughseas:burnout` shows the ones the build is at; the item leaves them out. `dugout.py` draws them.
- The seats (`FrontSeat`, `BackSeat`, the reed boat's `Cargo`) live only in the JSON, kept by `Shape.keep`: move them there, with room for the pose's legs, and check with `bin/scene seats`.
- Every vessel of ours but the dugout, whose rower holds a vanilla oar, moves with its own oars or paddle. Its builder describes their motion as a stroke (`stroke.paddling` for a paddle on one side, its own for the currach's oars and the coracle's sculling), and `stroke.crew` fits the seated player's arms to it: each hand to a grip element, frame by frame, with the engine's own pose math and the seat's pose from the game. The build writes the vessel's animations into its shape, the rower's into `patches/player-<name>.json`, and `<temp>/model/<vessel>-rowed.json`, the vessel with the player seated in it, for `bin/model pose`: look at a stroke there before the game, framed on the rower with `bin/model pose ... Rower`.
- The arms are anatomy (`arm.py`), and know nothing of oars. The hand lands on its grip exactly; what is left free is how far the elbow swings round the line from shoulder to hand, and the arm takes the swing a body would: the shoulder, elbow and wrist inside their measured ranges, the fist closed across the gear's shaft (its x), the arm outside the torso, joint rotations away from the ends of their range, and the arm's own weight carried with the least effort. The build prints how near the hands come to their grips (a miss means the stroke asks more than the arms reach) and every joint pushed past its range or arm through the body, with the frame; move the stroke, not the arm, until it prints none.
- In an animation of version 0 an offset is turned by the element's rotation; `stroke.moved` puts an element at a point in the shape, turned as asked, from where it rests.
- `bin/damage` draws the hull damage overlays, four stages a material, from a seed; `bin/damage PREVIEW.png` lays them over their textures to look at.

## What bin/model guarantees

- A shape that goes through Blender untouched comes back identical: every value, every animation. `bin/model check` proves it for a shape; `bin/model test` for the probe (`bin/lib/vsmodel/test/probe.json`), our vessels, the vanilla boats and a fish.
- Blender plays every animation as the game will, on every frame, within 0.1° and 1/800 block. `bin/lib/vsmodel/pose.py` evaluates poses with the engine's own math (`ShapeElement.GetLocalTransformMatrix`, both animation versions, the keyframe resolution of `Animation` and the keyframe interpolation of `ClientAnimator`); `check` compares Blender's evaluated pose with it, and export refuses an animation that would play otherwise.
- What changed in Blender is exported, and only that. Vanilla shapes in the game install are never written.

## What the add-on gets wrong, and what bin/model does about it

- **Disabled faces and faceless elements** (`Hull`, seat boxes): the add-on makes every element a full cube with six textured faces. In Blender those faces carry the material `vs_disabled`; on export the original faces come back.
- **Values it drops**: `windMode`, `windData`, `autoUv`, `renderInEditor`, `scaleX/Y/Z`, the `editor` block. They come back from the original. Scale does not reach Blender: a scaled element (the coracle's carried `Bowl`) shows unscaled there.
- **Texture paths and sizes**: it writes the texture code as the path, and takes the first image's size, so UVs come out scaled when an image is larger than `textureWidth`. bin/model exports with the shape's own size and keeps its paths. A new material's texture is written as the asset its image file is (`game:block/...`); a code the shape does not define gets no entry.
- **Faces without `uv`**: the engine maps them by the element's size, the add-on by the whole texture. bin/model hands the add-on the engine's UVs.
- **Zero-thickness elements** (sails, `hideWater`, plaques; add-on issue #17): it writes placeholder `#0` faces and wrong UV rotations. A plane's faces always come back from the original; only its geometry is editable.
- **Float noise and equivalent rewrites** (`-1.9` as `-1.899999976`, `rotationY` 180 as -180, a UV flipped and turned 180°): values within 0.001 of the original, compared as the engine draws them, keep the original's spelling. Element and attachment point order is kept too.
- **JSON5 shapes** (unquoted keys, comments): read with the pinned `json5` package; strict JSON goes to the add-on and comes back out.
- **Bugs patched in `addon.patch`**: armature import copied `XZY` Euler angles into `XYZ` pose bones, turning every element rotated about two or three axes; a missing texture image crashed the import (a float image size); the test that a bone's object sits on the bone used an absolute 1e-5 on translations up to a hundred units, so float32 noise renamed elements to `b_<name>`.
- **Animations**: the add-on's keyframe conversion is lost by up to 28° (issue #19), so bin/model converts animations itself (`bin/lib/vsmodel/animation.py`). A bone rests at its element's rotation origin, turned by the element's rotation; for an animation of version 0 a pose bone carries the rotation `R′ = R_e⁻¹·R(e+kf)` and the location `R′·(scale·stretch·offset)`, for version 1 `R(kf)` and `scale·(d+offset) − scale·stretch·R(kf)·d` with `d = from − rotationOrigin`. Between keyframes the game interpolates Euler angles and offsets, Blender its own channels; both directions add keys wherever the two would differ by more than 0.005° or 0.001 units, so they play alike on every frame. A spin across ±180° is exported with `rotShortestDistance`, with keys close enough that each segment turns less than 180°, and with a key at every frame any other element is keyed: there the game takes each element's plain lerp, without the shortest path.

## Limits that stay

- The game runs every animation of an entity at the highest `version` among those playing, so a shape keeps one version: new and changed animations are exported in the version the shape already uses (0 for every vanilla shape).
- An element's `scale` does not reach Blender, so the motion of elements below a scaled one cannot be shown there; `check` names them. A scaled element's own motion is exact for a uniform scale. An animation's non-uniform `stretch` combined with rotation has no exact Blender form; uniform stretch is exact.
- A face added in Blender to a side the original lacks is exported; a plane's faces always come from the original.
- **Renaming** an element or attachment point makes it new: the original's extras (disabled faces, wind, scale) do not follow it, and entity JSON that names it (selection boxes, `stepParentTo`, vanilla's `SailUnfurled`/`SailHalf`/`SailFurled`, `ORIGIN/hideWater`) breaks.
- **Cuboids only**: a mesh that is not 8 vertices and 6 faces is skipped. Rotate in object mode; vertices rotated in edit mode export wrong.
- **Bones without cubes** are not exported; give each bone a cube (a faceless one will do).

## Trying a vanilla shape in the game

A shape under `RoughSeas/assets/game/` does not replace vanilla's on the bench: `bin/client` adds our assets as an origin, and vanilla's `survival` origin loads after it and wins (`AssetManager.GetAssetsDontLoad`). A packaged mod loads after vanilla. To see an edited vanilla shape on the bench, copy it into our domain, prefix its texture paths with `game:`, and point the entity at it with a patch:

```json
[{ "op": "replace", "path": "/client/shape/base", "value": "roughseas:entity/test/sailed", "file": "game:entities/nonliving/boat-sailed.json", "side": "Server" }]
```
