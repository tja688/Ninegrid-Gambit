# 场景与预制体文本本地化盘点

> 生成工具：`Assets/Notes/Localization/tools/scan_unity_texts.py` · 扫描日期：2026-08-12

## 摘要

| 范围 | 文本条数 | 需翻译 | 静态标签 | 运行时覆写 | 模板占位 |
|------|---------|--------|---------|-----------|---------|
| MainScene.unity | 61 | 45 | 22 | 25 | 14 |
| 自有 Prefab (14 个文件) | 27 | 3 | — | — | — |
| UITestSence.unity | 61 | （低优先，未逐条列出） | | | |
| TestScene.unity | 0 | （低优先，未逐条列出） | | | |

## TMP 字体资产

| 路径 | 名称 | 字表条目(约) | Atlas 模式 | ASCII a-zA-Z |
|------|------|-------------|-----------|--------------|
| `Assets/Arts/Fronts/DeYiHei/SmileySans-Oblique-3 SDF.asset` | SmileySans-Oblique-3 SDF | ~6801 | Dynamic | 已含 |
| `Assets/Arts/Fronts/正格点黑16_1.0.0/ZhengGeDianHei-16.asset` | ZhengGeDianHei-16 | ~6927 | Static | 已含 |
| `Assets/Arts/Fronts/长坂点宋12_1.4.2/ChangBanDianSong-12 SDF.asset` | ChangBanDianSong-12 SDF | ~56 | Dynamic | 未检出/不全 |

> **英文字形初判**：在字表 YAML 中检索 `m_Unicode: 65`–`122` 范围；若 52 个字母均存在则标「已含」，否则标「未检出/不全」。Dynamic 模式运行时可补字。

## MainScene.unity 文本清单

### 主菜单 MainPanel（7 条）

| 层级路径 | 组件 | active | 当前文本(解码) | 静态/运行时覆写 | 是否需翻译 |
|----------|------|--------|---------------|----------------|-----------|
| `Panels/MainPanel/TutorialRun/按钮文字` | TextMeshPro | 1 | 教  学 | 静态标签(标准世界文字实例，脚本未绑定写入) | 是 |
| `UI面板/局内功能菜单BG/功能模块/回到主菜单/text (3)` | TextMeshPro | 1 | 回到主菜单\n | 静态标签(脚本中未发现 FindTmp/路径绑定) | 是 |
| `Panels/MainPanel/副标题文字/标准世界文字` | TextMeshPro | 1 | — 九宫地下城 — | 静态标签(代码路径常量引用「标题文字」) | 是 |
| `Panels/MainPanel/QuitGame/按钮文字/标准世界文字` | TextMeshPro | 1 | 退出游戏 | 静态标签(标准世界文字实例，脚本未绑定写入) | 是 |
| `Panels/MainPanel/StartRun/按钮文字/标准世界文字` | TextMeshPro | 1 | 开始游戏 | 静态标签(标准世界文字实例，脚本未绑定写入) | 是 |
| `Panels/MainPanel/SettingsScreen/按钮文字/标准世界文字` | TextMeshPro | 1 | 设  置 | 静态标签(标准世界文字实例，脚本未绑定写入) | 是 |
| `Panels/MainPanel/版本号文字/标准世界文字` | TextMeshPro | 1 | 开发版 1.0 | 静态标签(标准世界文字实例，脚本未绑定写入) | 是 |

### 局内功能菜单（8 条）

