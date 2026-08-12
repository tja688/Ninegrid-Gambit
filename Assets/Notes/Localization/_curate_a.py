import re, json
from pathlib import Path
from collections import Counter

ROOT = Path(r'C:\Users\jinji\Documents\GitHub\Ninegrid Gambit')
SCRIPTS = ROOT / 'Assets' / 'Scripts'
cjk = re.compile(r'[\u4e00-\u9fff]')
str_re = re.compile(r'"([^"\\]*(?:\\.[^"\\]*)*)"')

EXCLUDE_PATH = [
    '.Editor/', '/Editor/', '/Cheat/', '/Tests/', 'QuickTest', 'DiagTrace', 'PerfTrace',
    'VisualFxLab', 'DevTest', 'VfxParticlePresetLibrary', 'VfxProjectilePresetLibrary',
    'CardChassisPaths', 'ThemeDeckMappingVerifier', 'MusicSystem', 'VfxSystem',
    'BattleSessionCheat', 'AudioCue.cs', 'InteractionAudioCues',
]

DISPLAY_HINTS = {
    'BoardBriefTipCopy': 'BoardBriefTipPresenter → Panels/简要解释文字框 TMP',
    'BoardBriefTipPresenter': 'Panels/简要解释文字框 TMP',
    'FloorHintPresenter': '楼层提示/大楼层提示、小房间提示 TMP',
    'RunSummaryPanel': 'UI面板/结算面板BG 各 TMP',
    'CharacterSelectPanel': '人物选择BG/提示文字、属性文字 TMP',
    'RunSaveLoadPanel': '局内功能菜单/存档读档模块 条目 TMP',
    'ShopBoardPresenter': '简要解释文字框 Notice',
    'TavernBoardPresenter': '简要解释文字框 Notice',
    'AttributeBoardPresenter': '简要解释文字框 Notice',
    'RewardBoardPresenter': '简要解释文字框 Notice',
    'GameFlowController': '简要解释文字框 Notice',
    'GameFlowOrchestrator': '简要解释文字框 Notice / 结算面板',
    'PhaseSystem': '简要解释文字框 Notice（Core Reason）',
    'BattleInfoPreviewCopySO': '战斗信息预览/房间信息 TMP',
    'BattleInfoPreviewPresenter': '战斗信息预览/房间信息 TMP',
    'CardInspectOverlayPresenter': '右键描述面板 TMP',
    'CardInspectDetailComposer': '右键描述 TMP（JSON 为主）',
    'CardFaceDescriptionComposer': '卡面描述 TMP（JSON 投影）',
    'CardHandManagerSingleton': 'CardRecycleNotice 标准世界文字 TMP',
    'RelicHudController': '简要解释文字框 Notice',
    'BattleSessionExecutor': '简要解释文字框 Notice',
    'BounceFanChoicePresenter': '弹扇选择 UI TMP',
    'CardFaceSlotNodeMap': '卡面槽位（节点名，部分场景静态）',
    'ContentDefinitions': '内容 Catalog（JSON 投影）',
    'RewardSystem': '简要解释文字框 Notice',
}

def hint_for(path):
    for k, v in DISPLAY_HINTS.items():
        if k in path:
            return v
    return '（运行时文案，上屏途径待接）'


def main():
    A = []
    for p in sorted(SCRIPTS.rglob('*.cs')):
        rel = p.relative_to(ROOT).as_posix()
        if any(x in rel for x in EXCLUDE_PATH):
            continue
        lines = p.read_text(encoding='utf-8', errors='replace').splitlines()
        for i, line in enumerate(lines, 1):
            if not cjk.search(line):
                continue
            stripped = line.strip()
            if stripped.startswith('//') or stripped.startswith('///') or stripped.startswith('*'):
                continue
            if 'Tooltip(' in line or 'AudioCue(' in line:
                continue
            if re.search(r'\.Find\s*\(|FindDeep\s*\(|FindSceneNamed\s*\(|GameObject\.Find\s*\(', line):
                continue
            if re.search(
                r'const\s+string\s+\w*(?:Name|Path|ObjectName|RootObjectName|PanelRootName|'
                r'SlotName|ButtonName|TextName|DecorName|ModuleName|TemplateName)\s*=',
                line,
            ):
                continue
            if 'Debug.' in line:
                continue
            lits = [m.group(1) for m in str_re.finditer(line) if cjk.search(m.group(1))]
            if not lits:
                continue
            A.append({
                'file': rel,
                'line': i,
                'strings': lits,
                'content': stripped[:220],
                'display': hint_for(rel),
            })

    seen = set()
    Au = []
    for e in A:
        k = (e['file'], e['line'])
        if k in seen:
            continue
        seen.add(k)
        Au.append(e)

    fc = Counter(e['file'] for e in Au)
    out = ROOT / 'Assets' / 'Notes' / 'Localization' / '_curated_a.json'
    out.write_text(
        json.dumps({'count': len(Au), 'entries': Au, 'top': fc.most_common(30)}, ensure_ascii=False, indent=2),
        encoding='utf-8',
    )
    print('Curated A:', len(Au))
    for f, c in fc.most_common(12):
        print(c, f)


if __name__ == '__main__':
    main()
