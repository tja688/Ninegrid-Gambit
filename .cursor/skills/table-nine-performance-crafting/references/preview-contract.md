# 预览契约 — PreviewChannel vs RuntimeChannel

每个 `*Performance` 须实现**双通道**，使表演器能脱离导演/适配器独立调参和演示。

## 双通道定义

```text
RuntimeChannel  — 外部调用 Play(params)；等待导演/适配器传入演员与参数
PreviewChannel  — ContextMenu / Inspector 按钮 / previewMode 开关
                  表演器自行 EnsurePreviewActors()，用最小模板占位
```

### RuntimeChannel

- 入口：`Play(IReadOnlyList<Transform> actors, …)` 或 `Play(Vector2 direction, …)` 等
- **禁止**要求外部 Director/Adaptor 先查询「当前有哪些卡 uid」才能播放
- **允许**关联场景内锚点 Transform、材质模板、音效引用（静态引用，非运行时业务状态）

### PreviewChannel

- 入口：`PlayPreview()` / `[ContextMenu("Play Preview")]` / Inspector 自定义按钮
- 内部调用 `EnsurePreviewActors()` 生成或复用占位演员
- 播放结束或 `StopAndRestore` 时调用 `TeardownPreviewActors()`（若本次预览有生成物）
- 预览生成物**不得泄漏**到 RuntimeChannel（不污染正式演员列表）

## 最小预览素材

每个 Performance 暴露**本表演所需的最小**预览模板字段，例如：

```csharp
[Header("Preview")]
[SerializeField] private GameObject cardPreviewPrefab;  // 可为 null
[SerializeField] private Material spriteStub;
[SerializeField] private bool previewInteractable;
[SerializeField] private int previewActorCount = 3;
```

**Fallback 规则：** 模板为 null 时，允许工程内 fallback（如 `Shader.Find`、纯色 Quad、内置 Sprite），参考 [`CardHitFlashPerformance.cs`](../../../Assets/Scripts/NineGrid.Presentation/Performance/CardHitFlashPerformance.cs)。

**上限：** 预览只需「能看懂运动与参数」，不要求复刻正式卡牌美术。避免每个 Performance 膨胀数百行 spawn 逻辑。

Assets/Prefabs/Standard Card.prefab 有最小卡牌模板素材，如果用mcp挂载到场景预览时对卡牌元素有诉求，可以直接关联，但是不在代码中写死查找这个对象，只做场景内的mcp关联可选项

## 交互预览

需要状态交互的表演（点选方向、拖拽起点终点等）：

- `[SerializeField] bool previewInteractable` — **仅 PreviewChannel 生效**
- 勾选：响应鼠标/触摸，便于人调参
- 不勾选（正式运行时）：关闭输入，等待外部 `Play(params)`

## 两种「预览」不要混淆

| 类型 | 目的 | 做法 |
|------|------|------|
| **开发预览**（PreviewChannel） | 自给自足调参、Inspector 点播放 | `EnsurePreviewActors` + `PlayPreview` |
| **验收对照**（对基准视觉） | 证明忠实复刻了人手搓/参考源 | 与 Route A/B/C 原始源并排比对；见 [`verification.md`](verification.md) |

开发预览**不能替代**验收对照。Route C 尤须人标注验收标准。

## EditMode vs PlayMode

- **PlayMode** — DOTween / Coroutine 忠实动效预览的主战场
- **EditMode** — 仅适合参数校验、静态布局、`OnValidate` 约束；**不要**声称 EditMode 可完整验收 DOTween 手感

## 工程现状参考

| 脚本 | 播放安全 | 预览自给 |
|------|----------|----------|
| `CardDeckEntryPerformance` | 已达标（`deferPlayOneFrame` + Pause/Restart） | 部分 — 依赖 `previewCards` 场景预摆，待 `EnsurePreviewActors` 迭代 |
| `CardHitFlashPerformance` | N/A（Coroutine） | 部分 — 可对 `target` 自身播放；无多演员 spawn |

新 Performance 应以本契约为准；旧脚本逐步对齐。

## 允许 vs 禁止依赖

| 允许关联 | 禁止依赖 |
|----------|----------|
| 锚点 Transform、slotRoot | 外部 Director 提供 uid 列表才能播 |
| 材质/音效/Prefab 模板 | Adaptor 先跑一遍内核才能预览 |
| 可选：预览专用父节点 | 联动的其他 MonoBehaviour 必须存在才能呈现核心运动 |
