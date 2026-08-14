using System.Collections.Generic;
using QFramework;

namespace NineGrid.Core
{
    /// <summary>
    /// 追踪玩家当前交战敌人与交战窗口是否打开。
    /// EngagedEnemyUid 供 UntilEnemyChanges；IsEngagementActive 供 OnBattle 门禁（#78 / ADR-0012）。
    /// IsLeaveTrapBroken 即战斗房清关标志（#113 / ADR-0026；<c>IDeckSystem.IsNodeCleared</c> 读此位）。
    /// 开局真怪 N / 击破进度 / 离开机关是否已编入或洗入（ADR-0026：普通房开局编入；层主房击破开局层主后洗入）。
    /// </summary>
    public sealed class BattleContextModel : AbstractModel
    {
        private readonly HashSet<int> mOpeningTrueMonsterUids = new HashSet<int>();
        private readonly HashSet<int> mOpeningBossMonsterUids = new HashSet<int>();
        private readonly List<DeferredBoardMotion> mDeferredBoardMotions = new List<DeferredBoardMotion>();

        public BindableProperty<int> EngagedEnemyUid { get; private set; }

        /// <summary>
        /// BeginPlayerMonsterEngagement 与 EndBattleScopeCleanup 之间为 true。
        /// 与 EngagedEnemyUid 解耦：End 后敌人 uid 可保留，但交战窗口关闭。
        /// </summary>
        public bool IsEngagementActive { get; private set; }

        /// <summary>敌方行动阶段报名与收尾之间为 true（ADR-0044 位移锁定窗口的第二轴）。</summary>
        public bool IsEnemyActionPhaseActive { get; private set; }

        /// <summary>结算窗口内挂起的效果位移（ADR-0044）；窗口关闭且非终局时由收尾锚点排空。</summary>
        public bool HasDeferredBoardMotions
        {
            get { return mDeferredBoardMotions.Count > 0; }
        }

        /// <summary>离开机关被击破后由「离开」技能置位；驱动 <c>IsNodeCleared</c>。</summary>
        public bool IsLeaveTrapBroken { get; private set; }

        /// <summary>本节点开局编入的真怪物总数（不含机关）；SetupNodeDeck 写入。</summary>
        public int OpeningTrueMonsterCount { get; private set; }

        /// <summary>本节点已击破的开局真怪物数（仅计开局编入 UID）。</summary>
        public int DefeatedTrueMonsterCount { get; private set; }

        /// <summary>本节点是否已击破开局编入的层主（Boss Counter）。</summary>
        public bool IsOpeningBossDefeated { get; private set; }

        /// <summary>离开机关是否已洗入本节点战斗卡组（幂等）。</summary>
        public bool IsLeaveTrapInserted { get; private set; }

        protected override void OnInit()
        {
            if (EngagedEnemyUid == null)
            {
                EngagedEnemyUid = new BindableProperty<int>(0);
            }
        }

        public void Reset()
        {
            EngagedEnemyUid.Value = 0;
            IsEngagementActive = false;
            IsEnemyActionPhaseActive = false;
            mDeferredBoardMotions.Clear();
            ResetLeaveTrapProgress();
        }

        /// <summary>每战斗节点 Setup 时重置离开机关进度（含清关向标志），避免跨节点粘连。</summary>
        public void ResetLeaveTrapProgress()
        {
            // ADR-0044：节点重置一并清掉残留的位移锁定窗口与挂起队列，避免跨节点粘连。
            IsEnemyActionPhaseActive = false;
            mDeferredBoardMotions.Clear();
            IsLeaveTrapBroken = false;
            OpeningTrueMonsterCount = 0;
            DefeatedTrueMonsterCount = 0;
            IsOpeningBossDefeated = false;
            IsLeaveTrapInserted = false;
            mOpeningTrueMonsterUids.Clear();
            mOpeningBossMonsterUids.Clear();
        }

        public void SetEngagedEnemy(int monsterUid)
        {
            EngagedEnemyUid.Value = monsterUid;
        }

        public void SetEngagementActive(bool active)
        {
            IsEngagementActive = active;
        }

