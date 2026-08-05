---
status: accepted
---

# 内容单一权威：一卡一文件 JSON + Resources 加载

## 决策

内容数据收敛为**单一权威**，并明确资源加载后端：

1. **一卡一文件 JSON 是内容唯一权威。** 一张卡的身份、卡面表现引用、数值、效果装配、分类全部落在同一个文件里，不再有「某字段在 A 文件、被 B 文件覆盖」。
2. **废除 Luban。** 12 张表、`Generated/Luban/` 生成层、`Assets/Tools/Luban/` 工具链（`gen_table_nine.ps1`、xlsx、python IO 脚本）、`Luban.Runtime` 包依赖一并退休。
3. **内容侧 ScriptableObject 退休。** 6 个 `*VisualCatalogSO` 与 `ContentVisualSpriteCatalogBootstrapSO` 不再承担内容权威。表现层非内容用途的 SO（`CardFaceSlotRegistrySO`、`CardFaceDescriptionIconCatalogSO`、特效参数 SO 等）不在此列。
4. **卡牌用到的美术资源迁入 `Assets/Resources/` 下的单一根目录**，JSON 中的资产路径字符串因此在 Player 中可解析。
5. **`TableNineContentCatalog.cs`（1621 行硬编码镜像）降级为小型测试夹具**，不再镜像全量内容。
6. **`CardPresentationBusinessOverlay` 删除。** 表现文件不再覆盖 `stats` / `gold` / `displayName`。

## 为什么

改版前内容有三份需手工同步的权威源：`Assets/Tools/Luban/Datas/*.json`（150 条效果 + 12 张表）、`TableNineContentCatalog.cs`（同样 150 条效果的完整 C# 镜像，EditMode 测试用它）、`Assets/Arts/ContentVisual/cards/*.json`（180 个文件，且经 Overlay **覆盖**前两者的 `stats`/`gold`）。表现文件在当数值权威，与「数值不应绑定在表现层」的设计意图相反。

选 JSON 而非 ScriptableObject 的三条硬约束：

- **`NineGrid.Core` 是 `noEngineReferences: true`。** SO 属 `UnityEngine`，永远不可能成为规则核的内容类型；无论选什么格式都必须有一层「源 → `GameContentCatalog`」投影，「SO 更原生」在 Core 边界前不成立。
- **`.gitattributes` 把 `*.asset` 配成 `filter=lfs diff=lfs merge=lfs -text`，仓库内 322 个 `.asset` 全是 LFS 指针。** SO 没有文本 diff、没有 merge。本仓库的主要协作方式（人 + AI 读 diff 做 review）在 SO 上不成立。
- **效果 DSL 是 JSON 字符串。** 搬进 SO 若只是 `[TextArea] string`，编辑器收益为零；要拿到 Inspector 收益需从零建 `[SerializeReference]` 多态节点编辑器（仓库内业务代码 `[SerializeReference]` 先例为 0）。

选 Resources 而非 Addressables 的依据是规模：`Assets/Arts/Images/` 全量 7737 个文件 / 106 MB，但 180 个内容 JSON **实际引用只有 353 个文件 / 0.85 MB**，唯一引用键 107 个（45 个 sprite + 61 个动画文件夹 + 1 个图集）。项目无 CI、无服务器、无热更/补丁业务代码、`SpriteAtlas` 资产数为 0（不存在 Resources 与图集重复打包）、项目自有 Resources 目录只有 `Assets/Resources` 一处。Resources 那些「不可裁剪、全量入包、启动建索引」的告诫是按几十上百 MB 校准的，在 0.85 MB 上不构成约束。

拆 Luban 的耦合面很窄：`cfg.tablenine.*` 生成类型在 `Generated/` 之外**只出现在 3 个适配器**（`TableNineLubanCatalogFactory`、`TableNineVisualCatalogFactory`、`TableNineCardFrameStyleCatalogFactory`），业务代码一律消费 Core 的纯 C# `GameContentCatalog`。xlsx 那两张表由 python 脚本自动 bootstrap，非人工填写。

## 考虑过的替代

- **全 ScriptableObject**：否决——见上三条硬约束。若要成立须同时改 `.gitattributes` 脱 LFS 并从零建 DSL 节点编辑器，投入远大于问题规模。
- **引入 Addressables**：否决——为 GB 级资源 + 远程 CDN + 增量补丁设计，用于 107 个键 / 0.85 MB 不回本；且会把同步的卡面 Commit 路径拖成异步，逼近 ADR-0005 的时序契约。
- **Addressables + QFramework ResKit 假 AB 组合**：否决——两者是同一层的竞争机制（都做「Editor 直读、发布走真包」），叠加等于双 Init、双引用计数、双构建流水线。ResKit 在本仓库是 66 个 `.cs` / 7835 行的死代码，业务调用数为 0，无存量收益可继承。
- **保留 Luban 只做权威收敛**：否决——收敛后 Luban 只剩「schema + 生成 3 个 Factory 用的 Row 类」，净成本大于收益。
- **保持路径字符串但构建期烘一张索引 SO**：否决——那张索引 SO 就是被否决的 SO 方案的最小形态，且引入一个人不可见的生成物。

## 后果

- **必须新建断链校验器。** 路径字符串没有 GUID 保护，改名挪目录 Unity 不会重定向。编辑器里扫全部内容 JSON 的资产路径、报告解析不到的项——这是本决策的强制代价对冲，不是可选项。
- **`_index.json` 是磁盘快照，导出必须扫盘（#140）。** 索引不得由编辑器会话状态（xlsx 行 + 内存条目）合成——新增技能/遗物/房间选项曾因漏抄而漂移。导出入口（`CardPresentationEditorSession.TryExportIndex` / `CardPresentationIndexIO`）一律以 Authoring 目录的全部 DTO contentId 为准；`ContentHygieneValidator` 守护索引↔磁盘双向一致、Authoring↔Streaming 字节镜像、skillIds→技能 JSON、装配→效果模板、模板 body defId、空壳技能与归档可达性（编辑保存后再漂移即被 EditMode 契约拦下）。
- **`CardAnimFrameSource` 必须补非 Editor 分支。** 现在 folder / atlas 两个分支整段在 `#if UNITY_EDITOR` 内、`#else` 返回空。迁入 Resources 不会自动修好，需改为 `Resources.LoadAll<Sprite>` + 按名排序保证帧序确定。
- **21 个「SO 有、内容 JSON 无」的 contentId 要先补文件**（`deck.*`、房间、Fountain/Shop、属性三选一等），否则退休 SO 会丢内容。
- **约定单一 Resources 根**，禁止散建多个 Resources 目录（命名空间会合并，同名冲突）。
- EditMode 测试当前 33 处调用 `TableNineContentCatalog.CreateDefault()`，降级为夹具后需相应改写。
- 外部卡牌编辑器 `Locus/.../ViewApi.cs` 直读 `StreamingAssets/.../tablenine_tb*.json`，须改数据源。
- 迁移美术文件时 Unity 保留 GUID 与 meta，场景/预制体上的直接引用不会断；只有内容 JSON 里的路径字符串需批量重写。

## 相关

- [ADR-0009](0009-parameterized-effect-templates.md) — 效果参数化模板与跨容器装配（本决策承载其存储形态）
- [ADR-0005](0005-card-face-beat-commit.md) — 卡面数值锚点提交；本决策保持视觉 Commit 同步，不引入异步加载
- `CONTEXT.md` — 卡牌表现实体、卡牌内容装配