| 层级路径 | 组件 | active | 当前文本(解码) | 静态/运行时覆写 | 是否需翻译 |
|----------|------|--------|---------------|----------------|-----------|
| `UI面板/局内功能菜单BG/存档/读档模块/保存/text (1)` | TextMeshPro | 1 | 加载 | 运行时覆写(RunSaveLoadPanel) | 是 |
| `UI面板/局内功能菜单BG/存档/读档模块/UI槽位/保存条目模板/text (3)` | TextMeshPro | 1 | 2026年7月24日19:39  兵大哥\n | 模板占位(层级/对象名含「模板」) | 是 |
| `UI面板/局内功能菜单BG/功能模块/Light Controller/text (1)` | TextMeshPro | 1 | 按键控制 | 静态标签(脚本中未发现 FindTmp/路径绑定) | 是 |
| `UI面板/局内功能菜单BG/功能模块/退出游戏/text (2)` | TextMeshPro | 1 | 关闭游戏 | 静态标签(脚本中未发现 FindTmp/路径绑定) | 是 |
| `UI面板/局内功能菜单BG/存档/读档模块/加载/text (2)` | TextMeshPro | 1 | 保存\n | 运行时覆写(RunSaveLoadPanel) | 是 |
| `UI面板/局内功能菜单BG/音量模块/SFX音量滑条 (1)/text (1)` | TextMeshPro | 1 | SFX | 静态标签(脚本中未发现 FindTmp/路径绑定) | 待确认(英文) |
| `UI面板/局内功能菜单BG/音量模块/bgm音量滑条/text` | TextMeshPro | 1 | BGM | 静态标签(脚本中未发现 FindTmp/路径绑定) | 待确认(英文) |
| `UI面板/局内功能菜单BG/存档/读档模块/UI槽位/加载条目模板/text (4)` | TextMeshPro | 1 | 2026年7月24日19:39  兵大哥\n | 模板占位(层级/对象名含「模板」) | 是 |

### 右键详述（16 条）

| 层级路径 | 组件 | active | 当前文本(解码) | 静态/运行时覆写 | 是否需翻译 |
|----------|------|--------|---------------|----------------|-----------|
| `UI面板/右键描述/敌方描述专用BG/UI槽位/槽位2/Scroll View/Viewport/Content/详细效果信息1` | TextMeshPro | 0 | 字段介绍： | 运行时覆写(CardInspectOverlayPresenter) | 是 |
| `UI面板/右键描述/常规描述BG/UI槽位/Scroll View/Viewport/Content/详细效果信息1` | TextMeshPro | 0 | 字段介绍： | 运行时覆写(CardInspectOverlayPresenter) | 是 |
| `UI面板/右键描述/常规描述BG/UI槽位/框1 (1)/牌组介绍` | TextMeshPro | 1 | 牌组归属 | 运行时覆写(CardInspectOverlayPresenter) | 是 |
| `UI面板/右键描述/敌方描述专用BG/敌人卡模板占位/__InspectLiveFace/Front/血量数值` | TextMeshPro | 1 | 12 | 模板占位(层级/对象名含「模板」) | 否(数字/符号) |
| `UI面板/右键描述/敌方描述专用BG/敌人卡模板占位/Front/介绍区域/标准世界文字` | TextMeshPro | 1 | 治疗药水治疗药水治疗药水治疗药水 | 模板占位(层级/对象名含「模板」) | 是 |
| `UI面板/右键描述/敌方描述专用BG/UI槽位/框1 (1)/牌组介绍` | TextMeshPro | 1 | 背景介绍 | 运行时覆写(CardInspectOverlayPresenter) | 是 |
| `UI面板/右键描述/敌方描述专用BG/敌人卡模板占位/__InspectLiveFace/Front/攻击数值` | TextMeshPro | 1 | 12 | 模板占位(层级/对象名含「模板」) | 否(数字/符号) |
| `UI面板/右键描述/敌方描述专用BG/UI槽位/框1/背景介绍` | TextMeshPro | 1 | 背景介 | 运行时覆写(CardInspectOverlayPresenter) | 是 |
| `UI面板/右键描述/敌方描述专用BG/敌人卡模板占位/__InspectLiveFace/Front/护甲数值` | TextMeshPro | 1 | 12 | 模板占位(层级/对象名含「模板」) | 否(数字/符号) |
| `UI面板/右键描述/敌方描述专用BG/敌人卡模板占位/__InspectLiveFace/Front/名字` | TextMeshPro | 1 | 浣熊\n | 模板占位(层级/对象名含「模板」) | 是 |
| `UI面板/右键描述/敌方描述专用BG/敌人卡模板占位/__InspectLiveFace/Front/行动计数` | TextMeshPro | 1 | 12 | 模板占位(层级/对象名含「模板」) | 否(数字/符号) |
| `UI面板/右键描述/常规描述BG/UI槽位/背景介绍框/背景介绍` | TextMeshPro | 1 | 背景介绍 | 运行时覆写(CardInspectOverlayPresenter) | 是 |
| `UI面板/右键描述/敌方描述专用BG/敌人卡模板占位/__InspectLiveFace/Front/描述` | TextMeshPro | 1 | 测试环境的数据可能重叠的？ | 模板占位(层级/对象名含「模板」) | 是 |
| `UI面板/右键描述/敌方描述专用BG/敌人卡模板占位/Front/横幅/名字` | TextMeshPro | 1 | 治疗药水 | 模板占位(层级/对象名含「模板」) | 是 |
| `UI面板/右键描述/常规描述BG/道具卡标准模版/Front/横幅/名字` | TextMeshPro | 1 | 治疗药水 | 模板占位(层级/对象名含「模版」) | 是 |
| `UI面板/右键描述/常规描述BG/道具卡标准模版/Front/介绍区域/标准世界文字` | TextMeshPro | 1 | 治疗药水治疗药水治疗药水治疗药水 | 模板占位(层级/对象名含「模版」) | 是 |

