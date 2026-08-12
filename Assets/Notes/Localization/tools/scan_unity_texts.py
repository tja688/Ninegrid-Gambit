#!/usr/bin/env python3
"""
Scan Unity .unity / .prefab YAML for TMP (m_text) and legacy uGUI Text (m_Text).
Outputs TSV rows and can generate a Markdown inventory report.
"""
from __future__ import annotations

import argparse
import codecs
import json
import re
import sys
from collections import defaultdict
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, List, Optional, Set, Tuple

# ---------------------------------------------------------------------------
# Unity string decoding
# ---------------------------------------------------------------------------

def decode_unity_string(raw: str) -> str:
    """Decode a Unity YAML scalar (quoted or bare)."""
    raw = raw.strip()
    if not raw:
        return ""
    # Bare scalar (number, unquoted word)
    if raw[0] not in ('"', "'"):
        return raw
    quote = raw[0]
    if raw[-1] != quote:
        # Multiline or truncated — take rest
        inner = raw[1:]
    else:
        inner = raw[1:-1]
    # Python-style unicode / escape decoding
    try:
        return codecs.decode(inner, "unicode_escape")
    except Exception:
        return inner


# ---------------------------------------------------------------------------
# YAML block parser (line state machine)
# ---------------------------------------------------------------------------

BLOCK_HEADER_RE = re.compile(r"^--- !u!(\d+) &(\d+)")
FILEID_RE = re.compile(r"\{fileID:\s*(\d+)")
GUID_RE = re.compile(r"guid:\s*([0-9a-f]{32})", re.I)
COMPONENT_TYPE_RE = re.compile(r"m_EditorClassIdentifier:\s*(.+)")
TARGET_LINE_RE = re.compile(
    r"target:\s*\{fileID:\s*(\d+),\s*guid:\s*([0-9a-f]{32})", re.I
)
SCRIPT_GUID_RE = re.compile(r"m_Script:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-f]{32})", re.I)

# Unity built-in text component script GUIDs
TEXT_SCRIPT_GUIDS = {
    "9541d86e2fd84c1d9990edf0852d74ab",  # TextMeshPro
    "f4688fdb7df04437aeb418b961361dc5",  # TextMeshProUGUI
    "5f7201a12d95ffc409449d95f23cf332",  # UnityEngine.UI.Text
}
FINDTMP_NAME_RE = re.compile(r'FindTmp\([^,]+,\s*"([^"]+)"\)')
FIND_CHILD_NAME_RE = re.compile(r'Find(?:Child|Transform)?\([^,]+,\s*"([^"]+)"\)')


@dataclass
class GameObjectInfo:
    file_id: int
    name: str = ""
    is_active: bool = True
    components: List[int] = field(default_factory=list)


@dataclass
class TransformInfo:
    file_id: int
    game_object_id: int = 0
    father_id: int = 0


@dataclass
class TextComponentInfo:
    file_id: int
    game_object_id: int = 0
    component_type: str = ""
    text: str = ""
    font_guid: str = ""
    script_guid: str = ""
    source: str = "inline"  # inline | prefab_override
    hierarchy_override: str = ""
    active_override: str = ""


@dataclass
class PrefabInstanceInfo:
    instance_id: int
    parent_transform_id: int = 0
    source_guid: str = ""
    root_transform_id: int = 0
    root_name: str = ""
    root_active: bool = True


def parse_scalar_value(line: str) -> str:
    """Return value part after 'key: '."""
    idx = line.index(":")
    return line[idx + 1 :].strip()


def is_valid_text_component(tc: TextComponentInfo) -> bool:
    if tc.text is None:
        return False
    if tc.script_guid and tc.script_guid.lower() in TEXT_SCRIPT_GUIDS:
        return True
    if "TextMeshPro" in tc.component_type or "UI.Text" in tc.component_type:
        return True
    return False


