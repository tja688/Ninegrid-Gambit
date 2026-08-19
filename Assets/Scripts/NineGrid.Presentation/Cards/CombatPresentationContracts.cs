using NineGrid.Cards.Convergence;

namespace NineGrid.Cards
{
    /// <summary>
    /// 一次 DamageDealt 对应的飘字（可落在主目标或同段反伤目标上）。
    /// </summary>
    public struct CombatDamagePopup
    {
        public int TargetUid;
        public int Amount;
    }

    /// <summary>
    /// 一次可信命中结算后的表现侧摘要（由 Flow 桥填充；Cards 不引用 Core）。
    /// </summary>
    public struct CombatHitPresentationResult
    {
        public bool Accepted;
        public int DamageAmount;
        public int RemainingHp;
        public int RemainingArmor;
        public bool TargetKilled;
        public bool AvatarDefeated;
        public bool NodeClearedOrRewardPhase;
        /// <summary>本段所有 amount&gt;0 的 DamageDealt，按事件序；可多条（主伤+反伤）。</summary>
        public CombatDamagePopup[] DamagePopups;
        /// <summary>保序步骤流；非空时优先于扁平 Moves/Deals/RemovedUids。</summary>
        public BoardPresentationStep[] Steps;
        /// <summary>本段 CardMoved（含 OnBattle 旋转 hop），对齐 UseItem/Pickup 缓释契约。</summary>
        public PostKillCardMove[] Moves;
        /// <summary>本段 CardDealt（旋转后补牌等）。</summary>
        public PostKillCardDeal[] Deals;
        /// <summary>本段技能/效果 CardRemoved / CardKilled（非交战主目标尸体路径）。</summary>
        public int[] RemovedUids;

        public readonly bool HasOrderedSteps => Steps != null && Steps.Length > 0;

        public readonly bool HasBoardDelta =>
            HasOrderedSteps
            || (Moves != null && Moves.Length > 0)
            || (Deals != null && Deals.Length > 0)
            || (RemovedUids != null && RemovedUids.Length > 0);
    }

    /// <summary>
    /// 击杀后盘面：一张卡从 from→to（对应 Core CardMoved）。
    /// </summary>
    public struct PostKillCardMove
    {
        public int Uid;
        public int FromSlot;
        public int ToSlot;
    }

    /// <summary>
    /// 击杀后盘面：一张新牌落到 slot（对应 Core CardDealt）。
    /// </summary>
    public struct PostKillCardDeal
    {
        public int Uid;
        public int Slot;
        public string DefId;
    }

    /// <summary>
    /// 盘面表现步骤种类（由 Flow 自 EventLog 投影，非 Core 契约）。
    /// </summary>
    public enum BoardPresentationStepKind
    {
        Rotate,
        Swap,
        Move,
        Deal,
        Remove,
    }

    /// <summary>
    /// 多跳投影策略（StepProjector 编排层）。
    /// S = 串行多 beat 逐跳可见；C = 合并单 beat 只看终点。
    /// </summary>
    public enum MultiHopProjectionStrategy
    {
        /// <summary>策略 S：N 跳 → N 个 Sync Step，逐跳精确落格。</summary>
        SerialVisible = 0,
        /// <summary>策略 C：N 跳 → 1 个 Async Step，只保留终点。</summary>
        CollapsedEndpoint = 1,
    }

    /// <summary>
    /// 保序盘面表现步骤：一次 Core action 或独立事件对应一步演出。
    /// </summary>
    public struct BoardPresentationStep
    {
        public long CoreSequence;
        public int ActionId;
        public BoardPresentationStepKind Kind;
        /// <summary>旋转方向；仅 <see cref="BoardPresentationStepKind.Rotate"/> 有效。</summary>
        public bool Clockwise;
        /// <summary>Sync = 必达就位 + 租约；Async = 后发先至、无必达承诺。</summary>
        public CommitmentKind Commitment;
        /// <summary>多跳投影策略标签（Rotate/Swap/Deal/Remove 恒 SerialVisible）。</summary>
        public MultiHopProjectionStrategy MultiHopStrategy;
        public PostKillCardMove[] Moves;
        public PostKillCardDeal[] Deals;
        public int[] RemovedUids;
    }