### 简要解释（1 条）

| 层级路径 | 组件 | active | 当前文本(解码) | 静态/运行时覆写 | 是否需翻译 |
|----------|------|--------|---------------|----------------|-----------|
| `Panels/简要解释文字框/标准世界文字 (1)/标准世界文字` | TextMeshPro | 1 |  | 运行时覆写(BoardBriefTipPresenter) | 否(空) |

### 作弊工具（17 条）

| 层级路径 | 组件 | active | 当前文本(解码) | 静态/运行时覆写 | 是否需翻译 |
|----------|------|--------|---------------|----------------|-----------|
| `UI面板/作弊工具BG/第一层主面板/添加遗物选项/作弊工具提示 (1)` | TextMeshPro | 1 | 添加遗物 | 运行时覆写(CheatToolPanelController) | 是 |
| `UI面板/作弊工具BG/第二层_log记录面板/InputField (TMP)/Text Area/Text` | TextMeshProUGUI | 1 | 顺劈斧攻击异常_接线冒烟​ | 运行时覆写(CheatToolPanelController) | 是 |
| `UI面板/作弊工具BG/第二层_log记录面板/InputField (TMP)/Text Area/Placeholder` | TextMeshProUGUI | 1 | 描述问题，例如：顺劈斧攻击异常 | 模板占位(层级/对象名含「Placeholder」) | 是 |
| `UI面板/作弊工具BG/第二层_log记录面板/InputField (TMP)` | TextMeshPro | 1 | 顺劈斧攻击异常_接线冒烟 | 运行时覆写(CheatToolPanelController) | 是 |
| `UI面板/作弊工具BG/第二层_添加卡菜单/InputField (TMP)/Text Area/Placeholder` | TextMeshProUGUI | 1 | Enter text... | 模板占位(层级/对象名含「Placeholder」) | 待确认(英文) |
| `UI面板/作弊工具BG/第二层_log记录面板/Button/Text (TMP)` | TextMeshProUGUI | 1 | 记录log | 静态标签(代码路径常量引用「作弊工具BG」) | 是 |
| `UI面板/作弊工具BG/第二层_添加卡菜单/InputField (TMP)/Text Area/Text` | TextMeshProUGUI | 1 | ​ | 运行时覆写(CheatToolPanelController) | 否(符号) |
| `UI面板/作弊工具BG/第二层_log记录面板/notice text` | TextMeshProUGUI | 1 | log 记录成功！\n已保存 3 个文件到：\nC:/Users/jinji/Documents/GitHub/Ninegrid-Gambit/Assets\Notes\Logs\ManualBugSnapshots\!!!AI-BUG-REPORT!!!-20260808-082245_顺劈斧攻击异常_接线冒烟 | 运行时覆写(CheatToolPanelController) | 是 |
| `UI面板/作弊工具BG/第二层_添加卡菜单/InputField (TMP)` | TextMeshPro | 1 |  | 运行时覆写(CheatToolPanelController) | 否(空) |
| `UI面板/作弊工具BG/第一层主面板/一键跨层选项/作弊工具提示 (1)/标准世界文字` | TextMeshPro | 1 | 前往下一层 | 静态标签(场景序列化标签（作弊选项名）) | 是 |
| `UI面板/作弊工具BG/第一层主面板/回复满血选项/作弊工具提示 (1)/标准世界文字` | TextMeshPro | 1 | 回复满血 | 静态标签(场景序列化标签（作弊选项名）) | 是 |
| `UI面板/作弊工具BG/第一层主面板/战斗加卡选项/作弊工具提示 (1)/标准世界文字` | TextMeshPro | 1 | 战斗加卡 | 静态标签(场景序列化标签（作弊选项名）) | 是 |
| `UI面板/作弊工具BG/标题/标准世界文字` | TextMeshPro | 1 | 作弊面板 | 静态标签(场景序列化标签（作弊选项名）) | 是 |
| `UI面板/作弊工具BG/第一层主面板/记录log选项/作弊工具提示 (1)/标准世界文字` | TextMeshPro | 1 | 记录log | 静态标签(场景序列化标签（作弊选项名）) | 是 |
| `UI面板/作弊工具BG/第一层主面板/无限金币选项/作弊工具提示 (1)/标准世界文字` | TextMeshPro | 1 | 无限金币 | 静态标签(场景序列化标签（作弊选项名）) | 是 |
| `UI面板/作弊工具BG/第一层主面板/一键清关选项/作弊工具提示 (1)/标准世界文字` | TextMeshPro | 1 | 一键清关 | 静态标签(场景序列化标签（作弊选项名）) | 是 |
| `UI面板/作弊工具BG/第一层主面板/无敌模式选项 /作弊工具提示 (1)/标准世界文字` | TextMeshPro | 1 | 无敌模式 | 静态标签(场景序列化标签（作弊选项名）) | 是 |

