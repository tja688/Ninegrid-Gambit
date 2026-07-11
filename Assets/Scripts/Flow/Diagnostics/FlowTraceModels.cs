using System;
using System.Collections.Generic;

namespace NineGrid.Flow.Diagnostics
{
    /// <summary>
    /// 一局 FlowTrace 会话：全流程事件时间线，与 BattleTrace 共享 sessionId/seed。
    /// </summary>
    [Serializable]
    public sealed class FlowTraceSession
    {
        public int schemaVersion = 1;
        public string seed = "0";
        public string sessionId = string.Empty;
        public List<FlowTraceEvent> events = new List<FlowTraceEvent>();
    }

    /// <summary>
    /// 一条流程事件（Loop / UI / CoreGate / CombatSummary / Deck）。
    /// </summary>
    [Serializable]
    public sealed class FlowTraceEvent
    {
        public int index;
        public string category = string.Empty;
        public string name = string.Empty;
        public string loopState = string.Empty;
        public string phaseBefore = string.Empty;
        public string phaseAfter = string.Empty;
        public bool accepted = true;
        public int refBattleOpIndex = -1;
        public Dictionary<string, string> payload = new Dictionary<string, string>();
    }

    /// <summary>
    /// V1 稳定 category 常量。
    /// </summary>
    public static class FlowTraceCategory
    {
        public const string Loop = "Loop";
        public const string UI = "UI";
        public const string CoreGate = "CoreGate";
        public const string CombatSummary = "CombatSummary";
        public const string Deck = "Deck";
        public const string Economy = "Economy";
    }

    /// <summary>
    /// V1 稳定 name 常量，便于 skill 检索。
    /// </summary>
    public static class FlowTraceNames
    {
        public const string EnterMainMenu = "EnterMainMenu";
        public const string StartRun = "StartRun";
        public const string ReturnMainMenu = "ReturnMainMenu";
        public const string SetState = "SetState";
        public const string RewardPresented = "RewardPresented";
        public const string RewardChosen = "RewardChosen";
        public const string RoomPresented = "RoomPresented";
        public const string RoomChosen = "RoomChosen";
        public const string EnterRoom = "EnterRoom";
        public const string StartNode = "StartNode";
        public const string CombatHitSummary = "CombatHitSummary";
        public const string PostKillBoard = "PostKillBoard";
        public const string Victory = "Victory";
        public const string Defeat = "Defeat";
        public const string GoldGained = "GoldGained";
    }
}