def parse_unity_file(path: Path) -> Tuple[
    Dict[int, GameObjectInfo],
    Dict[int, TransformInfo],
    List[TextComponentInfo],
]:
    game_objects: Dict[int, GameObjectInfo] = {}
    transforms: Dict[int, TransformInfo] = {}
    text_components: List[TextComponentInfo] = []

    current_block_type: Optional[int] = None
    current_id: Optional[int] = None
    current_go: Optional[GameObjectInfo] = None
    current_tf: Optional[TransformInfo] = None
    current_text: Optional[TextComponentInfo] = None
    in_children = False
    collecting_components = False

    def flush():
        nonlocal current_go, current_tf, current_text
        if current_go is not None:
            game_objects[current_go.file_id] = current_go
        if current_tf is not None:
            transforms[current_tf.file_id] = current_tf
        if current_text is not None and is_valid_text_component(current_text):
            text_components.append(current_text)

    with path.open("r", encoding="utf-8", errors="replace") as f:
        for line in f:
            line = line.rstrip("\n\r")

            m = BLOCK_HEADER_RE.match(line)
            if m:
                flush()
                current_block_type = int(m.group(1))
                current_id = int(m.group(2))
                current_go = None
                current_tf = None
                current_text = None
                in_children = False
                collecting_components = False

                if current_block_type == 1:  # GameObject
                    current_go = GameObjectInfo(file_id=current_id)
                elif current_block_type in (4, 224):  # Transform / RectTransform
                    current_tf = TransformInfo(file_id=current_id)
                elif current_block_type == 114:  # MonoBehaviour
                    current_text = TextComponentInfo(file_id=current_id)
                continue

            if current_block_type is None:
                continue

            # GameObject fields
            if current_go is not None:
                if line.startswith("  m_Name:"):
                    current_go.name = decode_unity_string(parse_scalar_value(line))
                elif line.startswith("  m_IsActive:"):
                    val = parse_scalar_value(line)
                    current_go.is_active = val == "1"
                elif line.startswith("  m_Component:"):
                    collecting_components = True
                elif collecting_components and line.strip().startswith("- component:"):
                    fm = FILEID_RE.search(line)
                    if fm:
                        current_go.components.append(int(fm.group(1)))
                elif collecting_components and not line.startswith("  "):
                    collecting_components = False
                elif collecting_components and line.startswith("  m_") and not line.strip().startswith("- "):
                    collecting_components = False

            # Transform fields
            if current_tf is not None:
                if line.startswith("  m_GameObject:"):
                    fm = FILEID_RE.search(line)
                    if fm:
                        current_tf.game_object_id = int(fm.group(1))
                elif line.startswith("  m_Father:"):
                    fm = FILEID_RE.search(line)
                    if fm:
                        current_tf.father_id = int(fm.group(1))
                elif line.startswith("  m_Children:"):
                    in_children = True
                elif in_children and not line.startswith("  "):
                    in_children = False

            # Text component fields
            if current_text is not None:
                if line.startswith("  m_GameObject:"):
                    fm = FILEID_RE.search(line)
                    if fm:
                        current_text.game_object_id = int(fm.group(1))
                elif line.startswith("  m_EditorClassIdentifier:"):
                    current_text.component_type = parse_scalar_value(line).strip()
                elif line.startswith("  m_Script:"):
                    sm = SCRIPT_GUID_RE.search(line)
                    if sm:
                        current_text.script_guid = sm.group(1).lower()
                elif line.startswith("  m_text:") or line.startswith("  m_Text:"):
                    current_text.text = decode_unity_string(parse_scalar_value(line))
                elif line.startswith("  m_fontAsset:"):
                    gm = GUID_RE.search(line)
                    if gm:
                        current_text.font_guid = gm.group(1).lower()

    flush()
    return game_objects, transforms, text_components


# Prefab asset cache: guid -> (comp_id -> go_id, go_id -> name, root_tf_id)
_PREFAB_CACHE: Dict[str, Tuple[Dict[int, int], Dict[int, str], int]] = {}


def _load_prefab_maps(prefab_path: Path) -> Tuple[Dict[int, int], Dict[int, str], int]:
    comp_to_go: Dict[int, int] = {}
    go_names: Dict[int, str] = {}
    root_tf = 0
    gos, tfs, _ = parse_unity_file(prefab_path)
    go_to_tf = build_go_to_transform(tfs)
    for gid, go in gos.items():
        go_names[gid] = go.name
        for cid in go.components:
            comp_to_go[cid] = gid
    for tid, tf in tfs.items():
        if tf.father_id == 0:
            root_tf = tid
    return comp_to_go, go_names, root_tf


def resolve_prefab_maps(guid: str, guid_index: Dict[str, Path]) -> Tuple[Dict[int, int], Dict[int, str], int]:
    if guid not in _PREFAB_CACHE:
        path = guid_index.get(guid)
        if path and path.exists():
            _PREFAB_CACHE[guid] = _load_prefab_maps(path)
        else:
            _PREFAB_CACHE[guid] = ({}, {}, 0)
    return _PREFAB_CACHE[guid]


def parse_prefab_instances(path: Path):
    """Return (instances, stripped_to_instance, text_overrides)."""
    instances: Dict[int, PrefabInstanceInfo] = {}
    stripped_to_instance: Dict[int, int] = {}

    current_pi: Optional[PrefabInstanceInfo] = None
    pending_prop: Optional[str] = None
    pending_target_comp = 0
    pending_target_guid = ""
    text_overrides: Dict[int, List[Tuple[int, str, str]]] = defaultdict(list)  # pi_id -> [(comp, guid, text)]

    lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
    i = 0
    while i < len(lines):
        line = lines[i]
        m = BLOCK_HEADER_RE.match(line)
        if m:
            btype, bid = int(m.group(1)), int(m.group(2))
            if btype == 1001:
                current_pi = PrefabInstanceInfo(instance_id=bid)
                instances[bid] = current_pi
            elif btype in (4, 224) and "stripped" in line:
                # next lines include m_PrefabInstance
                for j in range(i + 1, min(i + 6, len(lines))):
                    pm = re.search(r"m_PrefabInstance:\s*\{fileID:\s*(\d+)", lines[j])
                    if pm:
                        stripped_to_instance[bid] = int(pm.group(1))
                        if current_pi and current_pi.root_transform_id == 0:
                            current_pi.root_transform_id = bid
                        break
            elif btype == 114 and "stripped" in line:
                current_pi = None
        elif current_pi is not None:
            if line.strip().startswith("m_TransformParent:"):
                fm = FILEID_RE.search(line)
                if fm:
                    current_pi.parent_transform_id = int(fm.group(1))
            elif line.strip().startswith("m_SourcePrefab:"):
                gm = GUID_RE.search(line)
                if gm:
                    current_pi.source_guid = gm.group(1).lower()
            elif line.strip() == "m_Modifications:":
                pass
            elif line.strip().startswith("- target:"):
                tm = TARGET_LINE_RE.search(line)
                if tm:
                    pending_target_comp = int(tm.group(1))
                    pending_target_guid = tm.group(2).lower()
            elif line.strip().startswith("propertyPath:"):
                pending_prop = decode_unity_string(parse_scalar_value(line.strip()))
            elif pending_prop and line.strip().startswith("value:"):
                val = decode_unity_string(parse_scalar_value(line.strip()))
                if pending_prop == "m_Name" and pending_target_comp:
                    current_pi.root_name = val
                elif pending_prop in ("m_text", "m_Text") and pending_target_comp:
                    text_overrides[current_pi.instance_id].append(
                        (pending_target_comp, pending_target_guid, val)
                    )
                elif pending_prop == "m_IsActive":
                    current_pi.root_active = val == "1"
                pending_prop = None
        i += 1

    return instances, stripped_to_instance, text_overrides