### HUD（3 条）

| 层级路径 | 组件 | active | 当前文本(解码) | 静态/运行时覆写 | 是否需翻译 |
|----------|------|--------|---------------|----------------|-----------|
| `玩家信息/血条/血量数值（满血）` | TextMeshPro | 1 | 10 | 运行时覆写(PlayerInfoHudPresenter) | 否(数字/符号) |
| `玩家信息/金币/金币数值` | TextMeshPro | 1 | 0 | 运行时覆写(PlayerInfoHudPresenter) | 否(数字/符号) |
| `玩家信息/血条/血量数值（当前）` | TextMeshPro | 1 | 10 | 运行时覆写(PlayerInfoHudPresenter) | 否(数字/符号) |

### 其他（9 条）

| 层级路径 | 组件 | active | 当前文本(解码) | 静态/运行时覆写 | 是否需翻译 |
|----------|------|--------|---------------|----------------|-----------|
| `玩家信息/基础护甲/数值` | TextMeshPro | 1 | 1 | 运行时覆写(PlayerInfoHudPresenter) | 否(数字/符号) |
| `<GO:0>` | TextMeshPro | 1 |  | 静态标签(脚本中未发现 FindTmp/路径绑定) | 否(空) |
| `UI面板/战斗信息展示BG/玩家/房间信息 (1)/标准世界文字` | TextMeshPro | 1 | 玩家： | 运行时覆写(BattleInfoPreviewPresenter) | 是 |
| `UI面板/战斗信息展示BG/环境/房间信息 (1)/标准世界文字` | TextMeshPro | 1 | 环境： | 运行时覆写(BattleInfoPreviewPresenter) | 是 |
| `楼层提示/小房间提示/标准世界文字` | TextMeshPro | 1 | 常规战斗房间 | 运行时覆写(FloorHintPresenter) | 是 |
| `Anchors/CardHandAnchors/CardRecycleNotice/标准世界文字 (2)/标准世界文字` | TextMeshPro | 1 | +10 | 静态标签(标准世界文字实例，脚本未绑定写入) | 否(数字/符号) |
| `楼层提示/大楼层提示/标准世界文字` | TextMeshPro | 1 | 楼层·Ⅱ\n | 运行时覆写(FloorHintPresenter) | 是 |
| `UI面板/战斗信息展示BG/房间信息/标准世界文字` | TextMeshPro | 1 | 房间类型：\n楼层：\n进度： | 运行时覆写(BattleInfoPreviewPresenter) | 是 |
| `UI面板/战斗信息展示BG/怪物/房间信息 (1)/标准世界文字` | TextMeshPro | 1 | 怪物： | 运行时覆写(BattleInfoPreviewPresenter) | 是 |

