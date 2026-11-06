Rough Seas keeps one model of the world's wind, waves, storms and currents, computed on the server from the world's own climate and ocean map. The rest of the game meets that model in three ways, and a mod meets it in the same three.

![Rough Seas lies under vanilla: the world's ocean map and climate at the bottom, Rough Seas' map on the server above them, vanilla's weather machinery driven by it, and other mods reading vanilla as usual at the top; clients read the map the server sends](images/diagrams/under-vanilla.svg)

- **Through vanilla.** We don't replace the game's weather; we drive it. Each region's wind pattern, the rain and the weather events still come out of vanilla's machinery, set from our model. So a mod written for plain Vintage Story reads our weather without a line about us. A mod that moves a lever vanilla itself has, drying the world or shifting its temperature, gets the answer vanilla would give; a mod that sets the weather in vanilla's place changes what vanilla reports, not our sea ([Reading the weather](Reading-the-Weather#what-moves-our-sea)).
- **Through our own code.** Vanilla has no place for the wind's direction, for waves or for currents. We publish them from our own types, on both sides: the sea state, and on a client the drawn sea, its picture of the waves.
- **Through behaviours.** A vessel takes our sea by carrying our entity behaviours, configured in JSON: it floats by its mass, ships water, cracks, floods, moors and drifts. No code.

## The pages

Each page covers one part, for the mod that touches it; read the ones yours does.

| Your mod | Read |
| --- | --- |
| reads the wind, rain or temperature, or changes them | [Reading the weather](Reading-the-Weather) |
| adds weather events | [Weather events](Weather-Events) |
| sets a region's wind pattern | [Wind patterns](Wind-Patterns) |
| reshapes the oceans, or calls vanilla's ocean layer | [The ocean map](The-Ocean-Map) |
| reads the wind's direction, waves, storms or currents: a helm, a sail, a fish | [Reading the sea](Reading-the-Sea) |
| poses a vessel of its own on the swell a client draws | [Reading the sea](Reading-the-Sea#the-drawn-sea) |
| adds a vessel, or gives ours its behaviours | [Building a vessel](Building-a-Vessel) |

Where a page touches physics, it says what the mod does and links [The science behind it](The-Science-Behind-It) for why.

## What we keep

What these pages document is what we keep: vanilla's channels as they describe them, `ISeaWeather` with `SeaState`, `SeaCurrent` and `SeaClock`, `IDrawnSea` with `SeaPose` and `HullResponse`, and our vessel behaviours with their keys and their configs. It follows semantic versioning: a change that breaks any of it comes with a new major version and a line in the release notes, and what we add comes in a minor version. If you build on it, or need something it doesn't give, tell us on the [issue tracker](https://github.com/hoblin/rough-seas/issues).

### Hulls that crack, leak and flood

Hulls that crack, leak and flood changed the vessel keys, so they came as a major version. What a vessel of yours built before them needs to change:

- `roughseas:vesselintegrity` loses `wearPerDay`: a reed hull's [`soak`](Building-a-Vessel#the-hull) and a cracking hull's `seep` take its place. A mend's `restores` now closes that much of one place's crack, or of a lashed hull's lashing; with `greases`, it is the share of `seep.greaseDays` it gives back.
- `roughseas:bilge` loses `sinks`: a swamped vessel sinks by its [buoyancy](Building-a-Vessel#floating).
- `roughseas:seaforces`' `freeboardMetres` counts only for a vessel that doesn't float by its mass, one with no `depthMetres` on its `vesselintegrity`.
- `config/vessels/impact.json` is now `config/vessels/mass.json`, with `crewKg` added; a patch to it follows the rename.
- A carried vessel's hull travels on its item as the stack attribute `roughseas:vessel`; `roughseas:worn` is gone.

New: the hull `model`, `parts[].share`, `depthMetres`, `waterplaneSquareMetres`, `seep`, `soak`, the `density` of a material, `config/vessels/hull.json`, the `response` block of `config/storms.json`, and [`HullResponse`](Reading-the-Sea#how-a-hull-answers). A world from before keeps its vessels: a raft takes its old condition as its lashing, a reed boat as water soaked up, and a cracking hull starts sound; a vessel carried as an item does the same from its durability.

### Blows by their physics

In 3.1 every blow meets what really takes it ([The hull](Building-a-Vessel#the-hull)). No key was removed, so a vessel of yours loads as it did, but it fares differently:

- `impactCapacity` is what a broad load (a knock, a ram, rapids, a blast) must reach to break the hull up, and a smaller one cracks its place by its share; on a `reed` hull too, which now tears.
- Two hulls with `vesselintegrity` ram each other; `minImpactSpeed` is the speed they take in their stride.
- Without a `shell`, the frame's capacity resists point blows and cuts too, and breakers, now a pressure against the planking, don't harm the hull. Give it a [`shell`](Building-a-Vessel#the-hull) for both.
- `Struck`'s second value is the blow over what resisted it, which is the shell for a point blow, a cut or a breaker, no longer always the hull's capacity. Its knock is `Harmed` only when the place came to a worse stage.

New: the vessel's `shell`, `config/vessels/damage.json`'s `broadSources`, `cutShare` and `variation`, a material's `toughness` and `cutToughness`, and `hull.json`'s `soakedStrengthShare`. Gone, never documented here: `config/storms.json`'s `waves.breakerTransfer`, `waves.periodSecondsPerRootMetre` and `ashore.impactFactor`.

### One sea, one clock

In 3.2 every client draws the same waves, and everything at sea keeps one clock ([The sea's time](Reading-the-Sea#the-seas-time)). A vessel of yours loads as it did, but code that reads the time changes:

- `ISeaWeather.NowHours` gave way to `ISeaWeather.Clock`, a `SeaClock`: its `Hours` is the old value. A mechanic of yours at sea that keeps a watch of it stands still and races with the calendar, as ours do.
- `IDrawnSea.Seconds` is the waves' time, the same on every client, so a mod that rides our swell needs no clock of its own.
- Waves come aboard, bilges fill and leaks run by game time, so a vessel near a sleeping player lives through the night.

Gone, never documented here: `config/storms.json`'s `lightning.checkSeconds`, and in `config/soundscape.json` the `water.lapping` loop and `shore.lappingSound` and `lappingStrength`. A hull's splash is `water.splash` now, and the slaps take a `volumePerMetre`.

### Heave by mass

In 4.0 a vessel with a draft heaves by her own mass ([Floating](Building-a-Vessel#floating)), and what she drags with her comes from her form. A vessel of yours loads as it did; code that reads the heave changes:

- `HullResponse.HeavePeriod(draftMetres, config)` gave way to `HullResponse.HeavePeriod(kg, addedKg, kgPerMetre)`; `HullResponse.Of` keeps its arguments.
- `config/storms.json`'s `response.heaveAddedMass` is gone: `HeaveAddedMass` gives the water from a hull's waterplane.
- `IBuoyant.SettlingMetres` is gone; `ReferenceDraftMetres`, `KgPerMetre` and `AddedKg` are new. `IOutboard` adds `WaterplaneSquareMetres` and `LengthMetres`.
- Vanilla's `swimmingOffsetY` now sets her keel: vanilla's `Swimming` means her keel is wet.

### Dugouts built in place

The dugout is burnt and hewn out of five logs on the shore ([Built in place](Building-a-Vessel#built-in-place)), and its grid recipes are gone. No key was removed, so a vessel of yours loads as it did.

- The dugout's rower holds what drives her, as on vanilla's raft: she sets `requiresPaddlingTool`, and her strokes are her own for each tool ([Paddles, poles and oars](#paddles-poles-and-oars)).
- `roughseas:bilge`'s `capacityLitres` is what the finished hull holds; while she is built, she holds what is cut out of it.

- An axe starts it: Shift + right-clicked on the middle log of a row, it lays the blank. Anywhere else an axe does what vanilla does.
- Every axe in the game, a modded one too, carries our collectible behaviour `roughseas:hew`, so that holding it on a blank strips its bark and on a charred blank hews.
- Rain on a vessel, in her bilge and drying her ashore, is what `WeatherSystemBase.GetPrecipitation` reads, as vanilla's fires do, and nothing under a roof.

New: the behaviour `roughseas:burnout` with its keys, the collectible behaviour `roughseas:hew`, and the watched tree `roughseas:build` on a vessel being built.

### Bark canoes framed in place

The bark canoe is stripped off a standing tree and framed where it lies ([Framed in place](Building-a-Vessel#framed-in-place)), and its grid recipe is gone. No key was removed, so a canoe of yours loads finished, and the bark from a log still mends it.

- The bark canoe's rower holds what drives her, as the dugout's does: she sets `requiresPaddlingTool`, and her paddler kneels. Her own built-in paddle is gone; her strokes are her own for each tool ([Paddles, poles and oars](#paddles-poles-and-oars)).
- A knife held on a standing birch, elm or spruce trunk strips it: the trunk becomes our `roughseas:debarkedtrunk-{wood}`, which keeps the grown log's tree-felling group, its spread and its hold on the leaves, and drops vanilla's debarked log. A mod that looks for `log-grown-*` to find a trunk misses a stripped one.
- Rope held on a canoe still being lashed lashes her instead of tying itself to her: a Harmony prefix on `ItemRope.OnHeldInteractStart`. Once her lashing is done, rope ties to her as before.
- Rope and every liquid container in the game, a modded one too, carry our collectible behaviour `roughseas:framing`, and our laths carry it by their JSON, so that holding them on a canoe being built lays her next piece.
- A vessel picked up keeps her build on the item, as she keeps her hull's condition, under the stack attribute `roughseas:build`; the item of a framed vessel carries our collectible behaviour `roughseas:carriedbuild`, which draws her as far as she is built and in her laths' wood.

New: the behaviour `roughseas:framing` with its keys, the collectible behaviour of the same code, the items `roughseas:lath-{wood}-{dry|wet}` and the blocks `roughseas:wedge-{we|ns}` and `roughseas:debarkedtrunk-{wood}`.

### Outriggers rigged on a dugout

The outrigger canoe is a finished dugout rigged where she lies ([Rigged in place](Building-a-Vessel#rigged-in-place)), and its grid recipes are gone. Her code is still `roughseas:outrigger-{wood}`, the wood her hull's, so an outrigger of yours loads finished, her float and poles of that wood.

- Her hull is the dugout's: its shape, shell, collision boxes, seats, bilge and `impactCapacity`. Her washstrakes and platform, sawn planks, are gone, and with them her old `hullBox`. A patch that addressed her old shape's elements or her selection boxes by index misses: her boxes are the dugout's three, then `LowerMastAP`.
- Her rower holds what drives her, as the dugout's does: she sets `requiresPaddlingTool`, and her strokes are her own for each tool ([Paddles, poles and oars](#paddles-poles-and-oars)). Her own built-in paddle is gone.
- She mends with a log of her hull's wood, and breaks into firewood, a rope and linen.
- A pole held on a finished dugout with a row of three logs alongside replaces the dugout with a new outrigger entity: a mod that keeps a dugout's entity id sees it die (`EnumDespawnReason.Removed`). Her hull's cracks and bilge go across through `IHandedOver`; ropes tied to the dugout do not.
- `roughseas:framing`'s `framePart` now names a part by its new `name` key, not by its material: the bark canoe's frame part is `"name": "frame"`.
- Axes and cloth carry our collectible behaviour `roughseas:framing` too, as rope does.
- The wedge's `laths` and `lath` attributes are now `splits`: what a row of so many wedged logs splits into, five into laths, three into poles.

- `IHullForm` grew `Wrecked`, `UnhewnLitres`, `DepthMetres` and `WaterplaneSquareMetres`, and `Hollowed` became `HoldLitres`: what a vessel being built breaks into, the wood she still carries outside her hull, and her depth, waterplane and hold so far. A vessel with a mast that stands only once stepped implements the new `IMasted`. An implementation of yours returns the designed values to keep its behaviour.
- The dugout is the five logs from first to last: 4.5 m long, 0.9 m wide, 0.56 m deep, a 1298-litre hold and 252 kg in oak, against 400 kg and 450 litres before. Her shape's stage groups are each level's whole trunk, `Clay{n}` each fire's rim, and `Shell` the finished hull; the item hides the stages as before. A dugout of yours loads as she was, at her new size.
- The outrigger takes optional washstrakes, sawn planks: `roughseas:framing`'s stages may be `optional` and raise her side and hold. One saved without a build has them on.

New: the behaviours `roughseas:riggable` and `roughseas:rigging` with their keys, `roughseas:framing`'s works `pole`, `hew`, `cloth` and `plank` and its keys `replaces`, `optional`, `raisesMetres`, `raisesLitres`, `part` and `texture`, `roughseas:burnout`'s keys `blankDepthMetres`, `blankWaterplaneSquareMetres` and `unhewnLitres`, a part's `name` and `takesKnocks`, and the items `roughseas:pole-{wood}`.

### Reed boats bound in place

The reed boat is bound from two reed bundles laid side by side ([Bound in place](Building-a-Vessel#bound-in-place)), and its grid recipe is gone. Her code is still `roughseas:reedboat`, so a reed boat of yours loads finished, with her ends bound up and her soak as it was.

- She is a float of two 19 cm bundles: 3 m by 0.36 m, 0.19 m deep, 50 kg, her rider 5 cm above the water. Her shape, collision boxes and seat height changed, and her `Hull`, `FrontSeat` and `Cargo` elements with them; a patch that addressed her old shape's `Port`, `Starboard`, `Core` or `Binding` elements misses. Her `soak` and the `reed` material's density changed with her ([why](The-Science-of-the-Hull)).
- Her rower holds what drives her: she sets `requiresPaddlingTool`, and her strokes are her own for each tool ([Paddles, poles and oars](#paddles-poles-and-oars)). Her own built-in paddle is gone.
- `roughseas:framing` no longer needs a vessel of a wood, and strips a tree only when it has `barkSeconds`.
- The help text `roughseas:shipyard-lash` now reads "Lash her with rope" on every framed vessel.

New: the block `roughseas:reedbundle` of class `roughseas:BlockBundles` ([its codes](#reed-boats-of-the-plant-they-were-bound-from)) and the behaviour `roughseas:droop`.

### Coracles and currachs laid out on the ground

The coracle and the hide currach are laid out as laths on the ground and built where they lie ([Laid out on the ground](Building-a-Vessel#laid-out-on-the-ground)), and their grid recipes are gone. Their codes are still `roughseas:coracle` and `roughseas:currach`, so one of yours loads finished, the currach the right way up, both covered in prepared hide.

- The coracle's built-in sculling paddle is gone: she sets `requiresPaddlingTool`, and her sculler holds a paddle. The currach sets it `false` and is rowed with her own oars ([The currach rowed facing her stern](#the-currach-rowed-facing-her-stern)). Their strokes are their own for each tool ([Paddles, poles and oars](#paddles-poles-and-oars)).
- Their shapes are new: the coracle a Teifi basket of a lattice and a plaited gunwale, 1.5 by 1.07 m, with no seat board; the currach double-ended, 4.76 by 1.2 m, with ribs, stringers and a thwart pole in place of her transom, floor and stem post. A patch that addressed their old elements misses. Their bilges hold 272 and 1911 litres, and they weigh 17 and 88.5 kg.
- Laths and poles have a length: `roughseas:lath-{wood}-{2|3|4|5}-{dry|wet}` and `roughseas:pole-{wood}-{2|3|4|5}`, no longer `lath-{wood}-{dry|wet}` and `pole-{wood}`. A row of two to five wedged logs splits into 64 poles as long as it; the wedge's `splits` list one per row length. An axe splits a pole into 16 laths of its length in the crafting grid (`recipes/grid/laths.json`). Their name takes the length as `{1}`.
- Every piece of a `lath` or `pole` stage names its length in the shape, a `"metres"` key on its group, and takes a lath or pole at least that long. The bark canoe's and the outrigger's pieces name theirs too.
- `seep`'s `litresPerHour` is now `litresPerHourASeam`: a cover seeps at every seam between hides. A cracking hull's new `cover` gives the hides of one from before its build; `config/vessels/hides.json` gives each hide's keeping afloat and the water it takes up, and a patch can change them.
- `IAfloat.Pass` takes the water's temperature, and a hull whose cover is sewn on her as she is built implements the new `ICovered`.
- Hides carry our collectible behaviour `roughseas:framing`, as rope does; laths carry `roughseas:lathbed` by their JSON, and with Shift lay themselves on the ground. Rope held on laths laid out in a layout binds them rather than doing what vanilla's rope does.

New: the behaviour `roughseas:covering` with its keys `layout`, `overturned` and `overturnedPivot`, `roughseas:framing`'s work `hide` and its keys `bends`, `hides` and `hideStates`, the block `roughseas:lathbed`, the collectible behaviour `roughseas:lathbed`, `roughseas:vesselintegrity`'s key `cover`, the interface `ICovered`, and `config/vessels/hides.json`.

### Every build without a jump, every hull by its model

Each vessel built in place now changes the way the work would, and floats by numbers measured off her model ([why](The-Science-of-the-Hull#floating)).

- The weights, depths, waterplanes, holds, decks and `swimmingOffsetY` of the dugout, outrigger, bark canoe, coracle and currach changed. The dugout's walls are 5 cm and her blank is the square trunk: `unhewnLitres` 3329, `blankWaterplaneSquareMetres` 5, `hollowDepthMetres` 0.5125, `hollowBeamMetres` 0.8. A patch that set these numbers sets them against her new model.
- The outrigger's float is hewn from its three logs in six passes: her stage `Ends` and the groups `Float` and `Blunt` are gone, and `Hewing` (a `successive` stage replacing `FloatLog`) comes after `Booms`, whose pieces follow it (`follows`). Her washstrakes' end pieces are boards across her hollow, and the washstrakes add 422 litres, her measured opening 15 cm higher. The bark canoe's and the currach's holds stop at the lowest point of their gunwale, where water first spills over: 609 and 1911 litres. Her texture codes `floatbark` and `floatrings` draw the logs, in the float's wood. A dugout off the block grid is squared onto the logs when rigged, turned and moved so the float lies on them, no end of it more than half a block; one turned further from the row isn't rigged.
- The bark canoe's sheet is 5 m long. Her stages are `Frame` (inwales and thwarts), `Slashes` and `Ends` (knife cuts, work `cut`) and `Outwales`, before her stems; all four lift her `Skin` group, one child a level, `Skin1` to `Skin13`, and her frame pieces show from `Frame{n}At{k}` and `Outwales{n}At{k}`. The finished canoe's item is drawn by `roughseas:carriedbuild` too, which shows only her last step.
- The coracle's lattice is spaced 0.125 m and 0.1875 m, the currach's pair 1.075 m, both within 0.0625 m; the currach's gunwale pieces show from `Gunwale{n}At{k}`, her stern ends apart until her second end lashing.
- A hide bigger than what is left of a cover gives back the rest as smaller hides of its kind. `Laying.Spend` has an overload taking the pieces the work used.
- A reed boat's ends sag over the soak she takes before she sinks with nothing aboard, not over her full saturation. The interface `ISoaking` is gone; `EntityBehaviorVesselIntegrity.SoakToSinkKg` gives that soak.
- A refused piece logs a `[shipyard]` line once a hold.

New: `roughseas:framing`'s stage keys `successive`, `follows` and `lifts` and its work `cut`, the shape key `depthMetres` on a lifted group's steps, `roughseas:rigging`'s key `floatStage`, the shape keys `woodLitres` and `waterlineSquareMetres`, and `bin/lib/hulls/physics.py`, which prints what each builder measures.

### Paddles, poles and oars

A paddled, poled or rowed vessel goes as fast as her rower drives her against the water's drag on her ([Driving her](Building-a-Vessel#driving-her)), and the tools that drive her are items of their own.

- Our vessels no longer carry `speedMultiplier` or `turnSpeed`: their speed and turn come from our behaviour `roughseas:propulsion`, which vanilla's raft gets by patch. A Harmony postfix on `EntityBoat.SeatsToMotion` hands any boat that carries it to the behaviour; vanilla's sailboat and every other mod's boat move as before. Under sail, vanilla's motion stands.
- While someone holds her controls and she is not under sail, the behaviour's own drag takes the water's place along the surface: a prefix on `EntityBehaviorPassivePhysics.MotionAndCollision` gives back what vanilla's water drag takes from her horizontal motion, and leaves it on her vertical motion, which it keeps damping. `WaterDragValue` itself stays vanilla's.
- What the rower spends is the behaviour's `EffortWatts`, the power his body gives while he strokes, and `Stroke`, the stroke he makes; until a stamina of our own takes it, the effort costs him satiety.
- Vanilla's crude oar carries `roughseas:propulsion` as a 2 m pole, and its `tpHandTransform` is ours, so it lies in the hand where our strokes put a pole.
- Each vessel's rower has strokes of his own for each tool, `roughseas-{vessel}-{pole|paddle}-{ready|forward|backward}`, in `patches/player-{vessel}.json`, the raft's too; they key `ItemAnchor` to hold the tool where the stroke puts it. The currach's are `roughseas-currach-{ready|forward|backward}` again, with her own oars and thole pins.
- Every vessel of ours has an `Oar Storage` slot, its box `OarAP` after her others; the coracle takes `rideableaccessories` for it.
- The pole is drawn at its length: `shapeByType` takes `item/pole-{2|3|4|5}` and its attributes are by type. The shape `item/pole` is gone.
- The currach's build ends with a stage `Oars` of the new work `oar`, two `roughseas:tholeoar-{wood}`, and her `parts` gain `oars`; `roughseas:covering`'s new key `oars` names what that stage takes. With them she weighs 88.5 kg.
- The hull's drag law, `HullDrag` with `WaveMaking`, lives with the hull, and a vessel's size for it is her `Hull` box, which a client now measures once her selection boxes load rather than keeping her entity's selection box for good.

New: the behaviour `roughseas:propulsion` with its keys, the items `roughseas:paddle-{wood}` and `roughseas:tholeoar-{wood}`, the item attribute `roughseas:propulsion`, `config/propulsion.json` and `config/vessels/drag.json`.

### Damage drawn in the model

Our vessels draw each place's crack in their own model, from a scratch to a hull stove in, and an outrigger's float takes the knocks on its side on its own lashings ([Building a vessel](Building-a-Vessel#the-hull)). A vessel of yours loads as it did, its overlay still drawn from `damage.textures`.

- Our dugout, outrigger, bark canoe, coracle and currach dropped `damage.textures`: their wounds are `Wound{Place}…` groups in their shapes, and a patch to their shapes must keep them. Only `wood` and `reed` overlays are left; the `bark` and `hide` ones are gone.
- A knock on an outrigger's float side no longer cracks her starboard side: it goes to `IOutboard`, `roughseas:floatlashing` on ours, whose lashings live in `roughseas:vessel/float`. So do a blast's and lightning's broad load landing outside her hull on that side; a blade or a club there only chips the float. An outrigger saved before has her lashings whole.
- Once those lashings give out the outrigger is replaced by a dugout (`EnumDespawnReason.Removed`), as rigging replaced the dugout before. Her riders keep their seats; what her accessory slots held drops beside her.
- `HullDamage.Visible` is `Overlaid`, true only while an overlay is drawn.
- Two vessels meet where either one's `IHull.Reach` touches the other's: her hull out to what stands off it, so a vessel that crosses only an outrigger's float rams the float. The knock's direction is still taken from the hulls, or toward the outboard when only it was met.
- `HullShape.PointOf` sets each port place a little toward the bow and each starboard place toward the stern, so a leak no longer starts at the mirror of the other side's.

New: `IOutboard` with `OffMetres`, `IHull.Reach`, `HullShape.Spanning`, `EntityBehaviorVesselIntegrity.Impact`, `HullDamage.Hidden`, the behaviour `roughseas:floatlashing` with its keys, and `roughseas:rigging`'s `lashing`.

### Paddling on both sides

A paddle works on one side and turns her away from it, and the paddler changes sides to hold a course or to turn her ([Driving her](The-Science-of-the-Hull#driving-her)). A vessel of yours loads as it did.

- A paddled vessel's `strokes` hold `paddle-starboard` and `paddle-port` in place of `paddle`, each with a fourth animation, `change`, that carries the paddle over to that side: `roughseas-{vessel}-paddle-{starboard|port}-{ready|forward|backward}` and `roughseas-{vessel}-paddle-to-{starboard|port}`. A vessel whose `strokes` keep a bare `paddle`, the coracle sculling over her bow, is paddled on no side. A patch that played or replaced `roughseas-{vessel}-paddle-{ready|forward|backward}` misses.
- Her new key `paddleAbeamMetres` is how far out from her middle the blade drives: its thrust turns her by that lever. Without it a paddle drives her straight, as before.
- The side is decided on the server, which syncs it to every client in her watched attributes `roughseas:paddleSide` (−1 starboard, 1 port) and `roughseas:paddleChanging`.
- `HullDrag.Turning` takes her speed through the water: under way a hull resists turning in proportion to it, so every vessel turns on a wider circle while she makes way than before, and spins on the spot as before. `Propulsor.Push` takes the `Side` the tool works on.
- `config/propulsion.json`'s `paddling` stroke gains `veerDegrees`, how far off her course she goes before the paddler changes sides.

New: the enum `Side`, the class `Paddler`, the keys `paddleAbeamMetres` and `veerDegrees`, and the watched attributes `roughseas:paddleSide` and `roughseas:paddleChanging`.

### Poled standing

- `roughseas:propulsion`'s `handsAboveWaterMetres` moved into its new `pole` object, with the `stroke` the pole makes and the `stance` the rower takes to work it ([Driving her](Building-a-Vessel#driving-her)). A patch that set the old key sets nothing.
- With a pole in hand, the rower's seat changes while he holds her controls: its `RiderOffset`, `Animation` and `EyeHeight` become the stance's, and go back when he puts the pole away, leaves her or someone else takes her. Only the seat of whoever holds her controls changes. A mod that reads a seat's config off one of our vessels reads the stance while he poles.
- One owner now writes a seat's pose, `SeatPoses` in `RoughSeas.Vessels`. A swamped hull's hold outranks a stance, and a stance outranks the seat as built.
- New player animation `roughseas-stand`, which the stance stands the rower in. The pole strokes key his legs and hips too, `UpperFootR/L`, `LowerFootR/L` and `LowerTorso`.
- `config/propulsion.json` has a second pole stroke, `seatedpole`, for a vessel too tender to stand on. `pole` is the standing one, at a gross efficiency of 0.134.

New: the `pole` key with `stroke`, `handsAboveWaterMetres` and `stance`, the stroke `seatedpole`, the animation `roughseas-stand`, `SeatPoses`, `bin/lib/hulls/leg.py` and `physics.stands`, which decides from a hull whether her poler stands, and the bench's `bin/scene shallows`.

### The currach rowed facing her stern

Her rower sits where a real Aran rower does: facing her stern, pulling his oars on pins that stand just abaft his thwart.

- Our vessels' seats are `VesselSeat`, vanilla's boat seat turned by its `mountRotation`: the rider's body, head and camera face that way. Vanilla turns only the limits a body is held in. The currach's seat has `mountRotation` y 180; a pole's `stance` takes a `mountRotation` of its own, so her poler stands facing her bow.
- Her thwart, seat and thole pins moved: the `BackSeat` box is at x 2.12 to 10.12, its point on the thwart; the cargo boxes lie two forward of the rower and one aft of his feet. Her `spanMetres` is 4.12 and her `bladeSquareMetres` 0.057, the blade under the water and her `sweepMetres` 1.3.
- Her oars pivot on their pins through the bull on the loom's after side, the loom lying on her gunwale, their looms reach from the pin to her middle, and their blades stand upright, face to the water they push.

New: `RoughSeas.Vessels.VesselSeat`, and a seat's `mountRotation` read by `bin/lib/hulls/stroke.py`.

### Every box inside the round it builds

Our vessels' shapes are rebuilt so that no box stands outside the round it builds and no two faces share a plane. A vessel of yours loads as it did; a patch that addresses their elements by name should look again.

- The coracle's bottom strips, `Bottom{Bow|Stern}{n}`, are inscribed in her bottom and joined to her sides by `Floor{n}` facets, one under each side board. Her bottom reaches round her outermost laths, her laths lie inside her hide, and her binding ties the 27 crossings where both laths lie flat: still three ropes. Her hold is 272 litres.
- A hull lofted through stations (the currach, bark canoe, dugout and outrigger) takes more stations toward her ends, so her boards' names run on through them, and each tapering panel takes a second board, `{name}Top`.
- The currach's and the bark canoe's ends are as broad as what they close round: the currach is 4.76 m long, with a 1911-litre hold; the bark canoe's stem stands 5 cm higher inside her bark, and her lashing takes 21 ropes.
- The dugout's stepped ends are no wider than the hull they continue, her lower ends narrowest: she is 252 kg of oak, with 3329 litres hewn away (`unhewnLitres`).
- A reed bundle and the outrigger's float are as thick as their round: the bundles are 19 cm across, and the float is 196 litres, 132 kg in oak, which makes the outrigger 491 kg.

### Bundles that crush, knocks that happen once

- A knock against a block is read as vanilla reads a fall: on the step a collision first blocks an axis of her motion, at the speed her position really changed by over the tenth of a second before, not from `Pos.Motion`, which a steered vessel's rider rewrites each frame. A vessel held against a rock, by a current or her rider paddling on, strikes it once. A ram's closing speed comes from both hulls' real change of position too. `IHull.Velocity` gives a hull's, in m/s, kept by `Course`; `Impact.Step` takes it in place of her motion's history, and `Contact`'s closing speed is in m/s.
- A `bundle` shell takes a broad load by crushing its stems first: `Resistance.CushionJoules`, from the material's new `crushStrength` and `densificationStrain`, across the bundle's `metres` along one metre of it. Only what is left meets `impactCapacity`. Our reed boat's `impactCapacity` is now her lashings' work to break, 30,600 J. A `bundle` shell of yours on our `reed` row crushes too, and takes knocks it used to tear from.
- The bench's `/bridge current SPEED YAW` sets one current everywhere in place of the sea's, to carry an unridden vessel as a river would.

New: a material's `crushStrength` and `densificationStrain`, `Resistance.CushionJoules` and `Resistance.Severity`.

### Framed hulls take a knock where it lands

- A `shell` can name its `frame`, `{ "material", "metres", "widthMetres", "spacingMetres", "spanMetres" }`: the laths or ribs behind it. A broad load (a knock, a ram, rapids, a blast, lightning) on such a shell meets only the shell where it lands and the members there, `Resistance.Framed`: on a member (`Resistance.Backed` rolls it by `MemberShare`), a `rigid` shell's holing with its lining, nothing for a `skin`, plus the member's `toughness` across its section; between two, both members, and a `skin`'s stretch across the gap until its pull breaks them, from a material's new `tensileStrength` and `elongation`. The members' breaking brings the place to a split (`HullModel.FrameBroken`, a crack of 0.5), twice that stoves it in, and only what is left meets `impactCapacity` to break the hull up; a load that neither opens the shell nor breaks a member leaves the place whole. A shell without a `frame`, or a hull with no `impactCapacity`, takes a broad load as before.
- Our coracle, bark canoe and currach carry a `frame`, a 1 by 6 cm lath of their build's wood, and feel a knock from about 1.2 m/s with one aboard where they used to shrug off 2 m/s. A patch that removes the `frame` gives them the whole-frame knock back.
- `Resistance.Loss` and `Resistance.BreaksFrame` are now the resistance's own, and `BreaksFrame` takes the blow's severity, and whether it landed on a member, rather than its loss. `Resistance.Backed` takes the blow.

- While she is built, a broad load meets only the frame members laid: `roughseas:framing`'s stages marked `members` lay them, and `Shell.Framed` spaces them wider while some are missing.

New: a shell's `frame` (`FrameMember`), `Resistance.Framed` (`FramedResistance`), a material's `tensileStrength` and `elongation`, `HullModel.FrameBroken`, a framing stage's `members`.

### Reed boats of the plant they were bound from

A reed bundle keeps the plant it was bound from, and the boat each of her bundles. A world's reed boats and bundles from before load as cattail, through the engine's remap files.

- The bundle is `roughseas:reedbundle-{port}-{starboard}-{axis}`, its `count` gone: `starboard` is `none` while it lies alone. Each plant is the code of the vanilla tops item it was bound from, `cattailtops`, `papyrustops` or `thatch`, and the grid recipe makes the bundle of the tops it takes.
- The boat is `roughseas:reedboat-{port}-{starboard}`, her item the same; the creative inventory lists the three of one plant. Bound from two plants, each side is its own. Her wreckage is a handful of each side's tops, `game:{port}` and `game:{starboard}`.
- Her shape draws each bundle in its own texture, `port` and `starboard`, and her bound ends in `tips`: the port plant's reed with the starboard plant's strands (`reed-{plant}-strands`) laid over it. The textures `roughseas:entity/vessel/reed-{plant}` replace `reed`, and her `damage` marks all three.
- A W–E pair binds into a boat turned half round from before (`yaw` 3π/2, was π/2), so each bundle stays on the side it lay.

New: `Reeds`, the plants a vessel's bundles were bound from, and its name.

### The raft paddled kneeling

Vanilla's raft is paddled from her side, at her own size. A vessel of yours loads as it did.

- The raft's `Hull` box is her logs, 3.88 by 1.32 m, where it reached her rope wraps' corners, 4.25 by 1.88; the bamboo raft's is 4.19 by 1.32, where it was 4.19 by 1.63. Her drag, reach, the places a knock lands and her heave all read it, and through her beam so do the sea's pull on her mooring, the water she ships and her roll: a moored raft holds about ten times longer in a gale.
- `roughseas:seaforces`'s `beamMetres` may be left out: the hull then takes her `Hull` box's width, so a vessel's beam lives in one place. The new `capsizeBreakerBeams` gives the capsizing breaker as a share of that beam. The raft carries neither `beamMetres` nor `capsizeBreakerMetres` now. `EntityBehaviorSeaForces.BeamMetres` reads her hull each time, and `Seaworthiness.From` takes the beam.
- `roughseas:propulsion` takes ahead and turning from the rower's keys, not from vanilla's motion: a paddled boat that carries it no longer goes astern while her rider looks astern. Vanilla's raft is one of them.
- The raft's seats hold the body to her heading, `bodyYawLimit` 0.4, and her rower kneels, `mountAnimations.idle` `roughseas-kneel`. Her paddle strokes key `LowerTorso` and `UpperTorso` as well: he shifts toward the side he paddles and bends his trunk to it. Her pole's stance stands him half a metre toward the side he poles.
- In `bin/lib/hulls`, `seat` reads a seat where the game seats a rider, through the elements it hangs from, and `paddling` takes a trunk `bend`, a `shift` of the hips toward the working side and the `middle` both sides mirror about.

New: `capsizeBreakerBeams`, `EntityBehaviorPropulsion.Asked`.

### Every vessel's parameters in one place

`bin/model build` now writes every value it measures off a vessel's model into her own JSON, in metres with the voxels beside them ([Measured from the model](Building-a-Vessel#measured-from-the-model)): our vessels' entity files, and for vanilla's raft and sailboat our patches. A vessel of yours loads as it did, unless it set one of the keys renamed below.

- `roughseas:vesselintegrity`'s `hullBox`, in the shape's voxels, is gone. `hullBoxMetres`, `{ "from", "to" }` in metres from the point the entity stands at, gives the hull box instead; without it the hull is measured on her `HullAP` element at runtime, as before. A vessel of yours that set `hullBox` is now measured on that element.
- `roughseas:covering`'s `overturnedPivot`, a height in voxels, is now `overturnedPivotVoxels`, a point `[x, y, z]` on the shape that the line she is turned about runs through.
- `roughseas:bilge` takes `levelHeightsMetres`, the height of each level of water her shape draws; the renderer no longer reads them from the shape, so a bilge of yours without it shows no water.
- `roughseas:rigging` takes `floatLengthMetres`, the float's length for the water it drags as she heaves; before, the float's count of logs stood in for it.
- `roughseas:seaforces` takes `capsizeBreakerLengths`, the capsizing breaker as a share of her hull box's length. The sailboat's is 0.55, from the Wolfson Unit's trials: 4.25 m on her 7.72 m, where it was 3.3. The dugout, the bark canoe and the outrigger give `capsizeBreakerBeams` 1 in place of a breaker in metres. Our vessels leave `beamMetres` out, but for the outrigger, whose measured 2.57 m spans hull and float.
- Our vessels' `hitboxSize` is their hull's width and height, and their `collisionBoxes` their hull box in pieces, with the outrigger's float. The raft's box is measured from vanilla's logs, 3.88 by 1.32 m, and the sailboat's from vanilla's planking, 7.72 by 2.5 m.
- Measured values that changed: the sailboat's hold 1500 to 8209 litres, her deck 8 to 15.1 m², her waterplane 3 to 8.98 m² and her mast 6 to 7.0 m; windage, in m², the dugout 0.6 to 1.93, the currach 0.9 to 2.54, the bark canoe 0.5 to 1.41, the outrigger 4 to 2.76, the sailboat 6 to 11.46, the raft 1 to 0.35, the reed boat 0.3 to 0.18 and the coracle 0.5 to 0.35; the outrigger's `finSquareMetres` 0.38 to 0.15; the reed boat's depth 0.182 to 0.194 m and her seated poler's hands 0.72 to 0.49 m above the water; `backedShare` and `bottomBackedShare` the coracle's 0.47 and 0.65 to 0.4 and 0.49, the currach's 0.4 and 0.31 to 0.39 and 0.4, the reed boat's 0.1 and 0.05 to 0.27 and 0.28, and the bark canoe's 1 to 0.48 and 0.98; `seep.litresPerHourASeam` the coracle's 12 to 13, the currach's 15 to 12.6; the raft's `shell.spanMetres` 1.5 to 1.0. `paddleAbeamMetres` is measured from her middle. A gale's sea now pulls a moored sailboat about as hard as the gale's wind does, where it pulled twice as hard. A patch that sets one of these values sets it against her model, and on our own vessels the next build writes over it.
- The drawn sea poses a vessel by her hull box's length and her beam; it took her collision box's longer side for both.
- `HullShape`'s members are `HalfLengthMetres`, `HalfBeamMetres`, `DepthMetres`, `CentreAlongMetres`, `CentreAcrossMetres` and `KeelMetres`, where they were `HalfLength`, `HalfBeam`, `Depth`, `CentreAlong`, `CentreAcross` and `Keel`. `HullShape.FromBox(fromMetres, toMetres)` takes a box in metres from the entity's origin, and `HullShape.VoxelsPerBlock` is gone. `Seaworthiness.From(attributes, beamMetres, lengthMetres)` takes her length too.

New: `hullBoxMetres`, `overturnedPivotVoxels`, `levelHeightsMetres`, `floatLengthMetres`, `capsizeBreakerLengths`, the voxel twin of every measured key, and `RoughSeas.Units.Voxels`, which converts voxels and metres from the one definition in `RoughSeas/Units/voxel.json`.

### The dugout's build revised

The dugout is built as the sources describe it: the bark comes off first, the fire is laid on bare wood, catches and smoulders down to embers, and every hewing is drawn the way the finished hull is ([Built in place](Building-a-Vessel#built-in-place)). A vessel of yours loads as it did.

- An axe Shift + right-clicked on the middle log lays the blank, in its bark; dry grass laid with Shift on a log does what vanilla does again, and our Harmony prefix on `ItemDryGrass.OnHeldInteractStart` is gone. A blank no longer starts with its grass laid.
- `roughseas:hew` strips the bark a stroke a piece before anything else goes on: clay, dry grass, firewood and a light are refused with `shipyard-debark-first` until it is bare.
- A burnt-out fire leaves the clay on the rim; the hewing takes it off. The first ball's kind is kept as `clayType` in `roughseas:build`, and the bark stripped as `barkStripped`.
- The dugout's fire spawns vanilla's fire particles at their real quantities (they never showed before) and lights the ground round it, `LightHsv` as a pit kiln's.
- The dugout's shape: `Bark`, `Grass`, the pieces `Wood{n}x{k}`, `Fire{n}At{p}` and the pieces of `Clay{n}` are new; `Blank1` to `Blank5` are hull boards like her `Shell`, with floors, where they were stacked bands. Her ends show end grain, the outrigger's too. Her `char` texture is vanilla's burnt log.

New: `barkPieces`, `hollowLengthMetres`, `shipyard-debark`, `shipyard-bark`, `shipyard-debark-first`.

### The dugout's hewing shown as it's done

A hewing no longer changes her at once: the inside is cut a section at a time, then the outside trimmed a section at a time, in the same strokes ([Built in place](Building-a-Vessel#built-in-place)).

- `roughseas:burnout` needs `hewnSectionsMetres`, and its shape drawn in those sections: `Blank{n}`, `Char{n}` and `Shell` hold a group per section and her ends, and `Hewn{n}` is new. A burnout vessel of yours draws its levels as the dugout's do.
- `IVesselHold` grew `DesignedLitres`, the hold she is built to: a hewing's strokes are counted from it.
- `roughseas:hew` swings the player animation `roughseas-hew`, vanilla's felling chop with its chop at the blow, where it swung vanilla's `axechop` with a chop a stroke from the server.

- The one who last took an axe to her is kept as `hewer` in `roughseas:build`: only that hull throws chips at their blows.

New: `hewnSectionsMetres`, `roughseas-hew`, `hewer`, `IVesselHold.DesignedLitres`.

### A vessel lies where she is laid

A vessel that becomes an entity, bound, hewn, rigged or spawned, stays where she was put: she no longer slides sideways off a ledge, nor sinks a block into the ground. A vessel of yours loads as it did.

- Vanilla's `passivephysicsmultibox` turns only the middle of each collision box with her yaw, never its sides, so a box longer than it is wide reaches past her sides once she lies across it, and vanilla pushes her out of whatever it overlaps. Our vessels' `collisionBoxes` are now their hull box in squares as wide as she is, spread from end to end and overlapping where her length isn't a whole number of them: the reed boat's nine, the bark canoe's six, the dugout's five, the currach's four and the coracle's two. The outrigger's hull keeps five and her float takes the other five of the ten vanilla's physics holds, 0.25 m squares spread along her 3 m float.
- Every hull's boxes start at her origin. A box starting above it, as the reed boat's did by 9 mm, rests on the ground by float rounding, and she sinks through the block under her a few ticks later.
- A vessel of yours on `passivephysicsmultibox` meets both: cut its hull in squares, as vanilla's raft (1.5 by 1.5 m) and sailboat (2.8 by 2.8 m) are, from `y1` 0.

### Wedges driven blow by blow

Each wedge is driven on its own, a step a blow, and the row splits once every wedge in it is home ([Recipes](Recipes#splitting-wedge)).

- The wedge's blocks are `roughseas:wedge-{we|ns}-{0..4}`, the last part how far it is driven; it drops and is crafted as `roughseas:wedge-we-0`. Its `splitSeconds` is now `secondsPerBlow`.
- Every axe carries `roughseas:drive`, which swings the player animation `roughseas-drive` (its butt brought down on the wedge, chop at the blow) while held on a wedge not yet home. The wedge's own hold and its progress bar are gone.
- `roughseas:drive` and `roughseas:hew` count blows the same way, one a cycle of their animation, each on the server.
- A pole's handbook page gives how it is split under "Created by", from `handbook.createdBy`.
- Poles and laths show one icon for every length: the collectible behaviour `roughseas:icon` draws its `shape` in place of the item's in inventory slots and the handbook, in the item's own textures. Their per-length `guiTransformByType` is one `guiTransform`.

New: `roughseas:drive`, `roughseas-drive`, `roughseas:icon`, `secondsPerBlow`, `shipyard-drive` (was `shipyard-split`), `handbook-createdby-pole`.

## What not to touch

| Name | What it is |
| --- | --- |
| `roughseas:seamap` | the map region moddata, and the network channel that carries the map to clients |
| `roughseas:oceancalibration` | save data: what the world's ocean map means |
| `roughseas:vessels` | the network channel a rider's client reports its vessel's knocks over |
| `roughseas-force*` | our wind patterns, which our model sets; to hold a region's wind, set a pattern of your own ([Wind patterns](Wind-Patterns)) |

## Adding and removing Rough Seas

The mod can join an existing world, and leave one. Without it the world opens cleanly: each region keeps the strength of the wind we last set for up to a game hour, then vanilla rolls its own pattern.
