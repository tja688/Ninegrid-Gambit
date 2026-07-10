using System;
using System.Threading;
using Cysharp.Threading.Tasks;
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
    /// 击杀后盘面结算摘要（Core 一次算完的结果，供表现缓冲缓释）。
    /// </summary>
    public struct PostKillBoardPresentationResult
    {
        public bool Accepted;
        public bool NodeClearedOrRewardPhase;
        public bool AvatarDefeated;
        public PostKillCardMove[] Moves;
        public PostKillCardDeal[] Deals;
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
        public bool NodeClearedOrRewardPhase;
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

        /// <summary>ApplyCombatHit(attackerUid, targetUid) → 摘要。</summary>
        public static Func<int, int, CombatHitPresentationResult> ApplyCombatHit;

        /// <summary>ResolvePostKillBoard() → 摘要（含 Moved/Dealt）。</summary>
        public static Func<PostKillBoardPresentationResult> ResolvePostKillBoard;

        /// <summary>预估 attacker 对 target 是否足以击杀（选 Lethal Profile 用）。</summary>
        public static Func<int, int, bool> EstimateWillKill;

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

        /// <summary>空槽点击：ClickEmpty(groundSlot) → 摘要（含 Moved/Dealt）。</summary>
        public static Func<int, PostKillBoardPresentationResult> ApplyClickEmpty;

        /// <summary>手牌打出：ApplyUseItem(itemUid, optionalTargetUid, selectedOption) → 摘要。</summary>
        public static Func<int, int?, string, UseItemPresentationResult> ApplyUseItem;

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

        public static PostKillBoardPresentationResult RequestClickEmpty(int groundSlot)
        {
            if (ApplyClickEmpty == null)
            {
                Debug.LogWarning("[CombatHitSink] ApplyClickEmpty 未注册。");
                return default;
            }

            return ApplyClickEmpty(groundSlot);
        }

        public static UseItemPresentationResult RequestUseItem(
            int itemUid,
            int? targetCardUid,
            string selectedOption = null)
        {
            if (ApplyUseItem == null)
            {
                Debug.LogWarning("[CombatHitSink] ApplyUseItem 未注册。");
                return default;
            }

            return ApplyUseItem(itemUid, targetCardUid, selectedOption);
        }

        public static void RequestBattleEnded(bool victory)
        {
            NotifyBattleEnded?.Invoke(victory);
        }
    }
}