## 自有预制体文本清单

### `Assets/Prefabs/StandardUIButton.prefab`（1 条）

| 层级路径 | 组件 | active | 当前文本(解码) | 静态/运行时覆写 | 是否需翻译 |
|----------|------|--------|---------------|----------------|-----------|
| `StandardUIButton/Text (TMP)` | TextMeshProUGUI | 1 | PASS | 待确认(预制体文本，未在脚本中找到明确引用) | 待确认(英文) |

### `Assets/Prefabs/VFX/DamageNumberPopup.prefab`（1 条）

| 层级路径 | 组件 | active | 当前文本(解码) | 静态/运行时覆写 | 是否需翻译 |
|----------|------|--------|---------------|----------------|-----------|
| `DamageNumberPopup/TMP` | Text | 0 | 1 | 待确认(预制体文本，未在脚本中找到明确引用) | 否(数字/符号) |

### `Assets/Prefabs/攻击力.prefab`（1 条）

| 层级路径 | 组件 | active | 当前文本(解码) | 静态/运行时覆写 | 是否需翻译 |
|----------|------|--------|---------------|----------------|-----------|
| `攻击力/F_UI_Gem_EmptySlot3/攻击数值` | TextMeshPro | 1 | 12 | 运行时覆写(CardFaceSlotNodeMap) | 否(数字/符号) |

### `Assets/Prefabs/攻击数值.prefab`（1 条）

| 层级路径 | 组件 | active | 当前文本(解码) | 静态/运行时覆写 | 是否需翻译 |
|----------|------|--------|---------------|----------------|-----------|
| `攻击数值` | TextMeshPro | 1 | 12 | 运行时覆写(CardFaceSlotNodeMap) | 否(数字/符号) |

### `Assets/Prefabs/标准世界文字.prefab`（1 条）

| 层级路径 | 组件 | active | 当前文本(解码) | 静态/运行时覆写 | 是否需翻译 |
|----------|------|--------|---------------|----------------|-----------|
| `标准世界文字` | TextMeshPro | 1 | 测试测试测试测试测试测试测试测试测试测试测试测试测试测试测试 | 静态标签(标准世界文字实例，脚本未绑定写入) | 是 |

### `Assets/Prefabs/标准遗物图标模板.prefab`（1 条）

