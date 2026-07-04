# UI / Dialogue / Notice

全局 UI 挂在 `Assets/Prefabs/UI.prefab`，仅保留在 **MainScene**。`UiSystem` 负责 `DontDestroyOnLoad`，其他场景的 `Overlay UI` 已移除，运行时也会再清一次残留。

## 组件

| 组件 | 职责 |
|------|------|
| `UiSystem` | 全局单例，调控 Overlay / Portrait / DialogBox / 各 Text |
| `DialogueSystem` | 对话演出与对话池取用 |
| `NoticeSystem` | NoticeText / FactoryText 通知 |

入口：`UiSystem.Instance.Dialogue` / `UiSystem.Instance.Notice`

## 对话演出顺序

1. Portrait 淡入（alpha 0→1）
2. Dialog Box 宽度 0→6，`Ease.OutCubic`（先快后慢，Sliced `SpriteRenderer.size`）
3. **展开完成后** 才激活 Overlay 上的 DialogText
4. `TextAnimator_TMP`：`SetText(text, hideText: true)` → `SetVisibilityEntireText(true, canPlayEffects: true)`（仅入场，无出场）

推进：空格 / 回车 / 左键。`Advance()` 到末句后 `Close()`。

说话人：`DialogueLine.speakerTag`（如 `Captain` / `Player`），运行时可读 `DialogueSystem.CurrentSpeakerTag`。

## 对话池

`Assets/ScriptableObjects/UI/DialoguePool.asset`

```csharp
UiSystem.Instance.Dialogue.Play("prologue_intro");
```

序章 `GameFlowState.Prologue` 进入时自动播放 `prologue_intro`，对话结束后 `Advance()` 进岛屿。

## 通知池

`Assets/ScriptableObjects/UI/NoticePool.asset`

| Channel | UI |
|---------|-----|
| `Notice` | NoticeText |
| `Factory` | FactoryText |

```csharp
UiSystem.Instance.Notice.Show("notice_sample");
UiSystem.Instance.Notice.Show("factory_sample");
// 或
UiSystem.Instance.Notice.Show(NoticeChannel.Notice, "自定义文案", duration: 2f);
```

同样挂 `TextAnimator_TMP`，仅入场效果。`duration <= 0` 不自动隐藏。

## 扩展

- 对话池加序列：Inspector 里往 `DialoguePool.sequences` 追加
- 通知加通道：扩展 `NoticeChannel` + `UiSystem.GetNoticeChannelObject`