def transform_to_go_name(
    tf_id: int,
    game_objects: Dict[int, GameObjectInfo],
    go_to_tf: Dict[int, int],
    tf_parent: Dict[int, int],
    stripped_to_instance: Dict[int, int],
    instances: Dict[int, PrefabInstanceInfo],
) -> str:
    """Resolve transform fileID to a display name (scene GO or prefab instance root)."""
    for gid, tid in go_to_tf.items():
        if tid == tf_id and gid in game_objects:
            return game_objects[gid].name
    if tf_id in stripped_to_instance:
        pi = instances.get(stripped_to_instance[tf_id])
        if pi and pi.root_name:
            return pi.root_name
        return f"<PrefabInstance:{stripped_to_instance[tf_id]}>"
    return f"<TF:{tf_id}>"


def build_scene_path_for_transform(
    tf_id: int,
    game_objects: Dict[int, GameObjectInfo],
    go_to_tf: Dict[int, int],
    tf_parent: Dict[int, int],
    stripped_to_instance: Dict[int, int],
    instances: Dict[int, PrefabInstanceInfo],
) -> str:
    names: List[str] = []
    cur = tf_id
    depth = 0
    while cur and depth < 64:
        names.append(
            transform_to_go_name(cur, game_objects, go_to_tf, tf_parent, stripped_to_instance, instances)
        )
        parent = tf_parent.get(cur, 0)
        if not parent:
            pi = instances.get(stripped_to_instance.get(cur, -1))
            if pi and pi.parent_transform_id:
                cur = pi.parent_transform_id
                continue
            break
        cur = parent
        depth += 1
    names.reverse()
    return "/".join(names)


def extract_prefab_text_overrides(
    path: Path,
    game_objects: Dict[int, GameObjectInfo],
    transforms: Dict[int, TransformInfo],
    guid_index: Dict[str, Path],
) -> List[TextComponentInfo]:
    instances, stripped_to_instance, text_overrides = parse_prefab_instances(path)  # type: ignore[misc]
    go_to_tf = build_go_to_transform(transforms)
    tf_parent = build_transform_parent(transforms)

    # Link stripped roots to instances and set parent chain
    for stf_id, pi_id in stripped_to_instance.items():
        if pi_id in instances:
            instances[pi_id].root_transform_id = stf_id
            tf_parent[stf_id] = instances[pi_id].parent_transform_id

    results: List[TextComponentInfo] = []
    for pi_id, overrides in text_overrides.items():
        pi = instances.get(pi_id)
        if not pi:
            continue
        for comp_id, prefab_guid, text in overrides:
            comp_to_go, go_names, _ = resolve_prefab_maps(prefab_guid, guid_index)
            go_id = comp_to_go.get(comp_id, 0)
            inner_name = go_names.get(go_id, "标准世界文字")
            scene_path = build_scene_path_for_transform(
                pi.root_transform_id, game_objects, go_to_tf, tf_parent, stripped_to_instance, instances
            )
            hierarchy = f"{scene_path}/{inner_name}" if scene_path else inner_name
            # Store hierarchy in a synthetic TextComponentInfo via negative go id hash
            tc = TextComponentInfo(
                file_id=-(pi_id * 1000 + comp_id % 1000),
                game_object_id=0,
                component_type="Unity.TextMeshPro::TMPro.TextMeshPro",
                text=text,
                source="prefab_override",
                hierarchy_override=hierarchy,
                active_override="1" if pi.root_active else "0",
            )
            results.append(tc)
    return results


# ---------------------------------------------------------------------------
# Hierarchy
# ---------------------------------------------------------------------------

def build_go_to_transform(transforms: Dict[int, TransformInfo]) -> Dict[int, int]:
    """game_object_id -> transform_file_id"""
    m: Dict[int, int] = {}
    for tid, tf in transforms.items():
        if tf.game_object_id:
            m[tf.game_object_id] = tid
    return m


def build_transform_parent(transforms: Dict[int, TransformInfo]) -> Dict[int, int]:
    """transform_id -> parent_transform_id (0 if root)"""
    return {tid: tf.father_id for tid, tf in transforms.items()}