        public void SetEnemyActionPhaseActive(bool active)
        {
            IsEnemyActionPhaseActive = active;
        }

        public void EnqueueDeferredBoardMotion(DeferredBoardMotion motion)
        {
            if (motion != null)
            {
                mDeferredBoardMotions.Add(motion);
            }
        }

        /// <summary>按挂起顺序倒入 <paramref name="buffer"/> 并清空队列；返回条数。</summary>
        public int DrainDeferredBoardMotions(List<DeferredBoardMotion> buffer)
        {
            var count = mDeferredBoardMotions.Count;
            if (buffer != null)
            {
                buffer.AddRange(mDeferredBoardMotions);
            }

            mDeferredBoardMotions.Clear();
            return count;
        }

        public void ClearDeferredBoardMotions()
        {
            mDeferredBoardMotions.Clear();
        }

        public void MarkLeaveTrapBroken()
        {
            IsLeaveTrapBroken = true;
        }

        public void RegisterOpeningTrueMonster(int cardUid, bool isBoss = false)
        {
            if (cardUid <= 0 || !mOpeningTrueMonsterUids.Add(cardUid))
            {
                return;
            }

            OpeningTrueMonsterCount = mOpeningTrueMonsterUids.Count;
            if (isBoss)
            {
                mOpeningBossMonsterUids.Add(cardUid);
            }
        }

        /// <summary>仅开局编入的真怪击破计入进度；返回是否计入。</summary>
        public bool TryRecordOpeningTrueMonsterDefeat(int cardUid)
        {
            if (cardUid <= 0 || !mOpeningTrueMonsterUids.Contains(cardUid))
            {
                return false;
            }

            DefeatedTrueMonsterCount++;
            return true;
        }

        /// <summary>仅开局编入且带 Boss Counter 的真怪击破计入层主进度。</summary>
        public bool TryRecordOpeningBossDefeat(int cardUid)
        {
            if (cardUid <= 0 || !mOpeningBossMonsterUids.Contains(cardUid))
            {
                return false;
            }

            IsOpeningBossDefeated = true;
            return true;
        }

        public void MarkLeaveTrapInserted()
        {
            IsLeaveTrapInserted = true;
        }

        /// <summary>
        /// 层主房（<paramref name="bossRoom"/>）：须已击破开局层主。
        /// 普通战斗房在 <c>BuildNodeDeckOptions</c> 开局编入，此处恒 false。
        /// </summary>
        public bool ShouldInsertLeaveTrap(bool bossRoom)
        {
            if (IsLeaveTrapInserted || !bossRoom)
            {
                return false;
            }

            return IsOpeningBossDefeated;
        }

        /// <summary>
        /// 本节点开局编入的真怪是否已全部清完（N=0 视为已清完）。
        /// 清关时据此决定是否自动兑金场上残留道具卡（ADR-0026）。
        /// 状态判据：开局真怪只要仍在册且存活（HP&gt;0 且不在 Graveyard/Removed）即视为未清完。
        /// 覆盖被机关效果（如滚石）移除而非击杀的情形——怪已离场但击杀计数缺失，
        /// 会导致「场上无怪、残留道具却不兑金」的偶发失效。
        /// <paramref name="registry"/> 为空时回退旧击杀计数口径。
        /// </summary>
        public bool AreAllOpeningMonstersDefeated(CardRegistry registry)
        {
            if (OpeningTrueMonsterCount <= 0)
            {
                return true;
            }

            if (registry == null)
            {
                return DefeatedTrueMonsterCount >= OpeningTrueMonsterCount;
            }

            foreach (var uid in mOpeningTrueMonsterUids)
            {
                CardInstance card;
                if (!registry.TryGet(uid, out card))
                {
                    // 已不在注册表（被移除/销毁）→ 视为离场。
                    continue;
                }

                if (!IsCardGoneFromPlay(card))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsCardGoneFromPlay(CardInstance card)
        {
            if (card == null
                || card.Zone.Value == ZoneId.Graveyard
                || card.Zone.Value == ZoneId.Removed)
            {
                return true;
            }

            return card.Stats.GetBase(StatId.Hp) <= 0f;
        }
    }
}
