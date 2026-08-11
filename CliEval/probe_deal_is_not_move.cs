using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Utilities;
using QFramework;

// 行为验证：发牌（非盘面 → 盘面）不得再产出 CardMoved；盘面 → 盘面仍产出 CardMoved。
// 依赖 NineGridArchitecture 完整装配（含 EffectSystem/TriggerSystem 注册），只做事件与位置断言。
var arch = new NineGridArchitecture();
var pipeline = arch.GetSystem<IActionPipelineSystem>();
var registry = arch.GetModel<CardRegistry>();
var board = arch.GetModel<BoardModel>();
var deck = arch.GetModel<DeckModel>();

// 玩家卡（Avatar）落中心格。
var avatar = registry.Create("avatar.default", CardKind.Avatar);
board.SetAvatar(avatar, SlotId.Board(5));

// 场景 A：牌库中的滚石经 MoveCardAction 发到格3 —— 必须产出 CardDealt，不得产出 CardMoved。
var stone = registry.Create("trap.rolling_stone", CardKind.Trap);
deck.AddToDrawPile(stone, false);
pipeline.Enqueue(new MoveCardAction(stone.Uid, SlotId.Board(3), "trap.rolling_stone", "probe"));
var resolvedA = pipeline.RunToCompletion();
var eventsA = new List<string>();
for (var i = 0; i < pipeline.EventLog.Entries.Count; i++)
{
    var e = pipeline.EventLog.Entries[i];
    if (e.Type == CoreEventType.CardMoved || e.Type == CoreEventType.CardDealt)
    {
        eventsA.Add(e.Type + ":card=" + e.CardUid + ":from=" + e.FromSlot + ":to=" + e.ToSlot);
    }
}

// 场景 B：盘面上的滚石从格1移到格3 —— 必须产出 CardMoved。
var stoneB = registry.Create("trap.rolling_stone", CardKind.Trap);
board.PlaceCard(stoneB, SlotId.Board(1));
pipeline.Enqueue(new MoveCardAction(stoneB.Uid, SlotId.Board(3), "trap.rolling_stone", "probe"));
var resolvedB = pipeline.RunToCompletion();
var eventsB = new List<string>();
for (var i = 0; i < pipeline.EventLog.Entries.Count; i++)
{
    var e = pipeline.EventLog.Entries[i];
    if (e.Type == CoreEventType.CardMoved || e.Type == CoreEventType.CardDealt)
    {
        eventsB.Add(e.Type + ":card=" + e.CardUid + ":from=" + e.FromSlot + ":to=" + e.ToSlot);
    }
}

var stonePosA = registry.Get(stone.Uid).Slot.Value;
var stonePosB = registry.Get(stoneB.Uid).Slot.Value;
return "A_resolved=" + resolvedA + " A_events=[" + string.Join(";", eventsA) + "] A_pos=" + stonePosA
    + " | B_resolved=" + resolvedB + " B_events=[" + string.Join(";", eventsB) + "] B_pos=" + stonePosB;
