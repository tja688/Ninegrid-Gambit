import re
import sys
from collections import defaultdict

def decode_name(raw: str) -> str:
    name = raw.strip().strip('"')
    if "\\u" in name:
        try:
            name = bytes(name, "utf-8").decode("unicode_escape")
        except UnicodeDecodeError:
            pass
    return name


def parse(path: str) -> None:
    text = open(path, encoding="utf-8").read()
    go_names: dict[str, str] = {}
    for m in re.finditer(
        r"--- !u!1 &(\d+)\nGameObject:.*?m_Name: (.+?)\n", text, re.S
    ):
        gid, name = m.group(1), decode_name(m.group(2))
        if name:
            go_names[gid] = name

    entries: list[tuple[str, str, int]] = []
    for m in re.finditer(
        r"--- !u!212 &\d+\nSpriteRenderer:.*?m_GameObject: \{fileID: (\d+)\}.*?m_SortingOrder: (-?\d+)",
        text,
        re.S,
    ):
        gid, order = m.group(1), int(m.group(2))
        entries.append(("SR", go_names.get(gid, gid), order))
    for m in re.finditer(
        r"--- !u!23 &\d+\nMeshRenderer:.*?m_GameObject: \{fileID: (\d+)\}.*?m_SortingOrder: (-?\d+)",
        text,
        re.S,
    ):
        gid, order = m.group(1), int(m.group(2))
        entries.append(("MR", go_names.get(gid, gid), order))

    entries.sort(key=lambda x: x[2])
    print(path)
    for kind, name, order in entries:
        print(f"  {kind:2} order={order:3} {name}")

    by_order: dict[int, list[str]] = defaultdict(list)
    for kind, name, order in entries:
        by_order[order].append(f"{kind}:{name}")
    dups = {k: v for k, v in by_order.items() if len(v) > 1}
    if dups:
        print("DUPLICATES:")
        for order in sorted(dups):
            print(f"  order={order}: {', '.join(dups[order])}")
    print()


if __name__ == "__main__":
    root = r"C:\Users\jinji\Documents\GitHub\Ninegrid-Gambit\Assets\Prefabs"
    names = [
        "怪物卡标准模板.prefab",
        "玩家卡标准模板.prefab",
        "道具卡标准模版.prefab",
        "遗物卡标准模版.prefab",
        "攻击力.prefab",
        "攻击数值.prefab",
        "护甲.prefab",
        "血条.prefab",
    ]
    for n in names:
        parse(root + "\\" + n)
