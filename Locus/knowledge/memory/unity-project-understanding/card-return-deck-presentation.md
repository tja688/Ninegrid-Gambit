---
id: kd_97a73f80-c55c-4e66-8c75-65490091c380
type: memory
path: unity-project-understanding/card-return-deck-presentation.md
title: card-return-deck-presentation
inheritInjectMode: true
summaryEnabled: true
commandEnabled: false
readOnly: false
inheritAiConfig: true
createdAt: 1785486692592
updatedAt: 1785486692593
---

# card-return-deck-presentation

## Summary
场上卡回卡组的表演链路：洗入表演系统（ShuffleInto 系列前缀）与快递交换（ExchangeWithDrawPile）的投影规则。

<!-- locus:body:start -->
## 场上卡回卡组的表演链路

### 洗入表演系统（上飞入组）
- 入口：`BoardPresentationPlayer.PresentShuffleIntoDeckFromEventLog(startIndex)` 扫描 EventLog，经 `ShuffleIntoDeckPresentationScanner.Collect` 入 sink，`DrainDealsAsync` 开头 `FlushPendingShuffleIntoPresentationAsync` 播出（Deal 前）。
- 识别前缀（`TryParseShuffleIntoEvent`）：`shuffleInto:`（ShuffleIntoDrawPile，NewCard）、`shuffleRandom:`（ShuffleRandomContentIntoDrawPile，RandomCard）、`shuffleExisting:`（ShuffleCardIntoDrawPile，ExistingCard）、`exchangeToDraw:`（ExchangeWithDrawPile，ExistingCard）。
- ExistingCard 分支：`deckManager.LaunchReturnFieldCardToDeck(existing)` → `LaunchFieldExitThenDeckInsert`（垂直上飞离画 + 从卡组上方 ripple 入组，发射后不管）。骷髅散架/合体同款表演。

### 快递交换（skill.delivery.move / ExchangeWithDrawPile）
- Core 是真交换：道具卡 `board.RemoveCard` + `deck.AddToDrawPile` + 洗牌；怪物卡从抽牌堆取出 `board.PlaceCard` 到道具卡原格。
- 事件：道具卡 `CardDealt(board→None)` message=`exchangeToDraw:<defId>`；怪物卡 `CardDealt(None→board)` message=`exchangeDraw:<defId>`。
- 投影规则（2026-07 修复）：`BoardPresentationStepProjector` 对 `exchangeToDraw` 不投影 Remove（避免碎裂动画），由洗入表演系统接管上飞入组；`exchangeDraw` 仍投影为 Deal（从卡组飞出）。
- 注意：`shuffleExisting:` 的 Remove 投影是有意保留的（探求失败回滚等路径），只跳过 `ExchangeWithDrawPile` 的离场事件。

### 相关文件
- `Assets/Scripts/NineGrid.Presentation/Flow/ShuffleIntoDeckPresentationScanner.cs`
- `Assets/Scripts/NineGrid.Presentation/Flow/BoardPresentationStepProjector.cs`
- `Assets/Scripts/NineGrid.Presentation/Flow/BattleSession/BoardPresentationPlayer.cs`
- `Assets/Scripts/NineGrid.Foundation/NineGrid.Core/Domain/Actions/EffectActions.cs`（ExchangeWithDrawPileAction）
<!-- locus:body:end -->
