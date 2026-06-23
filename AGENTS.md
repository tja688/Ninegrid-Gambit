# agents.md

## 项目介绍

本项目是一个 `Unity 6.3 LTS` urp 管线开发的 2D 卡牌像素风 Roguelike 游戏，名为 **TableNine**。核心玩法为九宫格棋盘驱动的卡牌战斗。

## 技术框架

- 项目框架采用 `QFramework`。
- 项目本地代码入口在：`Assets/Scripts`。

## 设计文档

- 游戏设计案统一存放在 `Assets/Docs` 目录下，当某些效果实现、游戏设计细节需要理解时可以进行查阅、参考。
- 游戏开发进行中过程性笔记：`Assets/Notes`，根据自己需求选择性阅读。如果开发过程中用户要求落地笔记、汇报等需求，统一落地在此目录。
- 游戏开发架构设计文档见：Assets/Notes/九宫牌局权威顶层架构设计.md ，想要总览游戏架构组织设计理念可以阅读此文档。
- 表现层四盒子与 AI 协作 Skill：总览见 `Assets/Notes/表现层方法论.md`；表演器制作 `.cursor/skills/table-nine-performance-crafting`（盒子③）、交互 FSM + 本地反馈 `.cursor/skills/table-nine-interaction-crafting`（泳道 B）、适配器 `.cursor/skills/table-nine-adapter-crafting`（盒子④）。
- 对于表现层的开发，`C:\Users\jinji\Desktop\文档\MyNote\游戏开发项目\引擎工作区\九宫牌局架构\九宫牌局表现层.canvas` 有权威的架构设计，对于全局理解可以查阅，注意上下文较大，有必要的时候才进行理解读取。
- **表现层蓝图落地对照**：`Assets/Notes/表现层蓝图落地对照表.md`（与蓝图中黄色「★已落地」注释节点同步）。

### 表现层蓝图落地工作流

表现层实现必须以蓝图为锚，保证「规划 ↔ 代码」可追溯：

1. **开工前查蓝图**：在 `九宫牌局表现层.canvas` 中定位对应节点（泳道 A/B/C、脊柱、适配器、表演黑盒）；可同时查阅 `表现层蓝图落地对照表.md`。
2. **蓝图无对应项**：若用户要求的落地在蓝图中找不到明确节点，**必须先与用户确认**——是描述/命名未对齐，还是设计需要改动；确认后再实现。
3. **完工后双向同步**：
   - 在蓝图中新增或更新 **黄色注释节点**（图例 `leg_ann`：`color: "3"`，文本以 `★已落地` 开头），用 **连线指向** 对应规划节点；精炼列出类名/路径与剩余缺口。
   - 更新 `Assets/Notes/表现层蓝图落地对照表.md` 对应行。
   - 在蓝图 `updatelog` 节点追加版本摘要。
4. **粒度约定**：一项落地 = 一条注释（或一组强耦合项合并为一条）；避免回到早期 `c_ann_landed` 式大段堆砌。表演黑盒与适配器分开标注缺口。

## 项目规则

- 开发本项目前，请优先阅读 `rules.md`，其中包含架构规范以及文件保护规则等全局性约束。
- 当前处于原型开发期，唯一游戏场景：`Assets/Scenes/MainScene.unity`
- 项目有 Unity MCP，在落地实现时注意了解功能并辅助使用，如果发现无法使用再回退文件操作形式开发。
- 请在完成代码落地后主动触发unity mcp 的unity 刷新功能并阅读Console，这可以暴露编译代码的错误并帮助你进行修复。
- 项目已经初始化 codegraph，可以使用codegraph macp来进行代码查询，使用提醒：它擅长“符号/类/方法/调用关系/影响面”，不擅长一次性查询混合了批次号、pending ID、设计文档、测试名、Luban 数据的宽泛任务。查询不理想时，先把问题拆成明确符号或代码区域，例如 `UseItemAction`、`EffectAtomLibrary`、`TableNineContentCatalog`、`P6ContentLandingTests`；查具体 pending ID、中文设计文本、JSON 行、计划笔记时优先用 `rg` 精确检索。代码刚改完且 codegraph 可能未同步时，以文件读取和 Unity 编译/测试结果为准。

## 协作需求

- 每次完成任务后，将本次改动**全量提交**至 git，提交信息应简洁、准确地概括改动内容与目的。
