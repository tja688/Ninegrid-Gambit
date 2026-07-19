using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards.Convergence;
using UnityEngine;

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
    }

    /// <summary>
    /// 场地拾取帮助卡/道具后的摘要。
    /// </summary>
    public struct PickupItemPresentationResult
    {
        public bool Accepted;
        public int CardUid;
        public bool AcquiredToHand;
        public bool RemovedWithoutHand;
        public PostKillCardMove[] Moves;
        public PostKillCardDeal[] Deals;
        public int[] RemovedUids;
        /// <summary>保序步骤流；非空时 Drain 逐步播放。</summary>
        public BoardPresentationStep[] Steps;
        public bool NodeClearedOrRewardPhase;

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

    /// <summary>
    /// Cards → Flow 交战/手牌结算桥：Cards 不引用 Core/Flow，由 InBattleManager 在 Awake 注册。
    /// </summary>
    public static class CombatHitSink
    {
        /// <summary>
        /// 下一次 RequestCombatHit 的 BattleTrace reason（环境变量式传递，不改委托签名）。
        /// Field 写入，Flow 的 ApplyCombatHitFromCore 读取并清空。例：PlayerAttack / CounterAttack。
        /// </summary>
        public static string PendingTraceReason;

        /// <summary>
        /// 局内选择覆盖层（宝箱/属性提升三选一）激活时为 true；Cards 侧 IsBusy/点击门禁读取，不引用 Flow。
        /// </summary>
        public static bool ChoiceOverlayActive;

        /// <summary>
        /// 场地多选选卡模式激活时为 true；允许场地卡点击 toggle，但阻断攻击/空槽/手牌拖拽。
        /// </summary>
        public static bool BoardSelectModeActive;

        /// <summary>
        /// StartNode 开局发牌编排进行中（含补牌 flight 收束）；Cards 侧拾取/空槽门禁读取，不引用 Flow。
        /// </summary>
        public static bool OpeningPresentationActive;

        /// <summary>
        /// 表现层单输入锁：一次玩家操作（写 Core → Drain 播完）期间为 true。
        /// Cards 侧 IsBusy 聚合读取，不引用 Flow。
        /// </summary>
        public static bool PresentationLocked { get; private set; }

        /// <summary>
        /// 尝试获取表现锁。已锁时返回 false（防重入）。
        /// </summary>
        public static bool TryBeginPresentationLock(string reason = null)
        {
            if (PresentationLocked)
            {
                return false;
            }

            PresentationLocked = true;
            if (!string.IsNullOrEmpty(reason))
            {
                Debug.Log($"[CombatHitSink] PresentationLocked begin: {reason}");
            }

            return true;
        }

        /// <summary>
        /// 释放表现锁。未持锁时为 no-op。
        /// </summary>
        public static void EndPresentationLock(string reason = null)
        {
            if (!PresentationLocked)
            {
                return;
            }

            PresentationLocked = false;
            if (!string.IsNullOrEmpty(reason))
            {
                Debug.Log($"[CombatHitSink] PresentationLocked end: {reason}");
            }
        }

        /// <summary>
        /// 生命周期清场强制解锁（取消/回主菜单）。
        /// </summary>
        public static void ForceEndPresentationLock(string reason = null)
        {
            if (!PresentationLocked)
            {
                return;
            }

            PresentationLocked = false;
            Debug.Log($"[CombatHitSink] PresentationLocked force-end: {reason ?? "clear"}");
        }

        /// <summary>
        /// 开局 / 回主菜单强制清零静态输入门禁，避免上一局覆盖层或选卡模式粘住整场 IsBusy。
        /// </summary>
        public static void ResetInputGates(string reason = null)
        {
            ChoiceOverlayActive = false;
            BoardSelectModeActive = false;
            OpeningPresentationActive = false;
            ForceEndPresentationLock(reason ?? "ResetInputGates");
            DirectorMainlineBusy = false;
        }

        /// <summary>ApplyCombatHit(attackerUid, targetUid) → 摘要。</summary>
        public static Func<int, int, CombatHitPresentationResult> ApplyCombatHit;

        /// <summary>ResolvePostKillBoard() → 摘要（含 Moved/Dealt）。</summary>
        public static Func<PostKillBoardPresentationResult> ResolvePostKillBoard;

        /// <summary>预估 attacker 对 target 是否足以击杀（选 Lethal Profile 用）。</summary>
        public static Func<int, int, bool> EstimateWillKill;

        /// <summary>解析玩家攻击实际目标（嘲讽重定向等）。</summary>
        public static Func<int, int> ResolvePlayerAttackTarget;

        /// <summary>命中后刷受击卡数值。</summary>
        public static Action<ManagedCard> SyncCardPresentation;

        /// <summary>世界坐标飘字。</summary>
        public static Action<Vector3, int> SpawnDamageNumber;

        /// <summary>击杀后按 Core Board 最小同步表现占格（安全网，非主路径）。</summary>
        public static Action SyncBoardFromCore;

        /// <summary>缓释击杀后盘面摘要：hop 已有卡 + 发新牌 + 软对齐。</summary>
        public static Func<PostKillBoardPresentationResult, CancellationToken, UniTask> DrainPostKillBoard;

        /// <summary>场地拾取：ApplyPickupItem(groundSlot) → 摘要。</summary>
        public static Func<int, PickupItemPresentationResult> ApplyPickupItem;

        /// <summary>空槽 explore：提交导演意图（忙时缓冲）；返回是否接纳。</summary>
        public static Func<int, bool> TrySubmitExploreIntent;

        /// <summary>攻击：提交导演意图（忙时缓冲）；返回是否接纳。</summary>
        public static Func<int, bool> TrySubmitAttackIntent;

        /// <summary>
        /// 表演导演主线在跑。已迁流程以此为输入互斥真相；Cards 侧 IsBusy 聚合读取，不引用 Flow。
        /// </summary>
        public static bool DirectorMainlineBusy;

        /// <summary>手牌打出：ApplyUseItem(itemUid, selectedCardUids, selectedOption) → 摘要。</summary>
        public static Func<int, int[], string, UseItemPresentationResult> ApplyUseItem;

        /// <summary>清场胜利 / 玩家战败 → Notice + 回主菜单。</summary>
        public static Action<bool> NotifyBattleEnded;

        public static CombatHitPresentationResult RequestCombatHit(int attackerUid, int targetUid)
        {
            if (ApplyCombatHit == null)
            {
                Debug.LogWarning("[CombatHitSink] ApplyCombatHit 未注册。");
                return default;
            }

            return ApplyCombatHit(attackerUid, targetUid);
        }

        public static PostKillBoardPresentationResult RequestPostKillBoard()
        {
            if (ResolvePostKillBoard == null)
            {
                Debug.LogWarning("[CombatHitSink] ResolvePostKillBoard 未注册。");
                return default;
            }

            return ResolvePostKillBoard();
        }

        public static bool RequestEstimateWillKill(int attackerUid, int targetUid)
        {
            return EstimateWillKill != null && EstimateWillKill(attackerUid, targetUid);
        }

        public static int RequestResolvePlayerAttackTarget(int intendedTargetUid)
        {
            if (ResolvePlayerAttackTarget == null)
            {
                return intendedTargetUid;
            }

            return ResolvePlayerAttackTarget(intendedTargetUid);
        }

        public static void RequestSyncCard(ManagedCard card)
        {
            SyncCardPresentation?.Invoke(card);
        }

        public static void RequestDamageNumber(Vector3 worldPosition, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            SpawnDamageNumber?.Invoke(worldPosition, amount);
        }

        public static void RequestSyncBoardFromCore()
        {
            SyncBoardFromCore?.Invoke();
        }

        public static UniTask RequestDrainPostKillBoard(
            PostKillBoardPresentationResult result,
            CancellationToken cancellationToken = default)
        {
            if (DrainPostKillBoard == null)
            {
                Debug.LogWarning("[CombatHitSink] DrainPostKillBoard 未注册。");
                return UniTask.CompletedTask;
            }

            return DrainPostKillBoard(result, cancellationToken);
        }

        public static PickupItemPresentationResult RequestPickupItem(int groundSlot)
        {
            if (ApplyPickupItem == null)
            {
                Debug.LogWarning("[CombatHitSink] ApplyPickupItem 未注册。");
                return default;
            }

            return ApplyPickupItem(groundSlot);
        }

        public static bool RequestExploreIntent(int groundSlot)
        {
            if (TrySubmitExploreIntent == null)
            {
                Debug.LogWarning("[CombatHitSink] TrySubmitExploreIntent 未注册。");
                return false;
            }

            return TrySubmitExploreIntent(groundSlot);
        }

        public static bool RequestAttackIntent(int groundSlot)
        {
            if (TrySubmitAttackIntent == null)
            {
                Debug.LogWarning("[CombatHitSink] TrySubmitAttackIntent 未注册。");
                return false;
            }

            return TrySubmitAttackIntent(groundSlot);
        }

        public static UseItemPresentationResult RequestUseItem(
            int itemUid,
            int[] selectedCardUids,
            string selectedOption = null)
        {
            if (ApplyUseItem == null)
            {
                Debug.LogWarning("[CombatHitSink] ApplyUseItem 未注册。");
                return default;
            }

            return ApplyUseItem(itemUid, selectedCardUids, selectedOption);
        }

        public static UseItemPresentationResult RequestUseItem(
            int itemUid,
            int? targetCardUid,
            string selectedOption = null)
        {
            int[] selected = null;
            if (targetCardUid.HasValue && targetCardUid.Value > 0)
            {
                selected = new[] { targetCardUid.Value };
            }

            return RequestUseItem(itemUid, selected, selectedOption);
        }

        public static void RequestBattleEnded(bool victory)
        {
            NotifyBattleEnded?.Invoke(victory);
        }

        /// <summary>
        /// 节点通关结算就绪（奖励/房间），非整局胜负。由 InBattleManager 注册。
        /// </summary>
        public static Action NotifyNodeSettlementReady;

        public static void RequestNodeSettlement()
        {
            NotifyNodeSettlementReady?.Invoke();
        }
    }
}
