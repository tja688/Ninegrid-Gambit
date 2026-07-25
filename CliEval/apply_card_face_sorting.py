"""Apply CardFaceSortingLayers to card face / stat component prefabs (YAML)."""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(r"C:\Users\jinji\Documents\GitHub\Ninegrid-Gambit\Assets\Prefabs")

# (GameObject name, renderer kind) -> order. kind: SR | MR
ORDER_BY_NAME_KIND: dict[tuple[str, str], int] = {
    ("Card_Border_rectangle_bronze", "SR"): 0,
    ("Card_Border_rectangle_dark", "SR"): 0,
    ("CardShirts_8", "SR"): 5,
    ("logo", "SR"): 8,
    ("CardTemplateSprites_42 (1)", "SR"): 10,
    ("CardTemplateSprites_42 (2)", "SR"): 12,
    ("背景", "SR"): 15,
    ("背景图", "SR"): 15,
    ("卡框", "SR"): 22,
    ("横幅", "SR"): 26,
    ("主视图Mask", "SR"): 34,
    ("核心图标", "SR"): 35,
    ("主图标", "SR"): 35,
    ("遗物主图标", "SR"): 35,
    ("介绍区域", "SR"): 40,
    ("预备面板", "SR"): 40,
    ("描述面板", "SR"): 45,
    ("附Icon", "SR"): 48,
    ("Icon预备1", "SR"): 48,
    ("Icon预备2", "SR"): 49,
    ("Rank_2", "SR"): 50,
    ("Rank_2 (1)", "SR"): 51,
    ("Rank_2 (2)", "SR"): 52,
    ("攻击力", "SR"): 53,
    ("F_UI_Gem_EmptySlot3", "SR"): 52,
    ("F_U_ObjectIcon_85", "SR"): 54,
    ("攻击", "SR"): 52,
    ("护甲", "SR"): 56,
    ("血量", "SR"): 60,
    ("行动", "SR"): 64,
    ("状态2", "SR"): 68,
    ("行动计数", "SR"): 72,
    ("攻击数值", "MR"): 76,
    ("护甲数值", "MR"): 80,
    ("血量数值", "MR"): 84,
    ("行动计数", "MR"): 88,
    ("名字", "MR"): 92,
    ("标准世界文字", "MR"): 94,
    ("描述", "MR"): 96,
}

DEFAULT_WORLD_TEXT = 94


def decode_name(raw: str) -> str:
    name = raw.strip().strip('"')
    if "\\u" in name:
        try:
            name = bytes(name, "utf-8").decode("unicode_escape")
        except UnicodeDecodeError:
            pass
    return name


def parse_go_names(text: str) -> dict[str, str]:
    names: dict[str, str] = {}
    for m in re.finditer(
        r"--- !u!1 &(\d+)\nGameObject:.*?m_Name: (.+?)\n", text, re.S
    ):
        gid, name = m.group(1), decode_name(m.group(2))
        if name:
            names[gid] = name
    return names


def resolve_order(name: str, kind: str) -> int | None:
    key = (name, kind)
    if key in ORDER_BY_NAME_KIND:
        return ORDER_BY_NAME_KIND[key]
    if name == "标准世界文字" and kind == "MR":
        return DEFAULT_WORLD_TEXT
    return None


def patch_renderer_block(block: str, go_names: dict[str, str], kind: str) -> str:
    m_go = re.search(r"m_GameObject: \{fileID: (\d+)\}", block)
    if not m_go:
        return block
    name = go_names.get(m_go.group(1), "")
    order = resolve_order(name, kind)
    if order is None:
        return block
    return re.sub(r"m_SortingOrder: -?\d+", f"m_SortingOrder: {order}", block, count=1)


def patch_mask_block(block: str, go_names: dict[str, str]) -> str:
    m_go = re.search(r"m_GameObject: \{fileID: (\d+)\}", block)
    if not m_go:
        return block
    name = go_names.get(m_go.group(1), "")
    if name != "主视图Mask":
        return block
    base = 34
    block = re.sub(r"m_SortingOrder: -?\d+", f"m_SortingOrder: {base}", block, count=1)
    block = re.sub(
        r"m_FrontSortingOrder: -?\d+",
        f"m_FrontSortingOrder: {base + 32}",
        block,
        count=1,
    )
    block = re.sub(
        r"m_BackSortingOrder: -?\d+",
        f"m_BackSortingOrder: {base - 32}",
        block,
        count=1,
    )
    return block


def patch_file(path: Path) -> bool:
    text = path.read_text(encoding="utf-8")
    if text.startswith("version https://git-lfs"):
        print(f"SKIP LFS pointer: {path.name}")
        return False
    go_names = parse_go_names(text)

    def sub_renderer(match: re.Match[str]) -> str:
        block = match.group(0)
        kind = "SR" if block.startswith("--- !u!212") else "MR"
        return patch_renderer_block(block, go_names, kind)

    new_text = re.sub(
        r"--- !u!(?:212|23) &\d+\n(?:SpriteRenderer|MeshRenderer):.*?(?=\n--- |\Z)",
        sub_renderer,
        text,
        flags=re.S,
    )
    new_text = re.sub(
        r"--- !u!331 &\d+\nSpriteMask:.*?(?=\n--- |\Z)",
        lambda m: patch_mask_block(m.group(0), go_names),
        new_text,
        flags=re.S,
    )
    if new_text != text:
        path.write_text(new_text, encoding="utf-8")
        print(f"Patched {path.name}")
        return True
    print(f"No changes {path.name}")
    return False


def main() -> int:
    names = sorted(
        p.name
        for p in ROOT.glob("*.prefab")
        if any(
            key in p.name
            for key in (
                "标准模板",
                "标准模版",
                "攻击力",
                "攻击数值",
                "标准世界文字",
            )
        )
    )
    for name in names:
        patch_file(ROOT / name)
    return 0


if __name__ == "__main__":
    sys.exit(main())