| 层级路径 | 组件 | active | 当前文本(解码) | 静态/运行时覆写 | 是否需翻译 |
|----------|------|--------|---------------|----------------|-----------|
| `标准遗物图标模板/计数` | TextMeshPro | 1 | 3 | 模板占位(层级/对象名含「模板」) | 否(数字/符号) |

### `Assets/Prefabs/血条.prefab`（1 条）

| 层级路径 | 组件 | active | 当前文本(解码) | 静态/运行时覆写 | 是否需翻译 |
|----------|------|--------|---------------|----------------|-----------|
| `血条/攻击数值 (1)` | TextMeshPro | 1 | 12 | 运行时覆写(CardFaceSlotNodeMap) | 否(数字/符号) |

### `Assets/Resources/Prefabs/UI/词条详细效果信息.prefab`（1 条）

| 层级路径 | 组件 | active | 当前文本(解码) | 静态/运行时覆写 | 是否需翻译 |
|----------|------|--------|---------------|----------------|-----------|
| `词条详细效果信息` | TextMeshPro | 1 | 字段介绍： | 待确认(预制体文本，未在脚本中找到明确引用) | 是 |

### `Assets/Resources/Prefabs/怪物卡标准模板.prefab`（6 条）

| 层级路径 | 组件 | active | 当前文本(解码) | 静态/运行时覆写 | 是否需翻译 |
|----------|------|--------|---------------|----------------|-----------|
| `怪物卡标准模板/Front/行动计数/行动计数数值` | TextMeshPro | 1 | 5 | 模板占位(层级/对象名含「模板」) | 否(数字/符号) |
| `怪物卡标准模板/Front/描述` | TextMeshPro | 1 |  | 模板占位(层级/对象名含「模板」) | 否(空) |
| `怪物卡标准模板/Front/血量/血量数值` | TextMeshPro | 1 | 12 | 模板占位(层级/对象名含「模板」) | 否(数字/符号) |
| `怪物卡标准模板/Front/名字` | TextMeshPro | 1 |  | 模板占位(层级/对象名含「模板」) | 否(空) |
| `怪物卡标准模板/Front/护甲/护甲数值` | TextMeshPro | 1 | 12 | 模板占位(层级/对象名含「模板」) | 否(数字/符号) |
| `怪物卡标准模板/Front/攻击/攻击数值` | TextMeshPro | 1 | 12 | 模板占位(层级/对象名含「模板」) | 否(数字/符号) |

### `Assets/Resources/Prefabs/房间选项标准模板.prefab`（2 条）

| 层级路径 | 组件 | active | 当前文本(解码) | 静态/运行时覆写 | 是否需翻译 |
|----------|------|--------|---------------|----------------|-----------|
| `房间选项标准模板/Front/横幅/名字` | TextMeshPro | 1 |  | 模板占位(层级/对象名含「模板」) | 否(空) |
| `房间选项标准模板/Front/介绍区域/标准世界文字` | TextMeshPro | 1 |  | 模板占位(层级/对象名含「模板」) | 否(空) |

### `Assets/Resources/Prefabs/机关卡标准模版.prefab`（4 条）

| 层级路径 | 组件 | active | 当前文本(解码) | 静态/运行时覆写 | 是否需翻译 |
|----------|------|--------|---------------|----------------|-----------|
| `机关卡标准模版/Front/行动计数/行动计数数值` | TextMeshPro | 1 | 2 | 模板占位(层级/对象名含「模版」) | 否(数字/符号) |
| `机关卡标准模版/Front/横幅/名字` | TextMeshPro | 1 |  | 模板占位(层级/对象名含「模版」) | 否(空) |
| `机关卡标准模版/Front/介绍区域/标准世界文字` | TextMeshPro | 1 |  | 模板占位(层级/对象名含「模版」) | 否(空) |
| `机关卡标准模版/Front/血量/血量数值` | TextMeshPro | 1 | 2 | 模板占位(层级/对象名含「模版」) | 否(数字/符号) |

### `Assets/Resources/Prefabs/玩家卡标准模板.prefab`（3 条）

