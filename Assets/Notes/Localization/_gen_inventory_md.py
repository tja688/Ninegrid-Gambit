"""Generate inventory-code-strings.md from scan artifacts."""
import json
from pathlib import Path
from collections import Counter, defaultdict
import re

ROOT = Path(r'C:\Users\jinji\Documents\GitHub\Ninegrid Gambit')
OUT = ROOT / 'Assets' / 'Notes' / 'Localization' / 'inventory-code-strings.md'

# Player-visible A: whitelist files + strong patterns
A_FILE_MARKERS = [
    'BoardBriefTipCopy.cs', 'RunSummaryPanel.cs', 'CharacterSelectPanel.cs', 'RunSaveLoadPanel.cs',
    'ShopBoardPresenter.cs', 'TavernBoardPresenter.cs', 'AttributeBoardPresenter.cs', 'RewardBoardPresenter.cs',
    'GameFlowController.cs', 'GameFlowOrchestrator.cs', 'PhaseSystem.cs',
    'BattleInfoPreviewPresenter.cs', 'BattleInfoPreviewCopySO.cs',
    'FloorHintPresenter.cs', 'BoardBriefTipPresenter.cs',
    'CardHandManagerSingleton.cs', 'RelicHudController.cs',
    'BattleSessionExecutor.Choice.cs', 'GameFlowShellSystem.cs',
    'BounceFanChoicePresenter.cs', 'InRoomItemAcquirePresentation.cs',
    'ContentDefinitions.cs',
]

A_LINE_PATTERNS = [
    r'ShowNotice\s*\(',
    r'SetText\s*\(',
    r'\.text\s*=',
    r'ShowHint\s*\(',
    r'FormatEntry',
    r'ForRoom|ForNavigation|ForContentId|ForFloor|ForRoomKind|BuildRoomTypeLabel',
    r'victoryMessage|defeatMessage',
    r'Populate\s*\(',
    r'Reject\s*\([^)]*"[^"]*[\u4e00-\u9fff]',
    r'return\s+"[^"]*[\u4e00-\u9fff]',
    r'const\s+string\s+\w*(?:Tip|Notice|Message|Copy)\s*=',
    r'roomInfoTemplate',
    r'BuildDefaultRoomInfoTemplate',
]

A_EXCLUDE_FILE = [
    'CardFaceSlotNodeMap', 'CardFaceSlotRegistrySO', 'CardChassisPaths', 'AudioAssetPaths',
    'CardDescriptionTokenRules', 'BattleVfxCues', 'MMSoundManagerAudioPlaybackAdapter',
    'CardEffectManager', 'CardDeckManagerSingleton',
]

cjk = re.compile(r'[\u4e00-\u9fff]')


def is_a_entry(e):
    f = e['file']
    content = e['content']
    if any(x in f for x in A_EXCLUDE_FILE):
        return False
    if 'Debug.Log' in content or 'Debug.LogWarning' in content or 'Debug.LogError' in content:
        return False
    if content.startswith('+$') or content.startswith('+ $"') or content.startswith('+"'):
        return False
    if '[Tooltip(' in content or '[Header(' in content:
        return False
    if 'BattleInfoPreviewPresenter' in f:
        if not any(k in content for k in ['roomInfoTemplate', '.text =', 'SetText', 'BuildDefault', 'return "房间']):
            return False
    if 'GameFlowOrchestrator' in f:
        if not any(k in content for k in ['ShowNotice', '教学完成', '胜利', '失败', 'view?.ShowNotice']):
            if '[GameFlow]' in content or '快速测试' in content or 'BuildQuickTest' in content:
                return False
    if any(f.endswith(m) or m in f for m in A_FILE_MARKERS):
        return True
    for pat in A_LINE_PATTERNS:
        if re.search(pat, content):
            return True
    return False


def load_json(name):
    return json.loads((ROOT / 'Assets' / 'Notes' / 'Localization' / name).read_text(encoding='utf-8'))


