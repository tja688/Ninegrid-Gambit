using NineGrid.Core;
using NineGrid.Core.Systems;
using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 本局结算统计（完结结算面板数据源）：只读扫描 Core EventLog 游标，
    /// 累计击败怪物数 / 玩家损失血量 / 使用道具卡数，并记录开局起始时刻。
    /// 只读旁路，不发 Core 指令、不参与规则；读档恢复后统计从恢复点重新累计。
    /// 生命周期：<see cref="HandleRunStarted"/> 在 GameFlowOrchestrator.Start 清零重开。
    /// </summary>
    public sealed class RunRecapTracker : MonoBehaviour
    {
        private static RunRecapTracker sInstance;
        private static int sMonstersKilled;
        private static int sHpLost;
        private static int sItemCardsUsed;
        private static float sRunStartRealtime = -1f;

        private int mCursor;

        public static int MonstersKilled => sMonstersKilled;
        public static int HpLost => sHpLost;
        public static int ItemCardsUsed => sItemCardsUsed;

        public static float ElapsedSeconds =>
            sRunStartRealtime >= 0f
                ? Mathf.Max(0f, Time.realtimeSinceStartup - sRunStartRealtime)
                : 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            sInstance = null;
            sMonstersKilled = 0;
            sHpLost = 0;
            sItemCardsUsed = 0;
            sRunStartRealtime = -1f;
        }

        /// <summary>run 开局（正式 / QuickTest / 教学 / 读档恢复）：清零并重开计时。</summary>
        public static void HandleRunStarted()
        {
            sMonstersKilled = 0;
            sHpLost = 0;
            sItemCardsUsed = 0;
            sRunStartRealtime = Time.realtimeSinceStartup;
            EnsureInstalled();
            if (sInstance != null)
            {
                // EventLog 会随 Bootstrap 清空；游标同步归零避免漏扫首批。
                sInstance.mCursor = 0;
            }
        }

        /// <summary>结算面板取数前显式追扫一遍，保证末批事件计入。</summary>
        public static void ScanNow()
        {
            sInstance?.ScanNewEntries();
        }

        /// <summary>「X分X秒」时长文案。</summary>
        public static string FormatElapsed()
        {
            var total = Mathf.FloorToInt(ElapsedSeconds);
            var minutes = total / 60;
            var seconds = total % 60;
            return string.Format(
                NineGrid.Core.Localization.L10n.Tr("summary.duration_format", "{0} 分 {1} 秒"),
                minutes,
                seconds);
        }

        private static void EnsureInstalled()
        {
            if (sInstance != null)
            {
                return;
            }

            var existing = FindFirstObjectByType<RunRecapTracker>();
            if (existing != null)
            {
                sInstance = existing;
                return;
            }

            var host = new GameObject(nameof(RunRecapTracker));
            sInstance = host.AddComponent<RunRecapTracker>();
        }

        private void Awake()
        {
            if (sInstance != null && sInstance != this)
            {
                Destroy(gameObject);
                return;
            }

            sInstance = this;
        }

        private void OnDestroy()
        {
            if (sInstance == this)
            {
                sInstance = null;
            }
        }

        private void Update()
        {
            ScanNewEntries();
        }

        private void ScanNewEntries()
        {
            try
            {
                var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
                var pipeline = arch?.GetSystem<IActionPipelineSystem>();
                if (pipeline?.EventLog == null)
                {
                    return;
                }

                var entries = pipeline.EventLog.Entries;
                if (entries.Count < mCursor)
                {
                    // EventLog 被清空（会话重开）：从头重扫，计数不清（由 HandleRunStarted 管）。
                    mCursor = 0;
                }

                if (entries.Count == mCursor)
                {
                    return;
                }

                var registry = arch.GetModel<CardRegistry>();
                for (var i = mCursor; i < entries.Count; i++)
                {
                    Accumulate(entries[i], registry);
                }

                mCursor = entries.Count;
            }
            catch
            {
                // 统计失败不阻塞主线。
            }
        }

        private static void Accumulate(CoreGameEvent e, CardRegistry registry)
        {
            if (e == null)
            {
                return;
            }

            switch (e.Type)
            {
                case CoreEventType.CardKilled:
                    if (IsKind(registry, e.CardUid, CardKind.Monster))
                    {
                        sMonstersKilled++;
                    }

                    break;
                case CoreEventType.HpChanged:
                    if (e.Delta < 0 && IsKind(registry, e.CardUid, CardKind.Avatar))
                    {
                        sHpLost += -e.Delta;
                    }

                    break;
                case CoreEventType.ItemUsed:
                    // 存档恢复重放（cause=replay）不重复计数。
                    if (!string.Equals(e.Cause, "replay", System.StringComparison.Ordinal))
                    {
                        sItemCardsUsed++;
                    }

                    break;
            }
        }

        private static bool IsKind(CardRegistry registry, int uid, CardKind kind)
        {
            if (registry == null || uid <= 0)
            {
                return false;
            }

            CardInstance card;
            return registry.TryGet(uid, out card) && card != null && card.Kind == kind;
        }
    }
}
