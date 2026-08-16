using System;
using System.Collections.Generic;

namespace NineGrid.Flow.Diagnostics
{
    /// <summary>
    /// 一局 BattleTrace 会话：统一门禁旁路产出的语义化作战记录。
    /// schemaVersion 2（#142）：op 新增 floor/nodeIndex 关联字段。
    /// </summary>
    [Serializable]
    public sealed class BattleTraceSession
    {
        public int schemaVersion = 2;
        public string seed = "0";
        public string sessionId = string.Empty;
        public string runTag = string.Empty;
        public string runTagNote = string.Empty;
        public List<BattleTraceOp> ops = new List<BattleTraceOp>();
    }

    /// <summary>
    /// 一次结算门记录（优先 ApplyCombatHit；也可为 PostKillBoard / StartNode）。
    /// floor / nodeIndex 由 Recorder 记录时自动从 RunModel 解析，供 Run-Floor-Node-Battle 关联。
    /// </summary>
    [Serializable]
    public sealed class BattleTraceOp
    {
        public int opIndex;
        public string opKind = string.Empty;
        public string reason = string.Empty;
        public string apiPath = string.Empty;
        public string phaseBefore = string.Empty;
        public string phaseAfter = string.Empty;
        /// <summary>#142：当前层号（RunModel.Floor，1-based）。</summary>
        public string floor = string.Empty;
        /// <summary>#142：当前节点索引（shell NodeIndex 优先，回退 RunModel.NodeIndex）。</summary>
        public string nodeIndex = string.Empty;
        public BattleTraceCardSnap attacker;
        public BattleTraceCardSnap target;
        public int eventStartIndex;
        public int eventEndIndex;
        public List<BattleTraceEventRow> events = new List<BattleTraceEventRow>();
        public BattleTracePresentation presentation = new BattleTracePresentation();
        public BattleTraceVerdictHints verdictHints = new BattleTraceVerdictHints();
    }

    /// <summary>
    /// 结算前门禁两侧卡牌快照。
    /// armor = 当前护甲（CurrentArmor，与卡面 / 事件 RemainingArmor 同口径）；
    /// effectiveArmor = 有效护甲（管线结算值，含借甲图腾等外部修正）。
    /// </summary>
    [Serializable]
    public sealed class BattleTraceCardSnap
    {
        public int uid;
        public string defId = string.Empty;
        public string kind = string.Empty;
        public int atk;
        public int hp;
        /// <summary>当前护甲（卡面 / RemainingArmor 同口径）。</summary>
        public int armor;
        /// <summary>有效护甲（管线结算值，可含借甲等外部修正）。</summary>
        public int effectiveArmor;
    }

    /// <summary>
    /// EventLog 切片中的一行，供 AI/回归直接读「谁打了谁、打了多少」。
    /// DamageDealt 行的 armorDamage（毛，含金币代偿）/ hpDamage / goldAbsorbedArmor（代偿量）
    /// 直接落字段，无需回源码拆账；remainingArmor 为该拍结算后当前甲（净）。
    /// </summary>
    [Serializable]
    public sealed class BattleTraceEventRow
    {
        public long sequence;
        public string type = string.Empty;
        public string actionName = string.Empty;
        public int actorUid;
        public int targetUid;
        public int cardUid;
        public int amount;
        public int delta;
        public int remainingHp;
        public int remainingArmor;
        /// <summary>仅 DamageDealt：毛甲伤（含金币代偿）。</summary>
        public int armorDamage;
        /// <summary>仅 DamageDealt：实际扣血伤害。</summary>
        public int hpDamage;
        /// <summary>仅 DamageDealt：金币盔甲代偿的甲伤量（含在 armorDamage 内）。</summary>
        public int goldAbsorbedArmor;
        public string sourceDefId = string.Empty;
        public string cause = string.Empty;
        public string summary = string.Empty;
    }

    /// <summary>
    /// 表现侧摘要镜像（与 CombatHitPresentationResult 对齐）。
    /// </summary>
    [Serializable]
    public sealed class BattleTracePresentation
    {
        public bool accepted;
        public int damageAmount;
        public bool targetKilled;
        public bool avatarDefeated;
        public bool nodeClearedOrRewardPhase;
        /// <summary>Core 拒结算原因；accepted=true 时为空。</summary>
        public string rejectReason = string.Empty;
    }

    /// <summary>
    /// 快速判决提示，便于扫一眼定位「反击致死」等。
    /// </summary>
    [Serializable]
    public sealed class BattleTraceVerdictHints
    {
        public bool avatarDefeated;
        public bool targetKilled;
        public int extraDamageDealtCount;
        public List<string> effectTriggeredIds = new List<string>();
    }
}