def main():
    curated = load_json('_curated_a.json')
    classified = load_json('_classified.json')

    a_entries = [e for e in curated['entries'] if is_a_entry(e)]
    # add asset
    asset_path = 'Assets/Resources/Flow/BattleInfoPreviewCopy.asset'
    a_entries.append({
        'file': asset_path,
        'line': 18,
        'strings': ['房间类型：{room}\\n楼层：{floor}\\n进度：{progress}'],
        'content': 'roomInfoTemplate (SO 序列化字段)',
        'display': '战斗信息预览/房间信息 TMP',
    })

  # dedupe
    seen = set()
    a_final = []
    for e in a_entries:
        k = (e['file'], e['line'])
        if k in seen:
            continue
        seen.add(k)
        a_final.append(e)

    b_entries = classified['B']
    b_by_file = Counter(e['file'] for e in b_entries)

    c_top = classified.get('c_top_files') or classified.get('C_file_summary', [])

    lines = []
    lines.append('# 代码硬编码中文与文本上屏路径盘点')
    lines.append('')
    lines.append('> 只读盘点 · 范围 `Assets/Scripts/**`（排除 Plugins/QFramework）· 生成于 2026-08-12')
    lines.append('')
    lines.append('## 扫描摘要')
    lines.append('')
    lines.append('| 类别 | 命中行数（含注释/元数据） | 本报告条目 |')
    lines.append('|------|--------------------------|-----------|')
    lines.append(f'| A 玩家可见运行时文案 | — | **{len(a_final)}** |')
    lines.append(f'| B 场景查找串（禁止翻译） | {len(b_entries)} | {len(b_by_file)} 文件 |')
    lines.append(f'| C 开发/编辑器/诊断 | {classified["counts"]["C"]} | 文件级汇总 |')
    lines.append(f'| 原始 CJK 命中总计 | 7153 | — |')
    lines.append('')

    # A table
    lines.append('## A 类：玩家可见运行时文案（必须翻译）')
    lines.append('')
    lines.append('| 文件:行号 | 字符串内容 | 上屏途径 |')
    lines.append('|-----------|-----------|----------|')
    for e in sorted(a_final, key=lambda x: (x['file'], x['line'])):
        loc = f"`{e['file']}:{e['line']}`"
        txt = ' / '.join(e['strings']) if e['strings'] else e['content'][:80]
        txt = txt.replace('|', '\\|')
        disp = e['display']
        lines.append(f'| {loc} | {txt} | {disp} |')
    lines.append('')

    # A concentration
    a_fc = Counter(e['file'] for e in a_final)
    lines.append('### A 类集中度（Top 10 文件）')
    lines.append('')
    for f, c in a_fc.most_common(10):
        lines.append(f'- `{f}`：{c} 处')
    lines.append('')

    # B
    lines.append('## B 类：场景对象名 / 节点查找串（⚠️ 禁止翻译）')
    lines.append('')
    lines.append('> **警告**：下列字符串用于 `transform.Find` / `FindDeep` / `GameObject.Find` / `FindSceneNamed` / `const *Name` 等场景绑定。翻译后会断查找、断按钮接线。')
    lines.append('')
    lines.append('| 文件 | 命中行数 | 典型用途 |')
    lines.append('|------|---------|----------|')
    b_examples = {
        'PlayerAudioSettingsPanel.cs': '面板根/关闭钮/音量滑条节点名',
        'CharacterSelectPanel.cs': '人物选择槽位/按钮节点名',
        'RunSummaryPanel.cs': '结算面板 TMP 子节点名',
        'CardInspectOverlayPresenter.cs': '右键描述/半黑屏BG',
        'GameFlowController.cs': 'MainPanel 按钮 FindDeep（StartRun 等英文名，无 CJK）',
        'FloorHintPresenter.cs': '楼层提示根对象名',
        'BoardBriefTipPresenter.cs': '简要解释文字框',
        'PlayerInfoHudPresenter.cs': '玩家信息 HUD 子节点名',
        'CardHandManagerSingleton.cs': '标准世界文字 (2) 回收提示子节点',
        'BattleInfoPreviewPresenter.cs': '玩家/怪物/环境 分组节点',
    }
    for f, c in b_by_file.most_common(40):
        short = f.split('/')[-1]
        ex = ''
        for k, v in b_examples.items():
            if k in short:
                ex = v
                break
        if not ex:
            ex = '场景节点 Find/绑定'
        lines.append(f'| `{f}` | {c} | {ex} |')
    lines.append('')
    lines.append(f'**合计**：{len(b_entries)} 行 / {len(b_by_file)} 文件')
    lines.append('')

    # C
    lines.append('## C 类：开发 / 编辑器 / 诊断（不翻或后翻）')
    lines.append('')
    lines.append('| 分区 | 代表路径 | 约行数 | 说明 |')
    lines.append('|------|---------|--------|------|')
    c_groups = [
        ('Content.Editor 工具窗', 'NineGrid.Content.Editor/', 0),
        ('Presentation.Editor', 'NineGrid.Presentation/Editor/', 0),
        ('Cheat 作弊面板', 'Presentation/Cheat/', 0),
        ('QuickTest 通道', 'QuickTest', 0),
        ('Tests 自动化', '/Tests/', 0),
        ('PerfTrace / DiagTrace', 'Diagnostics/', 0),
        ('AudioCue / VfxCue 元数据 note', 'AudioCue.cs / VfxCue', 0),
        ('VisualFxLab', 'VisualFxLab/', 0),
        ('DevTest', 'NineGrid.DevTest/', 0),
    ]
    for e in c_top[:60]:
        f, c = e[0], e[1]
        for i, (name, marker, _) in enumerate(c_groups):
            if marker.replace('/', '') in f.replace('/', '') or marker in f:
                c_groups[i] = (name, marker, c_groups[i][2] + c)
                break
    for name, marker, cnt in c_groups:
        if cnt:
            lines.append(f'| {name} | `{marker}*` | {cnt}+ | 宏隔离或仅 Editor/DEVELOPMENT_BUILD |')
    lines.append('')
    lines.append('- `Cheat/`：`#if UNITY_EDITOR || DEVELOPMENT_BUILD` 编译隔离（`CheatToolPanelController` 等）')
    lines.append('- `QuickTest`：`GameFlowShellSystem.BuildQuickTestPickerMenuText` 等主菜单 `\\` 通道文案')
    lines.append('- 日志 / Assert / Trace：玩家不可见')
    lines.append('- `AudioCue` 第二参数中文 note：音频工作台元数据，非 UI')
    lines.append('')

    # Path diagram - manual
    lines.append('## 文本上屏路径图')
    lines.append('')
    lines.append('玩家最终看到的文字，按来源分为五类：')
    lines.append('')
    lines.append('```mermaid')
    lines.append('flowchart TB')
    lines.append('  subgraph sources [五大来源]')
    lines.append('    JSON["内容 JSON 投影\\nCardPresentationConfig / GameContentCatalog"]')
    lines.append('    Glossary["词条表\\nCardGlossaryTerms + Resources SO"]')
    lines.append('    SceneTMP["场景静态 TMP\\n预制文案/按钮标签"]')
    lines.append('    CodeLit["代码字面量\\n本报告 A 类"]')
    lines.append('    Plugin["插件默认\\nDamageNumbersPro 等"]')
    lines.append('  end')
    lines.append('  JSON --> Mapper[CoreCardPresentationMapper]')
    lines.append('  Mapper --> Projector[CardFaceDescriptionProjector]')
    lines.append('  Projector --> Composer[CardFaceDescriptionComposer]')
    lines.append('  Composer --> FaceTMP[卡面 description TMP]')
    lines.append('  JSON --> Inspect[CardInspectDetailComposer]')
    lines.append('  Glossary --> Composer')
    lines.append('  Glossary --> GlossaryView[CardInspectGlossaryListView]')
    lines.append('  Inspect --> Overlay[CardInspectOverlayPresenter]')
    lines.append('  Overlay --> InspectTMP[右键描述 TMP]')
    lines.append('  CodeLit --> BriefCopy[BoardBriefTipCopy]')
    lines.append('  BriefCopy --> BriefTip[BoardBriefTipPresenter.bodyText]')
    lines.append('  CodeLit --> Panels[RunSummary / CharacterSelect / SaveLoad 等]')
    lines.append('  SceneTMP --> Panels')
    lines.append('  Plugin --> DmgNum[DamageNumberManagerSingleton]')
    lines.append('```')
    lines.append('')
    lines.append('### 1. 内容 JSON 投影')
    lines.append('')
    lines.extend([
        '| 类 / 文件 | 作用 | 改文案 |',
        '|----------|------|--------|',
        '| `CoreCardPresentationMapper` (`Flow/CoreCardPresentationMapper.cs`) | 从 `CardPresentationConfigCatalog` 拉 displayName/faceIntro/basicDescription，组装 `CardPresentationSnapshot` | `Assets/Content/**` JSON + CardPresentation 配置 |',
        '| `CardFaceDescriptionProjector` (`Cards/Presentation/CardFaceDescriptionProjector.cs`) | 描述模板 + 运行时参数填充 → 交给 Composer | JSON `basicDescription` + 装配参数 |',
        '| `CardInspectDetailComposer` (`Cards/Presentation/CardInspectDetailComposer.cs`) | 右键背景/牌组介绍 | JSON `faceIntro` / deck `displayName`+`description` |',
        '| `ContentDefinitions` / `GameContentCatalog` | 房间 DisplayName、怪物牌组名等 | Core 内容 JSON |',
    ])
    lines.append('')
    lines.append('### 2. 词条表')
    lines.append('')
    lines.extend([
        '| 类 / 文件 | 作用 | 改文案 |',
        '|----------|------|--------|',
        '| `CardGlossaryTerms` | 解析 `[[展示名]]` / `[code]` 内联图标与词条 | `Resources` 下 Glossary SO / JSON |',
        '| `CardFaceDescriptionComposer` | `[[名]]` 展开、sprite 内联 | 词条表 + 描述字符串 |',
        '| `CardInspectGlossaryListView` | 右键详述效果词条行列表 | 同上 |',
    ])
    lines.append('')
    lines.append('### 3. 场景静态 TMP')
    lines.append('')
    lines.append('- 主菜单 / 功能菜单 / 结算 / 人物选择等 **按钮标签、标题** 多在场景 `UI面板/*` 子物体 TMP 上直接填写（非代码 SetText）')
    lines.append('- 字体：`ChangBanDianSong-12 SDF`、`SmileySans-Oblique-3 SDF`、`Fantasypixelfont SDF`（见 TMP 节）')
    lines.append('')
    lines.append('### 4. 代码字面量（本报告 A 类）')
    lines.append('')
    lines.extend([
        '| 管线 | 关键类 | TMP / 面板 |',
        '|------|--------|------------|',
        '| 悬停一句话 + Notice | `BoardBriefTipCopy` → `BoardBriefTipPresenter` | `Panels/简要解释文字框` |',
        '| 楼层/房间类型 | `BoardBriefTipCopy` + `FloorHintPresenter` | `楼层提示/大楼层提示`、`小房间提示` |',
        '| 胜负 / 教学 / 商店拒因 | `GameFlowController` / `GameFlowOrchestrator` / 各 BoardPresenter `ShowNotice` | 同上简要解释框 |',
        '| 局终结算 | `RunSummaryPanel.Populate` | `结算面板BG` 各 TMP |',
        '| 人物选择拒选 | `CharacterSelectPanel.ShowHint` | `提示文字` TMP |',
        '| 存档列表 | `RunSaveLoadPanel.FormatEntry` | 存档模块条目 TMP |',
        '| 战斗信息预览 | `BattleInfoPreviewCopySO` + `BattleInfoPreviewPresenter` | 预览面板 `房间信息` TMP |',
        '| Core 拒因 | `PhaseSystem.Reject` → `result.Reason` → `ShowNotice` | 简要解释框 |',
        '| 回收预览 | `CardHandManagerSingleton` `+N` | `CardRecycleNotice` TMP |',
    ])
    lines.append('')
    lines.append('### 5. 插件默认文案')
    lines.append('')
    lines.append('- `DamageNumberManagerSingleton`（DamageNumbersPro）：飘字多为数字；前缀/后缀若配置则在插件 SO 中')
    lines.append('- TextMesh Pro 默认 `LiberationSans SDF` 不含 CJK，生产 UI 使用项目自有像素/点宋字体')
    lines.append('')
    lines.append('### 关键链路速查')
    lines.append('')
    lines.extend([
        '| 功能 | 入口 | 文件 |',
        '|------|------|------|',
        '| 卡面描述 | `CardFacePresentationBinder` → `CardFaceDescriptionComposer.Compose` | `Cards/Presentation/` |',
        '| 右键详述 | `CardInspectOverlayPresenter.ShowForCard` | `Flow/CardInspectOverlayPresenter.cs` |',
        '| 简要解释 | `BoardBriefTipPresenter.SetHover` / `ShowNotice` | `Flow/BoardBriefTip/` |',
        '| 战斗预览 | `BattleInfoPreviewPresenter.ShowAsync` | `Flow/BattleInfoPreview/` |',
        '| 结算 | `RunSummaryPanel.TryShowAndWaitAsync` | `Ui/RunSummaryPanel.cs` |',
        '| HUD 数值 | `PlayerInfoHudPresenter` | 仅数字，无中文标签（标签在场景 TMP） |',
    ])
    lines.append('')

    # Main menu buttons
    lines.append('## 主菜单加按钮（语言切换）调研')
    lines.append('')
    lines.append('### 既有惯例（`GameFlowController`）')
    lines.append('')
    lines.append('1. **场景摆放**：主菜单 `MainPanel` 下每个可点项 = 带 `SpriteRenderer`（按钮图）+ `BoxCollider2D`（命中）的子物体；按钮文字若需要则为子节点 **世界空间 `TMP_Text`**（多数主菜单按钮文案 baked 在精灵上）')
    lines.append('2. **序列化 + 回退**：`[SerializeField] Collider2D settingsHit` 等；`EnsureViewBindings()` 里若为空则 `FindDeep("SettingsScreen")` 等按 **英文名** 深搜取 `Collider2D`')
    lines.append('3. **轮询命中**：`Update()` 仅在 `GameFlowShellState.MainMenu` 且功能菜单/半黑屏未打开时；`WorldPointerUtility.TryOverlapColliderOnPlane(worldCamera, hit)` + `WasPrimaryPressedThisFrame()`')
    lines.append('4. **悬停反馈**：`UpdateMenuHover()` 对当前 hover 的 collider 的 transform `localScale *= 1.08`；配合 `InteractionAudioCues` 脉冲')
    lines.append('5. **叠层面板按钮**：`WorldUiHitButton`（`Ui/WorldUiHitButton.cs`）= `BoxCollider2D` + `PointerHitRegistry` + 悬停缩放；用于人物选择/结算/功能菜单，**非主菜单 StartRun 路径**')
    lines.append('6. **功能菜单入口**：主菜单 `菜单按钮` 走 `PlayerAudioSettingsPanel.FunctionMenuHitProxy`（非 GameFlowController 四按钮）')
    lines.append('')
    lines.append('### 候选接法（二选一，不做最终决策）')
    lines.append('')
    lines.append('**候选 A — 对齐主菜单四按钮范式**')
    lines.append('- 场景：在 `MainPanel` 增加 `LanguageToggle`（SpriteRenderer + BoxCollider2D），可选子 TMP 显示「中/EN」')
    lines.append('- 代码：`GameFlowController` 增加 `[SerializeField] Collider2D languageHit` + `FindDeep` 回退；`Update()` 增加命中分支调用 `ILanguageSettingsSystem.Toggle()`')
    lines.append('- 优点：与 Start/Settings/Tutorial/Quit 一致；缺点：需改 `GameFlowController` 与场景')
    lines.append('')
    lines.append('**候选 B — 放进局内功能菜单**')
    lines.append('- 场景：在 `局内功能菜单BG/功能模块` 下加按钮，用 `WorldUiHitButton` + `PlayerAudioSettingsPanel` 式 `WireButton`')
    lines.append('- 代码：扩展 `PlayerAudioSettingsPanel.EnsureBound` 接线；主菜单通过现有 `菜单按钮` 打开后切换语言')
    lines.append('- 优点：复用现成面板范式与 `WorldUiHitButton`；缺点：主菜单不能直接切语言，多一步')
    lines.append('')

    # Settings persistence
    lines.append('## 设置持久化惯例（语言偏好可照此落）')
    lines.append('')
    lines.extend([
        '| 要素 | 音频现状 (`PlayerAudioSettingsSystem`) | 语言建议 |',
        '|------|----------------------------------------|----------|',
        '| 接口 | `IPlayerAudioSettingsSystem : ISystem` | `ILanguageSettingsSystem : ISystem` |',
        '| PlayerPrefs 键 | `NineGrid.PlayerAudioSettings.v1`（JSON） | `NineGrid.LanguagePreference.v1` |',
        '| 默认值 | `Resources/MMSoundManagerSettings` 作者默认 | 可用 `PlatformInfo.LanguageCode`（Steam `schinese`/`english`）作首次默认 |',
        '| 变更通知 | `event Action<Snapshot> Changed` | 同模式，UI/TMP 刷新订阅 |',
        '| 存储抽象 | `IPlayerAudioSettingsStore` → `PlayerPrefsAudioSettingsStore` | 可复用同一 Store 接口 |',
        '| 装配 | `PresentationSceneRoot.WireHosts()` → `PlayerAudioSettingsSystem.EnsureRegistered()` | 同位置注册 |',
        '| 面板 | `PlayerAudioSettingsPanel` 读写 `Current` | 语言按钮调 `SetLocale(code)` |',
    ])
    lines.append('')

    # i18n traces
    lines.append('## 现有 i18n 痕迹')
    lines.append('')
    lines.append('- `Packages/manifest.json`：**无** `com.unity.localization` 包')
    lines.append('- `Assets/Scripts/` 内无 Localization/Locale/i18n 框架；仅：')
    lines.append('  - `IPlatformInfo.LanguageCode` / `PlatformInfo.LanguageCode`（`Flow/Platform/PlatformInfo.cs`）')
    lines.append('  - `SteamPlatformInfo.LanguageCode` → `SteamApps.GetCurrentGameLanguage()`（`NineGrid.SteamBridge`）')
    lines.append('  - Editor Web Workbench `localeCompare(..., "zh-CN")`（排序，非运行时 UI）')
    lines.append('- **结论**：尚无运行时本地化基础设施；Steam 语言仅可作默认检测参考')
    lines.append('')

    # TMP
    lines.append('## TMP 全局设置与项目字体')
    lines.append('')
    lines.extend([
        '| 项 | 值 |',
        '|----|-----|',
        '| TMP Settings 路径 | `Assets/TextMesh Pro/Resources/TMP Settings.asset` |',
        '| 默认字体 | `LiberationSans SDF`（guid `8f586378…`，TMP 自带） |',
        '| 全局 fallback 链 | **空** `m_fallbackFontAssets: []` |',
        '| LiberationSans Fallback 资产 | `LiberationSans SDF - Fallback.asset`（TMP 自带） |',
    ])
    lines.append('')
    lines.append('### 项目自有 TMP FontAsset（排除 Plugins demo）')
    lines.append('')
    lines.extend([
        '| 资产 | 路径 | Fallback 链 | 备注 |',
        '|------|------|------------|------|',
        '| ChangBanDianSong-12 SDF | `Assets/Arts/Fronts/长坂点宋12_1.4.2/` | 空 | 点宋，生产中文 UI 主力候选 |',
        '| SmileySans-Oblique-3 SDF | `Assets/Arts/Fronts/DeYiHei/` | （资产内查） | 得意黑斜体 |',
        '| Fantasypixelfont SDF | `Assets/Arts/Images/Png/2D Pixel Quest Vol3…/Font/` | （资产内查） | 像素 UI 包字体 |',
    ])
    lines.append('')
    lines.append('- 场景 TMP 具体引用由场景盘点另补；代码侧 `TMP Settings` 默认 **不会**自动 fallback 到点宋，各 TMP 组件需单独指定 Font Asset')
    lines.append('')

    OUT.write_text('\n'.join(lines), encoding='utf-8')
    print('Wrote', OUT, 'A entries:', len(a_final))


if __name__ == '__main__':
    main()
