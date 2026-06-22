# 播放安全 — 首帧 deltaTime 尖峰（DOTween Sequence）

凡 Performance 使用 `DOTween.Sequence`，且存在「同帧重对象生成 + 立即播放」，**必须**阅读并落实本节。写入 Performance 契约，交付时提醒人对照。

---

## 先破案：为什么「前 1~3 张牌瞬间出现在终点」

你描述的症状特别典型：**代码逻辑完美无缺、排查不出、怀疑到 DOTween 内核，但你自己在 DOTween Timeline 里预览又完全正常。** 这几乎可以锁定一个原因——**不是逻辑 bug，是「首帧 deltaTime 尖峰」吃掉了开头几个 tween**。

机理是这样的：

- 你的 DOTween Timeline 在运行时本质就是一条 `DOTween.Sequence()`（见 [`dotween-timeline-route.md`](dotween-timeline-route.md)），靠每个 clip 的 `delay` 错开时序。
- DOTween 的 Sequence 默认用 `Time.deltaTime` 推进。**它不会 clamp 首帧的超大 delta。**
- 在游戏里，AI 写的脚本通常是**同一帧里**：实例化/激活 20 张卡 → `GetComponent` → 建一条大 Sequence → 立刻播放。实例化 20 个对象本身很贵，于是**播放开始的那一帧 deltaTime 异常巨大**（可能 0.1~0.3 秒）。
- Sequence 第一次 `Update` 就直接快进了这 0.1~0.3 秒。凡是 `delay` 落在这个窗口里的前 1~3 个 tween，**直接被判定为「已经播完」**，于是瞬移到终点。后面的牌因为 delay 更大，逃过了首帧，所以正常。

**为什么你自己 Timeline 预览永远正常**：编辑器里点预览时，场景早就 warm 好了，没有「同帧实例化 20 个对象」的开销，首帧 delta 是正常的 16ms，自然一张都不会被吃。**这就是「同一份编排，你能复现、AI 不能」的真正原因——差别不在代码，在运行时上下文（冷启动 vs 热预览）。**

这也解释了为什么你怎么查代码都查不出：因为代码真的没错。

破解办法（任选，建议前两个一起上）：

1. **生成对象和启动 Sequence 分两帧**。先实例化/布置好所有卡（这一帧很贵无所谓），`yield return null` 等一帧，**下一帧再 `sequence.Play()`**。这样启动帧的 delta 是正常的，开头几张不会被吃。这是最稳的根治。
2. 全局开 `DOTween.useSmoothDeltaTime = true`（用 `Time.smoothDeltaTime` 推进，自动平滑尖峰），并设 `DOTween.maxSmoothUnscaledTime`。这是兜底。
3. 建 Sequence 时先 `.Pause()`，所有 tween 加完后再 `.Restart()`/`.Play()`，避免「边建边被推进」。

> 记住这条经验：**只要你看到「开头几个元素瞬移、越靠后越正常」，第一反应永远是首帧 delta 尖峰，而不是去逐行查逻辑。** 这是 DOTween 在「重对象生成 + 同帧播放」场景下的头号坑。

---

## 落地到 Performance 契约

### 1. deferPlayOneFrame（默认开启）

```csharp
[SerializeField] private bool deferPlayOneFrame = true;

// 建完 Sequence 后：
if (deferPlayOneFrame)
    StartCoroutine(PlayNextFrame(sequence));
else
    sequence.Restart();

private IEnumerator PlayNextFrame(Sequence sequence)
{
    yield return null;
    sequence.Restart();
}
```

工程范例：[`CardDeckEntryPerformance.cs`](../../../Assets/Scripts/NineGrid.Presentation/Performance/CardDeckEntryPerformance.cs)

### 2. 项目级 smoothDeltaTime 兜底

在 Bootstrap / 游戏初始化处配置一次即可，**不要每个 Performance 重复设置**：

```csharp
DOTween.useSmoothDeltaTime = true;
DOTween.maxSmoothUnscaledTime = 0.15f; // 按项目调
```

**分工：** `deferPlayOneFrame` 是根治；`useSmoothDeltaTime` 是兜底。二者不互相替代。

### 3. Sequence 先 Pause 再 Play

```csharp
var sequence = DOTween.Sequence().SetTarget(this).Pause();
// ... Insert 所有 tween ...
sequence.OnComplete(...);
// 通过 defer 或下一帧 Restart()
```

### 诊断口诀

**「前几个瞬移、后面正常 → 首帧 delta 尖峰」**

与 stagger 编排的关系：当错峰 delay 由代码控制时，整批 Play 推迟一帧可根治前几张被吃；这也是「一份切片 × N 次调用」优于「复刻整条 20 对象 Timeline」的原因之一。