| 层级路径 | 组件 | active | 当前文本(解码) | 静态/运行时覆写 | 是否需翻译 |
|----------|------|--------|---------------|----------------|-----------|
| `玩家卡标准模板/front/护甲数值` | TextMeshPro | 1 | 8 | 模板占位(层级/对象名含「模板」) | 否(数字/符号) |
| `玩家卡标准模板/front/攻击数值` | TextMeshPro | 1 | 12 | 模板占位(层级/对象名含「模板」) | 否(数字/符号) |
| `玩家卡标准模板/front/名字` | TextMeshPro | 1 | 兵大哥 | 模板占位(层级/对象名含「模板」) | 是 |

### `Assets/Resources/Prefabs/道具卡标准模版.prefab`（2 条）

| 层级路径 | 组件 | active | 当前文本(解码) | 静态/运行时覆写 | 是否需翻译 |
|----------|------|--------|---------------|----------------|-----------|
| `道具卡标准模版/Front/横幅/名字` | TextMeshPro | 1 |  | 模板占位(层级/对象名含「模版」) | 否(空) |
| `道具卡标准模版/Front/介绍区域/标准世界文字` | TextMeshPro | 1 |  | 模板占位(层级/对象名含「模版」) | 否(空) |

### `Assets/Resources/Prefabs/遗物卡标准模版.prefab`（2 条）

| 层级路径 | 组件 | active | 当前文本(解码) | 静态/运行时覆写 | 是否需翻译 |
|----------|------|--------|---------------|----------------|-----------|
| `遗物卡标准模版/GameObject/名字` | TextMeshPro | 1 |  | 模板占位(层级/对象名含「模版」) | 否(空) |
| `遗物卡标准模版/GameObject/描述面板/标准世界文字` | TextMeshPro | 1 |  | 模板占位(层级/对象名含「模版」) | 否(空) |

## 排除范围说明

- 已排除：`Assets/Plugins/**`、`Assets/QFramework/**`、`Assets/Samples/**`、`Assets/Arts/**`（美术演示）
- `Assets/Arts/Images/PixelartCardTCG/Demo/Samples/` 下有演示卡预制体含英文样例文本，**非生产引用**，未纳入上表。

## 纯运行时文本面板（场景内无序列化 m_text）

下列面板的主要文案**不在** `.unity` YAML 中，由 C# 在运行时写入（本次场景扫描无法逐条列出，需另做代码/内容串表盘点）：

| 面板 | 场景节点 | 写入类 | 说明 |
|------|---------|--------|------|
| 人物选择 | `UI面板/人物选择BG` | `CharacterSelectPanel` | 提示文字、属性文字、角色名（`displayName`） |
| 结算面板 | `UI面板/结算面板BG` | `RunSummaryPanel` | 标题/副标题/进度/金币/属性/遗物/种子等 |
| 存档槽位正文 | 加载/保存条目模板 | `RunSaveLoadPanel` | 克隆模板后写入日期+角色名 |
| 卡面/词条正文 | 各 `*标准模板` 预制体 | `CardFacePresentationBinder` 等 | 名称、描述、数值 |
| 楼层/回收提示 | `楼层提示/*`、`CardRecycleNotice` | `FloorHintPresenter`、`CardHandManagerSingleton` | 如 `楼层·Ⅱ`、`+10` 金币 |

## 脚本用法

```bash
python "Assets/Notes/Localization/tools/scan_unity_texts.py" \
  --project "C:/Users/jinji/Documents/GitHub/Ninegrid Gambit" \
  --report
```

参数：
- `--file <path>`：扫描单个 .unity/.prefab，输出 TSV 到 stdout
- `--project <root>`：项目根目录
- `--report`：全量扫描并写入 `Assets/Notes/Localization/inventory-scene-texts.md`
- `--tsv <path>`：将全量结果写入 TSV 文件
