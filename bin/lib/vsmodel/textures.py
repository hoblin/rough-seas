import os

import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
MOD = os.path.join(ROOT, "RoughSeas", "assets")
GAME = os.path.join(os.environ["VINTAGE_STORY"], "assets")
GAME_ORIGINS = ("game", "survival", "creative")


def link_textures(textures):
    for code, location in textures.items():
        material = bpy.data.materials.get(code)
        path = texture_file(location)
        if path is None and not location.startswith("#"):
            print(f"Texture {code} not found: {location}")
        if material is None or path is None or not material.use_nodes:
            continue
        for node in material.node_tree.nodes:
            if node.type == "TEX_IMAGE":
                node.image = bpy.data.images.load(path, check_existing=True)


def located(original, exported):
    known = original.get("textures", {})
    new = {code: image_location(code) for code in exported.get("textures", {}) if code not in known}
    return {**exported, "textures": {code: location for code, location in new.items() if location}}


def texture_file(location):
    if location.startswith("#"):
        return None
    domain, _, path = location.rpartition(":")
    domain = domain or "game"
    roots = [os.path.join(MOD, domain)] + [os.path.join(GAME, origin) for origin in (GAME_ORIGINS if domain == "game" else [domain])]
    for root in roots:
        candidate = os.path.join(root, "textures", path + ".png")
        if os.path.exists(candidate):
            return candidate
    return None


def image_location(code):
    material = bpy.data.materials.get(code)
    images = [node.image for node in material.node_tree.nodes if node.type == "TEX_IMAGE" and node.image] if material and material.use_nodes else []
    if not images:
        return None
    path = os.path.realpath(bpy.path.abspath(images[0].filepath))
    roots = {os.path.join(MOD, domain): domain for domain in os.listdir(MOD)}
    roots.update({os.path.join(GAME, origin): "game" for origin in GAME_ORIGINS})
    for root, domain in roots.items():
        textures = os.path.join(root, "textures") + os.sep
        if path.startswith(textures) and os.path.exists(path):
            return f"{domain}:{os.path.splitext(path[len(textures):])[0]}"
    return None
