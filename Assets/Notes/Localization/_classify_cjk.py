import re, json
from pathlib import Path
from collections import Counter

ROOT = Path(r'C:\Users\jinji\Documents\GitHub\Ninegrid Gambit')
SCRIPTS = ROOT / 'Assets' / 'Scripts'
cjk = re.compile(r'[\u4e00-\u9fff]')
str_lit = re.compile(r'(?:"([^"\\]*(?:\\.[^"\\]*)*)"|@"([^"]*)")')

B_PATTERNS = [
    r'\.Find\s*\(\s*"',
    r'FindDeep\s*\(\s*"',
    r'GameObject\.Find\s*\(\s*"',
    r'FindSceneNamed\s*\(\s*"',
    r'public\s+const\s+string\s+\w*(?:Name|Path|ObjectName|RootObjectName|PanelRootName|SlotName|ButtonName|TextName|DecorName|ChildName|NodeName|AnchorName|ModuleName|TemplateName)\s*=',
    r'private\s+const\s+string\s+\w*(?:Name|Path|ObjectName|TemplateName|ButtonName)\s*=',
    r't\.name\s*==\s*"',
    r'FindChild\s*\([^,]+,\s*"',
    r'childName\s*==\s*"',
]

C_PATH_PARTS = ['.Editor/', '/Editor/', '/Cheat/', '/Tests/', 'QuickTest', 'DiagTrace', 'PerfTrace', 'VisualFxLab', 'DevTest']

C_LINE_PATTERNS = [
    r'Debug\.(Log|LogWarning|LogError|Assert)',
    r'Trace\.', r'PerfTrace', r'Assert\.',
    r'\[Tooltip\(', r'///', r'^\s*//',
    r'UNITY_EDITOR', r'DEVELOPMENT_BUILD',
    r'AudioCue', r'VfxCue',
    r'InteractionAudioCues\.',
    r'CreateMenuRequest',
]

A_PATTERNS = [
    r'ShowNotice\s*\(',
    r'SetText\s*\(',
    r'\.text\s*=',
    r'BoardBriefTipCopy',
    r'victoryMessage|defeatMessage',
    r'const\s+string\s+\w*(?:Tip|Notice|Message|Copy|Label|Title|Hint)\s*=',
    r'return\s+"[^"]*[\u4e00-\u9fff]',
    r'\$"[^"]*[\u4e00-\u9fff]',
    r'FormatEntry',
    r'BuildAvatarStatsLine',
    r'Populate\(',
    r'BuildQuickTestPickerMenuText',
    r'ForRoom|ForNavigation|ForContentId|ForFloor|ForRoomKind',
    r'RelicHud|recycle.*价值|金币不足',
]

def classify_file(path: str) -> str:
    p = path.replace('\\', '/')
    if any(x in p for x in C_PATH_PARTS):
        return 'C_file'
    if '.Editor' in p:
        return 'C_file'
    return 'runtime'

def classify_line(line: str, path: str) -> str:
    if classify_file(path) == 'C_file':
        return 'C'
    stripped = line.strip()
    if stripped.startswith('//') or stripped.startswith('///') or stripped.startswith('*'):
        return 'C'
    for pat in B_PATTERNS:
        if re.search(pat, line):
            return 'B'
    for pat in C_LINE_PATTERNS:
        if re.search(pat, line):
            return 'C'
    for pat in A_PATTERNS:
        if re.search(pat, line):
            return 'A'
    if cjk.search(line) and re.search(r'"[^"]*[\u4e00-\u9fff][^"]*"', line):
        if '[SerializeField]' in line or 'Tooltip' in line:
            return 'C'
        return 'A'
    return 'other'

def extract_cjk_strings(line):
    results = []
    for m in str_lit.finditer(line):
        s = m.group(1) if m.group(1) is not None else m.group(2)
        if s and cjk.search(s):
            results.append(s)
    return results

entries = {'A': [], 'B': [], 'C': [], 'other': []}
file_counts = {k: Counter() for k in entries}

for p in sorted(SCRIPTS.rglob('*.cs')):
    rel = p.relative_to(ROOT).as_posix()
    try:
        lines = p.read_text(encoding='utf-8', errors='replace').splitlines()
    except Exception:
        continue
    for i, line in enumerate(lines, 1):
        if not cjk.search(line):
            continue
        cat = classify_line(line, rel)
        strings = extract_cjk_strings(line)
        entry = {'file': rel, 'line': i, 'content': line.strip()[:300], 'strings': strings}
        entries[cat].append(entry)
        file_counts[cat][rel] += 1

a_unique = []
seen = set()
for e in entries['A']:
    key = (e['file'], e['line'])
    if key in seen:
        continue
    seen.add(key)
    a_unique.append(e)

summary = {
    'counts': {k: len(v) for k, v in entries.items()},
    'a_unique_count': len(a_unique),
    'a_top_files': file_counts['A'].most_common(40),
    'b_top_files': file_counts['B'].most_common(40),
    'c_top_files': file_counts['C'].most_common(40),
    'A': a_unique,
    'B': entries['B'],
    'C_file_summary': [(f, c) for f, c in file_counts['C'].most_common(80)],
}
out = ROOT / 'Assets' / 'Notes' / 'Localization' / '_classified.json'
out.write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')
print('A:', len(a_unique), 'B:', len(entries['B']), 'C:', len(entries['C']))
for f, c in file_counts['A'].most_common(10):
    print(f'  A {c:3d} {f}')
