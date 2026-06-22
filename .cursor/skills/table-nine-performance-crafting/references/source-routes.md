# 来源路由

识别基准视觉属于哪条路由，再读对应细则。通用 5 步工作流见主 `SKILL.md`。

## 路由总表

| 路由 | 基准视觉 | AI 怎么读 | 典型产出 |
|------|----------|-----------|----------|
| **A. DOTween Timeline + Animation** | 场景内手搓 Timeline/Animation | 逐 clip 读 delay/duration/ease/callback | 纯 `DOTween.Sequence()` 重建 |
| **B. 现有脚本复刻** | 项目内 MonoBehaviour / Coroutine / Animator 片段 | 读源码 + 运行时对照；提取可参数化层 | 合并进 Performance 或 DOTween 等价 |
| **C. 外部参考视觉** | 录屏、截图序列、别引擎、Web/CSS、设计稿 | 人提供对照物；逐参数逼近并让人验收 | 参数暴露齐全，标注「非逐帧无损」 |
| **D. 预留** | — | 新来源只加本表条目，不改主流程 | — |

---

## Route A — DOTween Timeline + Animation

完整技术细则见 [`dotween-timeline-route.md`](dotween-timeline-route.md)。

**Intake 清单：**

- 基准物体/场景路径
- 基准方向或变体（哪一份是 canonical slice）
- 需推广的方向/变体列表（或「任意角度」）
- 每个 callback clip 触发什么（音效、受击、开关物体）
- 写死坐标对应哪个锚点

**产出要点：** Timeline 退化为 authoring/预览工具；运行时抛弃 Timeline 组件，纯代码等价 Sequence。

---

## Route B — 现有脚本复刻

**适用：** 项目里已有 MonoBehaviour、Coroutine、`Animator` 状态片段、或散落在多个脚本里的 DOTween 调用。

**Intake 清单：**

- 源脚本路径与入口方法
- 哪些是手感层（保留数值）、哪些是空间层（参数化）
- 是否与其他系统耦合（若有，只复刻视觉层进 Performance）
- 对照方式：PlayMode 并排预览 / 录屏比对

**工作步骤：**

1. 读源码，列出时序、缓动、目标对象、外部依赖
2. 识别可合并层：多个脚本的 DOTween 应合并进**单一** Performance，不复制 Adapter 调度
3. 提取 `[SerializeField]` 暴露手感参数
4. 空间位移/方向改为向量/角度参数（同 Route A 路由原则）
5. 若源用 Coroutine 而非 DOTween，评估是否直译 Coroutine 或转为等价 Sequence（优先 Sequence 以统一 Kill/restore）

**边界：** Route B 不是写 Adapter。脚本里若混有内核逻辑，只剥离视觉进 Performance。

---

## Route C — 外部参考视觉

**适用：** 录屏、截图序列、其他引擎导出、Web/CSS 动效、Figma/设计稿、任何**人眼可验证**但不在 Unity 场景内的视觉。

**Intake 清单（必填）：**

- 对照物路径（视频/图/GIF/链接）
- 基准方向/变体
- 验收帧或时间点（「第 0.3s 应到达哪里」）
- 可接受的误差范围（像素级 / 手感级 / 大致轮廓）
- 是否需要交互预览（点选方向、拖拽等）
- 验收标准由人书面确认 — **禁止 AI 自行定义「差不多就行」**

**工作步骤：**

1. 人标注关键帧与运动特征（位移曲线、punch、停留、层级）
2. AI 用 DOTween/Coroutine **逼近**，所有可调参数 `[SerializeField]` 暴露
3. 交付时明确标注 **「非逐帧无损」**，与 Route A 的 clip 级对照区分开
4. 人 PlayMode 验收；不通过则回到参数读取，不「凭感觉调一版」

**反模式：** 没有对照物就凭空实现；用 Route C 流程却声称与 Timeline 同等精度。

---

## Route D — 预留

新来源（如 Spine 时间轴、Shader 动画、粒子系统快照）到达时：

1. 在本文件追加一行路由表条目
2. 若需要超过半页细则，新建 `references/<route-name>-route.md`
3. 不修改主 SKILL 的 5 步工作流结构