def get_hierarchy_path(
    go_id: int,
    game_objects: Dict[int, GameObjectInfo],
    go_to_tf: Dict[int, int],
    tf_parent: Dict[int, int],
    max_depth: int = 64,
) -> str:
    names: List[str] = []
    tf_id = go_to_tf.get(go_id)
    if go_id in game_objects:
        names.append(game_objects[go_id].name or f"<GO:{go_id}>")
    else:
        names.append(f"<GO:{go_id}>")

    depth = 0
    while tf_id and depth < max_depth:
        parent_tf = tf_parent.get(tf_id, 0)
        if not parent_tf:
            break
        # find GO for parent transform
        parent_go_id = None
        for gid, tid in go_to_tf.items():
            if tid == parent_tf:
                parent_go_id = gid
                break
        if parent_go_id and parent_go_id in game_objects:
            names.append(game_objects[parent_go_id].name or f"<GO:{parent_go_id}>")
        tf_id = parent_tf
        depth += 1

    names.reverse()
    return "/".join(names)


# ---------------------------------------------------------------------------
# Classification helpers
# ---------------------------------------------------------------------------

NO_TRANSLATE_RE = re.compile(
    r"^[\s\d\+\-\×xX\*\/\.\,\:\;\!\?\#\@\%\&\(\)\[\]\{\}<>«»""''…·\|\\\\]*$"
    r"|^[\d\s\+\-\×xX\*\/\.\:]+$"
    r"|^\d+$"
    r"|^[vV]?\d+(\.\d+)*$"
    r"|^\+?\d+$"
    r"|^×\d+$"
    r"|^\d+/\d+$"
)

CHINESE_RE = re.compile(r"[\u4e00-\u9fff]")

TEMPLATE_NAME_HINTS = (
    "模板", "模版", "占位", "Placeholder", "placeholder", "Template", "template",
    "样例", "示例", "Example", "条目", "Entry", "Item", "Clone",
)

TEMPLATE_TEXT_HINTS = (
    "字段介绍", "描述问题", "顺劈斧", "占位", "示例", "样例", "Lorem", "Test",
    "2026年", "兵大锅", "牌组归属", "词条",
)


def needs_translation(text: str) -> str:
    if not text or text.isspace():
        return "否(空)"
    if NO_TRANSLATE_RE.match(text.strip()):
        return "否(数字/符号)"
    if CHINESE_RE.search(text):
        return "是"
    # English words that are UI labels
    if re.search(r"[A-Za-z]{2,}", text):
        return "待确认(英文)"
    return "否(符号)"


# ---------------------------------------------------------------------------
# C# cross-reference index
# ---------------------------------------------------------------------------

TEXT_WRITE_PATTERNS = [
    re.compile(r"\.text\s*="),
    re.compile(r"\.SetText\s*\("),
    re.compile(r"SetText\s*\("),
    re.compile(r"textMeshPro\.text", re.I),
    re.compile(r"tmpText\.text", re.I),
    re.compile(r"TextMeshProUGUI"),
]


@dataclass
class CodeIndex:
    files: List[Path]
    content_by_file: Dict[Path, str]
    # child TMP name -> classes that FindTmp + assign .text
    runtime_tmp_names: Dict[str, Set[str]]
    # path constants referenced in code (static UI wiring)
    static_path_refs: Set[str]


def build_code_index(scripts_root: Path) -> CodeIndex:
    files = list(scripts_root.rglob("*.cs"))
    content_by_file: Dict[Path, str] = {}
    runtime_tmp_names: Dict[str, Set[str]] = defaultdict(set)
    static_path_refs: Set[str] = set()

    for fp in files:
        try:
            content = fp.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        content_by_file[fp] = content
        cm = re.search(r"class\s+(\w+)", content)
        cls = cm.group(1) if cm else fp.stem
        if ".text" not in content and "SetText" not in content:
            continue
        for m in FINDTMP_NAME_RE.finditer(content):
            runtime_tmp_names[m.group(1)].add(cls)
        for m in FIND_CHILD_NAME_RE.finditer(content):
            runtime_tmp_names[m.group(1)].add(cls)
        for m in re.finditer(r'(?:const|public const)\s+string\s+\w+\s*=\s*"([^"]+)"', content):
            val = m.group(1)
            if "/" in val or "BG" in val or "按钮" in val or "文字" in val:
                static_path_refs.add(val)

    return CodeIndex(
        files=files,
        content_by_file=content_by_file,
        runtime_tmp_names=runtime_tmp_names,
        static_path_refs=static_path_refs,
    )


