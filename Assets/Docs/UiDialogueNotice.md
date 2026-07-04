# UI / Dialogue / Notice

全局 UI 挂在 `Assets/Prefabs/UI.prefab`，仅保留在 **MainScene**。`UiSystem` 负责 `DontDestroyOnLoad`。

## 对话池（说话人档案）

`Assets/ScriptableObjects/UI/DialoguePool.asset`

- **Speakers**：配置唯一 `name` + `portrait` 立绘
- **Sequences / Lines**：台词只填 `speakerName`（与 Speakers.name 精确匹配），自动取立绘

```csharp
UiSystem.Instance.Dialogue.Play("prologue_intro");
```

## 对话状态机

**入场（首句）**

1. Portrait 淡入  
2. Dialog Box 宽度 0→6（OutCubic）  
3. 展开完成后激活 DialogText，TextAnimator 入场效果  

**推进**：对话进行中鼠标左键点击。

**同轮换角色**：瞬间切换立绘，仅 DialogText 重新入场（面板不重开）。

**退场（整段结束）**

1. 先退场 Text（直接隐藏，无出场特效）  
2. 再收对话面板（宽度 6→0）  
3. 最后淡出立绘  

完成后触发 `SequenceCompleted`。

## 通知

`NoticePool`：`Notice` → NoticeText，`Factory` → FactoryText。

```csharp
UiSystem.Instance.Notice.Show("notice_sample");
```
