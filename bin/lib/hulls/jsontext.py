import json
import re

NUMBER = re.compile(r"-?\d+(\.\d+)?([eE][-+]?\d+)?")
COMMENT = re.compile(r"^[ \t]*//.*\n", re.M)


def load(path):
    return json.loads(COMMENT.sub("", path.read_text()))


def dumps(value):
    if isinstance(value, dict):
        return "{" + ", ".join(f"{json.dumps(k)}: {dumps(v)}" for k, v in value.items()) + "}"
    if isinstance(value, list):
        return "[" + ", ".join(dumps(v) for v in value) + "]"
    if isinstance(value, float) and value.is_integer():
        return str(int(value))
    return json.dumps(value)


def put(text, path, value, after=None):
    """The JSON text with the value at path set, every other character as it was. A missing key is added after the
    member named after, or last in its object, and the objects on its way with it."""
    for depth in range(1, len(path)):
        try:
            locate(text, path[:depth])
        except KeyError:
            return put(put(text, path[:depth], {}), path, value)
    start = locate(text, path[:-1])
    for key, value_start, value_end in items(text, start):
        if key == path[-1]:
            return text[:value_start] + dumps(value) + text[value_end:]
    if isinstance(path[-1], int):
        raise IndexError(f"no element {path[-1]} on the way to {path}")
    member = f"{json.dumps(path[-1])}: {dumps(value)}"
    found = items(text, start)
    if not found:
        return text[:start + 1] + member + text[start + 1:]
    key = skip(text, start + 1)
    line = text.rfind("\n", 0, key)
    separator = ",\n" + text[line + 1:key] if line > start else ", "
    end = next((value_end for key, _, value_end in found if key == after), found[-1][2])
    return text[:end] + separator + member + text[end:]


def locate(text, path):
    at = skip(text, 0)
    for step in path:
        found = [value_start for key, value_start, _ in items(text, at) if key == step]
        if not found:
            raise KeyError(f"no {step!r} on the way to {path}")
        at = found[0]
    return at


def items(text, start):
    """Each member of the object or element of the array opening at start: (key or index, value start, value end)."""
    closing = "}" if text[start] == "{" else "]"
    found, at, index = [], skip(text, start + 1), 0
    while text[at] != closing:
        key = index
        if closing == "}":
            key_end = past(text, at)
            key = json.loads(text[at:key_end])
            at = skip(text, skip(text, key_end) + 1)
        end = past(text, at)
        found.append((key, at, end))
        at = skip(text, end)
        if text[at] == ",":
            at = skip(text, at + 1)
        index += 1
    return found


def past(text, at):
    if text[at] == '"':
        at += 1
        while text[at] != '"':
            at += 2 if text[at] == "\\" else 1
        return at + 1
    if text[at] in "{[":
        depth = 0
        while True:
            if text[at] == '"':
                at = past(text, at)
                continue
            depth += {"{": 1, "[": 1, "}": -1, "]": -1}.get(text[at], 0)
            at += 1
            if depth == 0:
                return at
    literal = NUMBER.match(text, at) or re.compile(r"true|false|null").match(text, at)
    return literal.end()


def skip(text, at):
    while text[at] in " \t\r\n/":
        at = text.index("\n", at) + 1 if text.startswith("//", at) else at + 1
    return at
