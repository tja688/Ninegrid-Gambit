using System.Collections.Generic;
using QFramework;

namespace NineGrid.Core
{
    /// <summary>
    /// 追踪玩家当前交战敌人与交战窗口是否打开。
    /// EngagedEnemyUid 供 UntilEnemyChanges；IsEngagementActive 供 OnBattle 门禁（#78 / ADR-0012）。
    /// IsLeaveTrapBroken 即战斗房清关标志（#113 / ADR-0026；<c>IDeckSystem.IsNodeCleared</c> 读此位）。
    /// 开局真怪 N / 击破进度 / 离开机关是否已洗入供 #112 ⌈N/2⌉ 插入（ADR-0026）。
    /// </summary>
    public sealed class BattleContextModel : AbstractModel
    {
        private readonly HashSet<int> mOpeningTrueMonsterUids = new HashSet<int>();

        public BindableProperty<int> EngagedEnemyUid { get; private set; }

        /// <summary>
        /// BeginPlayerMonsterEngagement 与 EndBattleScopeCleanup 之间为 true。
        /// 与 EngagedEnemyUid 解耦：End 后敌人 uid 可保留，但交战窗口关闭。
        /// </summary>
        public bool IsEngagementActive { get; private set; }

        /// <summary>离开机关被击破后由「离开」技能置位；驱动 <c>IsNodeCleared</c>。</summary>
        public bool IsLeaveTrapBroken { get; private set; }

        /// <summary>本节点开局编入的真怪物总数（不含机关）；SetupNodeDeck 写入。</summary>
        public int OpeningTrueMonsterCount { get; private set; }

        /// <summary>本节点已击破的开局真怪物数（仅计开局编入 UID）。</summary>
        public int DefeatedTrueMonsterCount { get; private set; }

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
            ResetLeaveTrapProgress();
        }

        /// <summary>每战斗节点 Setup 时重置离开机关进度（含清关向标志），避免跨节点粘连。</summary>
        public void ResetLeaveTrapProgress()
        {
            IsLeaveTrapBroken = false;
            OpeningTrueMonsterCount = 0;
            DefeatedTrueMonsterCount = 0;
            IsLeaveTrapInserted = false;
            mOpeningTrueMonsterUids.Clear();
        }

        public void SetEngagedEnemy(int monsterUid)
        {
            EngagedEnemyUid.Value = monsterUid;
        }

        public void SetEngagementActive(bool active)
        {
            IsEngagementActive = active;
        }

        public void MarkLeaveTrapBroken()
        {
            IsLeaveTrapBroken = true;
        }

        public void RegisterOpeningTrueMonster(int cardUid)
        {
            if (cardUid <= 0 || !mOpeningTrueMonsterUids.Add(cardUid))
            {
                return;
            }

            OpeningTrueMonsterCount = mOpeningTrueMonsterUids.Count;
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

        public void MarkLeaveTrapInserted()
        {
            IsLeaveTrapInserted = true;
        }

        /// <summary>⌈N/2⌉；N=0 时永不触发（避免无怪即出门）。</summary>
        public bool ShouldInsertLeaveTrap()
        {
            if (IsLeaveTrapInserted || OpeningTrueMonsterCount <= 0)
            {
                return false;
            }

            var threshold = (OpeningTrueMonsterCount + 1) / 2;
            return DefeatedTrueMonsterCount >= threshold;
        }
    }
}
