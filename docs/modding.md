# Modding rakes

Read before writing mod code: patches, entities, behaviours, physics. Stepped on already; don't step again.

- A patch targets vanilla as `game:entities/...`, not `survival:`. Entity files are server-side assets, so an entity patch needs `"side": "Server"`.
- The patch loader reads every file in `patches/`, whatever its extension. To switch a patch off, move it out of the folder.
- An entity's variant is `entity.Properties.Variant["material"]`; `entity.Variant` is not set for entities.
- Server physics runs on worker threads. `PhysicsUpdateWatcher` fires from them for a drifting entity and from the main thread for a steered one, so never touch the world from it: queue the event and act in `OnGameTick`.
- A steered boat's `Pos.Motion` on the server is its intended speed (`EntityBoat` rewrites it from `ForwardSpeed`, which ignores collisions). The real speed comes from the rider's position packets. The server cannot hold a steered boat back either: an anchor or a leash can only follow it.
- Every decision our code makes logs a debug line, and every hit on a hull logs its cause and outcome. A new mechanic logs its decisions as it is written.
- Vanilla's attachable behaviour finds an entity's wearable slot by its selection box's index, and a box past the last slot crashes the hover help. A box that holds nothing, such as a hull box, gets an empty slot (`"forCategoryCodes": []`) at its index.
- `interactMountAnySeat` mounts a player who clicks any box that is not a seat with an empty hand: a sail cleat, a storage slot. To mount from a hull box, name it as the steering seat's `selectionBox`.
- The new world screen runs before any world loads, and reads a mod's world-setting and playstyle texts only from `assets/game/lang/worldconfig-<code>.json`, with keys unprefixed. In the mod's own lang files they show only in a running world. The bench can't drive that screen: a release zip in `$BENCH_DATA/Mods` that loads without `Failed to find language folder` for it in `client-main.log` is the check.
- A vanilla lang key is overridden from our own lang file as `game:<key>`, which replaces the whole value: there is no patch op for lang. Change only the false sentence of vanilla's value, in each locale vanilla translates; the others fall back to English.
- Writing a vanilla watched attribute with a value of another type (`/bridge set ID clothIds x`) crashes every client that receives it.
