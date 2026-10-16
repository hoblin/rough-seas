# Vanilla at sea

Read before working on boats, anchors, ropes, accessories or lightning: what 1.22.7 already has, and what we cannot change.

- The sailboat carries a stone anchor (`anchor-stone`) in its prow slot. Right-click lowers and raises it (the `lowered` stack attribute); in vanilla that is only a shape. Our mooring gives it hold.
- Boat accessories go on with Ctrl+right-click on the slot's own selection box (`bin/game hover ID LProwAP`). A slot holds one item, never a stack. The sailboat's `Lower Mast Storage` takes category `rope`, which no vanilla item has; our anchor warp fills it.
- Vanilla rope ties to fences and grown logs (`RopeTieable`) and to `ropetieable` entities, which boats are only through our patch. It is a cloth spring of at most 10 m that pulls, not a hard leash, and it saves with its map region. Its pull is tuned for animals on land: on a floating vessel it loses to a current, so our mooring holds the vessel and the rope only shows the tie (its tension maths once crashed the client on a NaN). Holding rope, a click on a rope-tieable entity ties it before any accessory slot sees the click.
- A rope survives a restart when pinned to a rope-tieable block, such as a fence; one `/bridge tie` pinned to a water block was gone after the restart, and loose ropes come back as rope items.
- A held liquid container passes its right-click to collectible behaviours only when empty and with no block selected; with drinkable content it drinks.
- Vanilla's own lightning strikes only within ±200 blocks of a map region's centre. `OnLightningImpactBegin` can move the impact; vanilla damages creatures, boats among them, within 8 blocks of it.