def classify_entry(
    go_name: str,
    hierarchy: str,
    text: str,
    file_path: str,
    is_prefab: bool,
    is_template_prefab: bool,
    code_index: CodeIndex,
    scripts_root: Path,
) -> Tuple[str, str]:
    """Return (classification, detail)."""
    path_lower = hierarchy.lower()
    name_lower = go_name.lower()

    for hint in TEMPLATE_NAME_HINTS:
        if hint.lower() in name_lower or hint.lower() in path_lower:
            return "模板占位", f"层级/对象名含「{hint}」"
    if is_template_prefab:
        return "模板占位", "卡面/条目标准模板预制体"

    for hint in TEMPLATE_TEXT_HINTS:
        if hint in text and ("模板" in path_lower or "模版" in path_lower or "占位" in path_lower):
            return "模板占位", f"文本含「{hint}」且位于模板/占位"

    # Runtime via FindTmp / named child binding
    search_names = {go_name}
    for p in hierarchy.split("/")[-4:]:
        if p and not p.startswith("<"):
            search_names.add(p)

    runtime_writers: Set[str] = set()
    for sn in search_names:
        for cls in code_index.runtime_tmp_names.get(sn, set()):
            runtime_writers.add(cls)

    if runtime_writers:
        return "运行时覆写", ", ".join(sorted(runtime_writers))

    # Cheat tool: only input/notice/placeholder are runtime-written
    if "作弊工具" in hierarchy:
        runtime_cheat_names = (
            "notice text", "Placeholder", "InputField", "Text Area", "添加遗物选项",
        )
        if any(n.lower() in hierarchy.lower() or n.lower() in go_name.lower() for n in runtime_cheat_names):
            return "运行时覆写", "CheatToolPanelController"
        if "作弊工具提示" in hierarchy or "标准世界文字" in hierarchy:
            return "静态标签", "场景序列化标签（作弊选项名）"

    # Card face / HUD numeric slots
    numeric_slot_names = ("攻击数值", "血量数值", "护甲数值", "行动计数", "金币数值", "计数")
    if go_name in numeric_slot_names or any(n in hierarchy for n in numeric_slot_names):
        for cls_map in (
            ("PlayerInfoHudPresenter", ("血量", "金币", "护甲")),
            ("CardFaceSlotNodeMap", ("攻击数值", "血量数值", "护甲数值")),
            ("RelicIconSlotView", ("计数",)),
        ):
            cls, hints = cls_map
            if any(h in hierarchy or h in go_name for h in hints):
                return "运行时覆写", cls

    # Known presenter bindings by scene object / path constants
    known_runtime: Tuple[str, str, Tuple[str, ...]] = (
        ("FloorHintPresenter", "大楼层提示,小房间提示,楼层提示", ("大楼层提示", "小房间提示", "楼层提示")),
        ("BattleInfoPreviewPresenter", "战斗信息展示BG", ("战斗信息展示BG", "房间信息")),
        ("BoardBriefTipPresenter", "简要解释文字框", ("简要解释",)),
        ("RunSaveLoadPanel", "存档/读档", ("保存条目模板", "加载条目模板", "存档/读档模块")),
    )
    for cls, _, hints in known_runtime:
        if any(h in hierarchy for h in hints):
            return "运行时覆写", cls
    for sp in code_index.static_path_refs:
        if sp in hierarchy or sp.endswith(go_name):
            return "静态标签", f"代码路径常量引用「{sp}」"

    # 标准世界文字 prefab instances used as button labels are static unless bound above
    if "标准世界文字" in hierarchy or go_name in ("按钮文字", "标题", "副标题文字", "版本号文字"):
        return "静态标签", "标准世界文字实例，脚本未绑定写入"

    if is_prefab:
        return "待确认", "预制体文本，未在脚本中找到明确引用"
    return "静态标签", "脚本中未发现 FindTmp/路径绑定"


def categorize_panel(hierarchy: str, go_name: str, file_path: str) -> str:
    h = hierarchy + "/" + go_name + "/" + file_path
    rules = [
        ("主菜单 MainPanel", ("mainpanel", "主菜单", "mainmenu", "title", "开始", "start", "教学")),
        ("人物选择", ("人物选择", "characterselect", "选角", "角色选择", "heroselect", "角色槽", "出发按钮", "提示文字")),
        ("结算面板", ("结算", "result", "victory", "defeat", "gameover", "胜利", "失败", "结束", "结算面板", "数据_进度", "遗物标题")),
        ("局内功能菜单", ("pause", "暂停", "功能菜单", "ingamemenu", "esc", "设置", "options", "按键控制")),
        ("存档读档", ("save", "load", "存档", "读档", "slot", "保存", "加载")),
        ("右键详述", ("详述", "detail", "tooltip", "词条详细", "效果信息", "右键")),
        ("简要解释", ("简要", "brief", "hint", "解释", "介绍", "字段介绍")),
        ("作弊工具", ("作弊", "cheat", "debug", "gm")),
        ("HUD", ("hud", "血", "hp", "mana", "金币", "gold", "回合", "turn", "层数", "floor")),
    ]
    hl = h.lower()
    for label, kws in rules:
        for kw in kws:
            if kw.lower() in hl:
                return label
    if "prefab" in file_path.lower() or ".prefab" in file_path:
        if any(x in hl for x in ("卡", "card", "遗物", "relic", "房间", "room")):
            return "其他(卡面/房间模板)"
    return "其他"


# ---------------------------------------------------------------------------
# Font asset resolution
# ---------------------------------------------------------------------------

def build_guid_index(project_root: Path) -> Dict[str, Path]:
    index: Dict[str, Path] = {}
    for meta in project_root.rglob("*.meta"):
        try:
            head = meta.read_text(encoding="utf-8", errors="replace")[:300]
        except OSError:
            continue
        gm = GUID_RE.search(head)
        if gm:
            asset = meta.with_suffix("")
            if asset.suffix:  # real asset
                index[gm.group(1).lower()] = asset
    return index


def analyze_font_asset(asset_path: Path, project_root: Path) -> dict:
    rel_path = str(asset_path)
    try:
        rel_path = asset_path.relative_to(project_root).as_posix()
    except ValueError:
        pass
    info = {
        "path": rel_path,
        "name": asset_path.stem,
        "char_count": 0,
        "atlas_mode": "unknown",
        "has_ascii": False,
        "ascii_sample": [],
    }
    if not asset_path.exists():
        return info
    try:
        content = asset_path.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return info
    info["char_count"] = content.count("m_Unicode:")
    if "m_AtlasPopulationMode: 1" in content:
        info["atlas_mode"] = "Dynamic"
    elif "m_AtlasPopulationMode: 0" in content:
        info["atlas_mode"] = "Static"
    # Check unicode 65-122 (A-Z, a-z)
    found = []
    for cp in list(range(65, 91)) + list(range(97, 123)):
        if f"m_Unicode: {cp}" in content:
            found.append(cp)
    info["has_ascii"] = len(found) >= 52
    info["ascii_sample"] = found[:10]
    return info