    /// <summary>
    /// 击杀后盘面结算摘要（Core 一次算完的结果，供表现缓冲缓释）。
    /// </summary>
    public struct PostKillBoardPresentationResult
    {
        public bool Accepted;
        public bool NodeClearedOrRewardPhase;
        public bool AvatarDefeated;
        /// <summary>保序步骤流；非空时 Drain 逐步播放，扁平 Moves 仅作回退。</summary>
        public BoardPresentationStep[] Steps;
        public PostKillCardMove[] Moves;
        public PostKillCardDeal[] Deals;
        /// <summary>
        /// 本段技能/效果导致的 CardRemoved / CardKilled（非交战主目标尸体）。
        /// Drain 须先播默认死亡退场，再 hop，再补牌。
        /// </summary>
        public int[] RemovedUids;
        /// <summary>旋转/移动技能等造成的 DamageDealt 飘字（如投石）。</summary>
        public CombatDamagePopup[] DamagePopups;
        /// <summary>
        /// 神圣决斗惩罚（非空时有效）：本批各决斗持有者对玩家的惩罚伤害，
        /// 表现层须以决斗者攻击编排逐个打出（而非只播触发脉冲 + 掉血）。
        /// </summary>
        public HolyDuelPunishmentEntry[] HolyDuelPunishments;
    }

    /// <summary>
    /// 单条神圣决斗惩罚：一名决斗持有者对玩家卡的惩罚伤害摘要。
    /// </summary>
    public struct HolyDuelPunishmentEntry
    {
        /// <summary>决斗持有者怪物 uid。</summary>
        public int HolderUid;
        /// <summary>惩罚伤害量（当前设计 2）。</summary>
        public int Amount;
    }

    /// <summary>
    /// 场地拾取帮助卡/道具后的摘要。
    /// </summary>
    public struct PickupItemPresentationResult
    {
        public bool Accepted;
        public string Reason;
        public int CardUid;
        public bool AcquiredToHand;
        public bool RemovedWithoutHand;
        public PostKillCardMove[] Moves;
        public PostKillCardDeal[] Deals;
        public int[] RemovedUids;
        /// <summary>保序步骤流；非空时 Drain 逐步播放。</summary>
        public BoardPresentationStep[] Steps;
        public bool NodeClearedOrRewardPhase;

        /// <summary>
        /// 本次拾取 Core 事件切片起点：节拍冲刷延迟到盘面 Drain 落地后统一消费
        /// （触发脉冲 → 效果打击 → 其余 Impact/Settled，ADR-0050 拾取补丁）。
        /// </summary>
        public int EventLogStartIndex;


        /// <summary>
        /// 本拾取已交 PresentationDirector 锁步剧本（拾卡分拍 + 互动链按批表演）；
        /// 手牌侧不再自行 Apply / 冲刷切片，只等导演 flush 后做入手动画。
        /// </summary>
        public bool RoutedToDirector;
        public readonly bool HasOrderedSteps => Steps != null && Steps.Length > 0;
    }

    /// <summary>
    /// 手牌打出后的摘要。
    /// </summary>
    public struct UseItemPresentationResult
    {
        public bool Accepted;
        public bool TargetKilled;
        /// <summary>本段 UseItem EventLog 中 CardKilled 的 uid（供表现侧 Vacate 尸体）。</summary>
        public int[] KilledTargetUids;
        /// <summary>本段所有 amount&gt;0 的 DamageDealt（飞刀等直伤）；可多条。</summary>
        public CombatDamagePopup[] DamagePopups;
        /// <summary>主目标（选定怪）上的伤害合计兜底；EventLog 漏 DamageDealt 时仍可飘字。</summary>
        public int DamageAmount;
        /// <summary>拖放选定的主目标 uid；与 DamageAmount 配对做强兜底。</summary>
        public int PrimaryTargetUid;
        public bool NodeClearedOrRewardPhase;
        public bool AvatarDefeated;
        public bool RewardChoicePending;
        public PostKillBoardPresentationResult PostKillBoard;
    }
}
