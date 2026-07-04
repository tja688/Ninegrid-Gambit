# 场景元素选择高亮

鼠标悬停场景精灵时的选择反馈。支持静态图与 **Animator 换帧** 角色。

## 组件

| 组件 | 作用 | 挂哪 |
| --- | --- | --- |
| `SelectableSceneElement` | 可点选目标 + 高亮表现 | 每个可选精灵（需 `SpriteRenderer`） |
| `SceneElementPointerSelector` | 每帧射线/点选，驱动悬停 | 场景里 **一个** 即可（如 `GameFlow`） |

命名空间：`NineGrid.Presentation.Visuals`（程序集 `PixelVisuals`）。

## 复用步骤（新对象）

1. 目标物体已有 `SpriteRenderer`（动画角色再挂 `Animator` 即可，描边会跟帧）。
2. Add `SelectableSceneElement`。
3. 设 `elementId`（逻辑 id；空则用 GameObject 名）。
4. 选 `visualMode`（见下）。
5. **Outline** 时指定 `outlineMaterial` → `Assets/Arts/VisualProfiles/TableNineSpriteSilhouette.mat`。
6. 确认场景里已有 `SceneElementPointerSelector`（可指定 `targetCamera`，默认 `Camera.main`）。

叠在一起时取 **sortingOrder 更高** 的；同 order 取 sibling 更靠后的。

## visualMode

| 模式 | 效果 | 适用 |
| --- | --- | --- |
| `Flash` | 本体着色/HitFlash | 静态建筑、已用 HitFlash 材质 |
| `Outline` | 8 向剪影描边，跟动画帧 | **船/角色等动画精灵（推荐）** |
| `FlashAndOutline` | 两者叠加 | 需要内闪 + 外框 |

- **Flash** 要材质带 `_HitFlashAmount` / `_HitFlashColor`（`TableNineSpriteHitFlash`）才是 shader 闪白；否则退化为 `color` 插值。
- **Outline** 不改本体材质；运行时在子节点 `__SelectionOutline` 下生成偏移剪影（`HideFlags.DontSave`，不进场景序列化）。

## Outline 推荐参数（可直接抄）

玩家船当前配置，角色类可复用：

| 字段 | 建议 |
| --- | --- |
| `outlineColor` | `(1, 0.86, 0.32, 1)` 暖金 |
| `outlineWidthPixels` | `1`～`2`（像素单位，跟 scale 一起放大） |
| `pulseOutline` | `true` |
| `pulseSpeed` | `2.6` |
| `pulseMinAlpha` / `pulseMaxAlpha` | `0.72` / `1` |
| `outlineMaterial` | `TableNineSpriteSilhouette.mat` |

## 资源

- Shader：`TableNine/SpriteSilhouette`（`Assets/Arts/VisualProfiles/TableNineSpriteSilhouette.shader`）— 只取 alpha 铺纯色。
- 材质：`Assets/Arts/VisualProfiles/TableNineSpriteSilhouette.mat`
- Flash 材质（可选）：`Assets/Arts/VisualProfiles/TableNineSpriteHitFlash.mat`

未指定 `outlineMaterial` 时会 `Shader.Find` 建运行时材质；**批量复用请在 Inspector 拖好共享材质**，避免每实例一份。

## 代码侧

```csharp
// 只读当前悬停
var selector = FindAnyObjectByType<SceneElementPointerSelector>();
SelectableSceneElement hovered = selector != null ? selector.Hovered : null;

// 手动开关高亮（一般不用，指针选择器会调）
element.SetHovered(true);

// 逻辑 id
string id = element.ElementId;
```

注册/反注册在 `OnEnable` / `OnDisable`：物体被 `SetActive(false)` 会自动退出悬停列表。

## 注意

- 命中靠 `Collider2D`（默认 `ensurePhysicsCollider` 会按 sprite physics shape 建 Polygon/Box，**trigger**）。动画换帧 **不会** 更新 collider 形状；帧差异大时请手调 Box。
- 指针在 UI 上时默认忽略（`ignoreWhenPointerOverUi`）。
- Outline 每宽 1px × 8 向 = 8 个额外 `SpriteRenderer`；单场景少量可选物没问题，大量单位慎用宽 3。
- 已有示例：`MainScene/player`（Outline）、`IslandScene` 厂房/船坞（Flash）。