# ---------------------------------------------------------------------------
# Scan entry
# ---------------------------------------------------------------------------

@dataclass
class ScanRow:
    file: str
    hierarchy: str
    component: str
    active: str
    text: str
    classification: str
    writer: str
    translate: str
    panel: str
    font_guid: str = ""


def scan_file(
    path: Path,
    project_root: Path,
    code_index: CodeIndex,
    scripts_root: Path,
    guid_index: Optional[Dict[str, Path]] = None,
) -> List[ScanRow]:
    game_objects, transforms, text_components = parse_unity_file(path)
    if guid_index is not None and path.suffix == ".unity":
        text_components = text_components + extract_prefab_text_overrides(
            path, game_objects, transforms, guid_index
        )
    go_to_tf = build_go_to_transform(transforms)
    tf_parent = build_transform_parent(transforms)
    rel = path.relative_to(project_root).as_posix()
    is_prefab = path.suffix == ".prefab"
    is_template_prefab = is_prefab and ("模板" in path.name or "模版" in path.name)
    rows: List[ScanRow] = []
    seen: Set[Tuple[str, str]] = set()

    for tc in text_components:
        if tc.source == "prefab_override":
            hierarchy = tc.hierarchy_override
            go_name = hierarchy.split("/")[-1] if hierarchy else ""
            active = tc.active_override or "1"
        else:
            go = game_objects.get(tc.game_object_id)
            go_name = go.name if go else f"<GO:{tc.game_object_id}>"
            active = "1" if (go is None or go.is_active) else "0"
            hierarchy = get_hierarchy_path(tc.game_object_id, game_objects, go_to_tf, tf_parent)

        dedup_key = (hierarchy, tc.text)
        if dedup_key in seen:
            continue
        seen.add(dedup_key)

        comp_type = tc.component_type or "Text"
        if "TextMeshProUGUI" in comp_type:
            comp_short = "TextMeshProUGUI"
        elif "TextMeshPro" in comp_type:
            comp_short = "TextMeshPro"
        elif "UI.Text" in comp_type:
            comp_short = "UnityEngine.UI.Text"
        else:
            comp_short = comp_type.split("::")[-1] if "::" in comp_type else comp_type

        classification, writer = classify_entry(
            go_name, hierarchy, tc.text, rel, is_prefab, is_template_prefab, code_index, scripts_root
        )
        translate = needs_translation(tc.text)
        panel = categorize_panel(hierarchy, go_name, rel)

        rows.append(
            ScanRow(
                file=rel,
                hierarchy=hierarchy,
                component=comp_short,
                active=active,
                text=tc.text.replace("\n", "\\n").replace("|", "\\|"),
                classification=classification,
                writer=writer,
                translate=translate,
                panel=panel,
                font_guid=tc.font_guid,
            )
        )
    return rows


def collect_prefab_paths(project_root: Path) -> List[Path]:
    paths: List[Path] = []
    exclude_prefixes = (
        "Assets/Plugins/",
        "Assets/QFramework/",
        "Assets/Samples/",
        "Assets/Arts/",
    )
    for pattern in ("Assets/Prefabs/**/*.prefab", "Assets/Resources/**/*.prefab"):
        for p in project_root.glob(pattern):
            rel = p.relative_to(project_root).as_posix()
            if any(rel.startswith(ex) for ex in exclude_prefixes):
                continue
            paths.append(p)
    return sorted(paths)


def escape_md_cell(s: str) -> str:
    return s.replace("|", "\\|").replace("\n", " ")


