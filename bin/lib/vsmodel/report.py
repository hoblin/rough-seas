import json
import re


def report(title, differences, path):
    with open(path, "w") as file:
        file.writelines(line + "\n" for line in differences)
    print(f"{title}: {len(differences)} differences, all in {path}" if differences else f"{title}: identical")
    kinds = {}
    for line in differences:
        kinds.setdefault(kind_of(*line.split(": ", 1)), []).append(line)
    for kind, lines in sorted(kinds.items(), key=lambda item: -len(item[1])):
        print(f"  {len(lines):5}  {kind}    e.g. {lines[0]}")


def diff(a, b, path=""):
    if isinstance(a, dict) and isinstance(b, dict):
        for key in dict.fromkeys([*a, *b]):
            if key not in b:
                yield f"{path}/{key}: lost"
            elif key not in a:
                yield f"{path}/{key}: added"
            else:
                yield from diff(a[key], b[key], f"{path}/{key}")
    elif named(a) and named(b):
        theirs = {key_of(each): each for each in b}
        for each in a:
            other = theirs.pop(key_of(each), None)
            place = each.get("name") or f"{path}[{each['code']}]"
            if other is None:
                yield f"{place}: lost"
            else:
                yield from diff(each, other, place)
        for key in theirs:
            yield f"{path}[{key}]: added"
    elif isinstance(a, list) and isinstance(b, list):
        if len(a) != len(b):
            yield f"{path}: {len(a)} items became {len(b)}"
        for index, (x, y) in enumerate(zip(a, b)):
            yield from diff(x, y, f"{path}[{index}]")
    elif a != b:
        yield f"{path}: {json.dumps(a)} became {json.dumps(b)}"


def named(items):
    return isinstance(items, list) and items and all(isinstance(each, dict) and key_of(each) for each in items)


def key_of(item):
    return item.get("name") or item.get("code")


def kind_of(place, change):
    key = re.sub(r"\[[^\]]*\]", "", place.split("/", 1)[1] if "/" in place else place)
    key = re.sub(r"\b(north|east|south|west|up|down)\b", "*", key)
    return f"{key} {change if change in ('lost', 'added') else 'changed'}"