def generate_report(
    project_root: Path,
    main_rows: List[ScanRow],
    prefab_rows: List[ScanRow],
    uitest_count: int,
    testscene_count: int,
    font_infos: Dict[str, dict],
    script_path: str,
) -> str:
    lines: List[str] = []
    lines.append("# 场景与预制体文本本地化盘点")
    lines.append("")
    lines.append(f"> 生成工具：`{script_path}` · 扫描日期：2026-08-12")
    lines.append("")

    # Summary
    main_trans = sum(1 for r in main_rows if r.translate == "是")
    main_static = sum(1 for r in main_rows if r.classification == "静态标签")
    main_runtime = sum(1 for r in main_rows if r.classification == "运行时覆写")
    main_template = sum(1 for r in main_rows if r.classification == "模板占位")
    prefab_trans = sum(1 for r in prefab_rows if r.translate == "是")

    lines.append("## 摘要")
    lines.append("")
    lines.append(f"| 范围 | 文本条数 | 需翻译 | 静态标签 | 运行时覆写 | 模板占位 |")
    lines.append(f"|------|---------|--------|---------|-----------|---------|")
    lines.append(
        f"| MainScene.unity | {len(main_rows)} | {main_trans} | {main_static} | {main_runtime} | {main_template} |"
    )
    lines.append(
        f"| 自有 Prefab ({len(set(r.file for r in prefab_rows))} 个文件) | {len(prefab_rows)} | {prefab_trans} | — | — | — |"
    )
    lines.append(f"| UITestSence.unity | {uitest_count} | （低优先，未逐条列出） | | | |")
    lines.append(f"| TestScene.unity | {testscene_count} | （低优先，未逐条列出） | | | |")
    lines.append("")

    # Fonts
    lines.append("## TMP 字体资产")
    lines.append("")
    lines.append("| 路径 | 名称 | 字表条目(约) | Atlas 模式 | ASCII a-zA-Z |")
    lines.append("|------|------|-------------|-----------|--------------|")
    for guid, info in sorted(font_infos.items(), key=lambda x: x[1]["path"]):
        ascii_status = "已含" if info["has_ascii"] else "未检出/不全"
        lines.append(
            f"| `{info['path']}` | {info['name']} | ~{info['char_count']} | {info['atlas_mode']} | {ascii_status} |"
        )
    lines.append("")
    lines.append(
        "> **英文字形初判**：在字表 YAML 中检索 `m_Unicode: 65`–`122` 范围；"
        "若 52 个字母均存在则标「已含」，否则标「未检出/不全」。Dynamic 模式运行时可补字。"
    )
    lines.append("")

    # MainScene tables by panel
    lines.append("## MainScene.unity 文本清单")
    lines.append("")
    panels_order = [
        "主菜单 MainPanel",
        "人物选择",
        "结算面板",
        "局内功能菜单",
        "存档读档",
        "右键详述",
        "简要解释",
        "作弊工具",
        "HUD",
        "其他",
    ]
    by_panel: Dict[str, List[ScanRow]] = defaultdict(list)
    for r in main_rows:
        by_panel[r.panel].append(r)

    for panel in panels_order:
        rows = by_panel.get(panel, [])
        if not rows:
            continue
        lines.append(f"### {panel}（{len(rows)} 条）")
        lines.append("")
        lines.append(
            "| 层级路径 | 组件 | active | 当前文本(解码) | 静态/运行时覆写 | 是否需翻译 |"
        )
        lines.append("|----------|------|--------|---------------|----------------|-----------|")
        for r in rows:
            cls = f"{r.classification}({r.writer})" if r.writer else r.classification
            lines.append(
                f"| `{escape_md_cell(r.hierarchy)}` | {r.component} | {r.active} "
                f"| {escape_md_cell(r.text)} | {escape_md_cell(cls)} | {r.translate} |"
            )
        lines.append("")

    # Prefab section
    lines.append("## 自有预制体文本清单")
    lines.append("")
    by_file: Dict[str, List[ScanRow]] = defaultdict(list)
    for r in prefab_rows:
        by_file[r.file].append(r)

    for fp in sorted(by_file.keys()):
        rows = by_file[fp]
        lines.append(f"### `{fp}`（{len(rows)} 条）")
        lines.append("")
        lines.append(
            "| 层级路径 | 组件 | active | 当前文本(解码) | 静态/运行时覆写 | 是否需翻译 |"
        )
        lines.append("|----------|------|--------|---------------|----------------|-----------|")
        for r in rows:
            cls = f"{r.classification}({r.writer})" if r.writer else r.classification
            lines.append(
                f"| `{escape_md_cell(r.hierarchy)}` | {r.component} | {r.active} "
                f"| {escape_md_cell(r.text)} | {escape_md_cell(cls)} | {r.translate} |"
            )
        lines.append("")

    # Arts note
    lines.append("## 排除范围说明")
    lines.append("")
    lines.append(
        "- 已排除：`Assets/Plugins/**`、`Assets/QFramework/**`、`Assets/Samples/**`、`Assets/Arts/**`（美术演示）"
    )
    lines.append(
        "- `Assets/Arts/Images/PixelartCardTCG/Demo/Samples/` 下有演示卡预制体含英文样例文本，**非生产引用**，未纳入上表。"
    )
    lines.append("")

    lines.append("## 纯运行时文本面板（场景内无序列化 m_text）")
    lines.append("")
    lines.append(
        "下列面板的主要文案**不在** `.unity` YAML 中，由 C# 在运行时写入（本次场景扫描无法逐条列出，需另做代码/内容串表盘点）："
    )
    lines.append("")
    lines.append("| 面板 | 场景节点 | 写入类 | 说明 |")
    lines.append("|------|---------|--------|------|")
    lines.append("| 人物选择 | `UI面板/人物选择BG` | `CharacterSelectPanel` | 提示文字、属性文字、角色名（`displayName`） |")
    lines.append("| 结算面板 | `UI面板/结算面板BG` | `RunSummaryPanel` | 标题/副标题/进度/金币/属性/遗物/种子等 |")
    lines.append("| 存档槽位正文 | 加载/保存条目模板 | `RunSaveLoadPanel` | 克隆模板后写入日期+角色名 |")
    lines.append("| 卡面/词条正文 | 各 `*标准模板` 预制体 | `CardFacePresentationBinder` 等 | 名称、描述、数值 |")
    lines.append("| 楼层/回收提示 | `楼层提示/*`、`CardRecycleNotice` | `FloorHintPresenter`、`CardHandManagerSingleton` | 如 `楼层·Ⅱ`、`+10` 金币 |")
    lines.append("")

    # Script usage
    lines.append("## 脚本用法")
    lines.append("")
    lines.append("```bash")
    lines.append(f'python "{script_path}" \\')
    lines.append('  --project "C:/Users/jinji/Documents/GitHub/Ninegrid Gambit" \\')
    lines.append("  --report")
    lines.append("```")
    lines.append("")
    lines.append("参数：")
    lines.append("- `--file <path>`：扫描单个 .unity/.prefab，输出 TSV 到 stdout")
    lines.append("- `--project <root>`：项目根目录")
    lines.append("- `--report`：全量扫描并写入 `Assets/Notes/Localization/inventory-scene-texts.md`")
    lines.append("- `--tsv <path>`：将全量结果写入 TSV 文件")
    lines.append("")

    return "\n".join(lines)


def count_texts_in_file(path: Path, guid_index: Optional[Dict[str, Path]] = None) -> int:
    if not path.exists():
        return 0
    gos, tfs, tcs = parse_unity_file(path)
    if guid_index is not None and path.suffix == ".unity":
        tcs = tcs + extract_prefab_text_overrides(path, gos, tfs, guid_index)
    return len(tcs)


def main():
    parser = argparse.ArgumentParser(description="Scan Unity scene/prefab for UI text")
    parser.add_argument("--project", type=Path, default=None)
    parser.add_argument("--file", type=Path, default=None)
    parser.add_argument("--report", action="store_true")
    parser.add_argument("--tsv", type=Path, default=None)
    args = parser.parse_args()

    script_dir = Path(__file__).resolve().parent
    project_root = args.project or script_dir.parents[3]  # Assets/Notes/Localization/tools -> repo root
    scripts_root = project_root / "Assets" / "Scripts"

    if args.file:
        code_index = build_code_index(scripts_root)
        guid_index = build_guid_index(project_root)
        rows = scan_file(args.file.resolve(), project_root, code_index, scripts_root, guid_index)
        print("file\thierarchy\tcomponent\tactive\ttext\tclassification\twriter\ttranslate\tpanel")
        for r in rows:
            print(
                f"{r.file}\t{r.hierarchy}\t{r.component}\t{r.active}\t{r.text}\t"
                f"{r.classification}\t{r.writer}\t{r.translate}\t{r.panel}"
            )
        return

    if not args.report and not args.tsv:
        parser.print_help()
        sys.exit(1)

    print("Building code index...", file=sys.stderr)
    code_index = build_code_index(scripts_root)
    guid_index = build_guid_index(project_root)

    main_scene = project_root / "Assets/Scenes/MainScene.unity"
    print(f"Scanning {main_scene}...", file=sys.stderr)
    main_rows = scan_file(main_scene, project_root, code_index, scripts_root, guid_index)

    prefab_paths = collect_prefab_paths(project_root)
    prefab_rows: List[ScanRow] = []
    for pp in prefab_paths:
        prefab_rows.extend(scan_file(pp, project_root, code_index, scripts_root, guid_index))

    uitest_count = count_texts_in_file(project_root / "Assets/Scenes/UITestSence.unity", guid_index)
    testscene_count = count_texts_in_file(project_root / "Assets/Scenes/TestScene.unity", guid_index)

    # Fonts
    all_guids: Set[str] = set()
    for r in main_rows + prefab_rows:
        if r.font_guid:
            all_guids.add(r.font_guid)

    font_infos: Dict[str, dict] = {}
    for guid in all_guids:
        asset = guid_index.get(guid)
        if asset:
            rel = asset.as_posix()
            if "/Plugins/" in rel or rel.startswith("Assets/Plugins"):
                continue  # skip third-party damage numbers font etc.
            font_infos[guid] = analyze_font_asset(asset, project_root)
        else:
            font_infos[guid] = {"path": f"<unknown guid:{guid}>", "name": "?", "char_count": 0,
                                "atlas_mode": "?", "has_ascii": False, "ascii_sample": []}

    all_rows = main_rows + prefab_rows

    if args.tsv:
        with args.tsv.open("w", encoding="utf-8") as out:
            out.write("file\thierarchy\tcomponent\tactive\ttext\tclassification\twriter\ttranslate\tpanel\tfont_guid\n")
            for r in all_rows:
                out.write(
                    f"{r.file}\t{r.hierarchy}\t{r.component}\t{r.active}\t{r.text}\t"
                    f"{r.classification}\t{r.writer}\t{r.translate}\t{r.panel}\t{r.font_guid}\n"
                )
        print(f"Wrote TSV: {args.tsv}", file=sys.stderr)

    if args.report:
        report_path = project_root / "Assets/Notes/Localization/inventory-scene-texts.md"
        report_path.parent.mkdir(parents=True, exist_ok=True)
        script_rel = Path(__file__).resolve().relative_to(project_root).as_posix()
        report = generate_report(
            project_root,
            main_rows,
            prefab_rows,
            uitest_count,
            testscene_count,
            font_infos,
            script_rel,
        )
        report_path.write_text(report, encoding="utf-8")
        print(f"Wrote report: {report_path}", file=sys.stderr)
        # Print summary JSON for parent agent
        summary = {
            "main_scene_total": len(main_rows),
            "main_static_translate": sum(1 for r in main_rows if r.translate == "是" and r.classification == "静态标签"),
            "main_runtime": sum(1 for r in main_rows if r.classification == "运行时覆写"),
            "prefab_total": len(prefab_rows),
            "fonts": list(font_infos.values()),
        }
        print(json.dumps(summary, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
