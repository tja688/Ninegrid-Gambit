using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Flow.Diagnostics
{
    /// <summary>
    /// #206 节奏/翻面诊断旁路：以 EventLog 游标扫描**全部**互动链（交战/拾取/点空/翻开/敌方行动分拍），
    /// 把 CardFaceChanged / ActionCountdownChanged / CardRhythmFireOpened / EnemyActionResolved /
    /// EffectTriggered 写入 FlowTrace（Rhythm / CombatSummary 轨）。
    /// 触发时机：每次 <see cref="Evt_PresentationBatchOpened"/> 即时扫描 + Update 兜底追扫。
    /// 只读诊断，失败一律吞掉，不影响游戏路径。
    /// </summary>
    public sealed class RhythmFaceFlowTraceBinder : MonoBehaviour
    {
        private static RhythmFaceFlowTraceBinder sInstance;
        private IUnRegister mEventUnRegister;
        private bool mRetrySubscriptionPending;
        private int mCursor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            sInstance = null;
        }

        /// <summary>确保存在且已订阅；架构暂不可用时延后重试。</summary>
        public static void EnsureInstalled()
        {
            if (sInstance == null)
            {
                var existing = FindFirstObjectByType<RhythmFaceFlowTraceBinder>();
                if (existing != null)
                {
                    sInstance = existing;
                }
                else
                {
                    var host = new GameObject(nameof(RhythmFaceFlowTraceBinder));
                    sInstance = host.AddComponent<RhythmFaceFlowTraceBinder>();
                }
            }

            sInstance.EnsureSubscribed();
        }

        private void Awake()
        {
            if (sInstance != null && sInstance != this)
            {
                Destroy(gameObject);
                return;
            }

            sInstance = this;
            EnsureSubscribed();
        }

        private void Update()
        {
            if (mRetrySubscriptionPending)
            {
                EnsureSubscribed();
            }

            // 兜底追扫：拒收/锁定链等未开批路径的事件也不遗漏。
            ScanNewEntries();
        }

        private void OnDestroy()
        {
            mEventUnRegister?.UnRegister();
            mEventUnRegister = null;
            if (sInstance == this)
            {
                sInstance = null;
            }
        }

        private void EnsureSubscribed()
        {
            if (mEventUnRegister != null)
            {
                mRetrySubscriptionPending = false;
                return;
            }

            var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            if (arch == null)
            {
                mRetrySubscriptionPending = true;
                return;
            }

            mRetrySubscriptionPending = false;
            mEventUnRegister = arch.RegisterEvent<Evt_PresentationBatchOpened>(OnBatchOpened);
        }

        private void OnBatchOpened(Evt_PresentationBatchOpened e)
        {
            ScanNewEntries();
        }

        private void ScanNewEntries()
        {
            try
            {
                if (!FlowTraceRecorder.Enabled)
                {
                    return;
                }

                var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
                var pipeline = arch?.GetSystem<IActionPipelineSystem>();
                if (pipeline?.EventLog == null)
                {
                    return;
                }

                var entries = pipeline.EventLog.Entries;
                if (entries.Count < mCursor)
                {
                    // EventLog 被清空（会话重开）：从头重扫。
                    mCursor = 0;
                }

                if (entries.Count == mCursor)
                {
                    return;
                }

                var registry = arch.GetModel<CardRegistry>();
                for (var i = mCursor; i < entries.Count; i++)
                {
                    RecordEntry(entries[i], registry);
                }

                mCursor = entries.Count;
            }
            catch
            {
                // 诊断失败不阻塞主线
            }
        }

        private static void RecordEntry(CoreGameEvent e, CardRegistry registry)
        {
            if (e == null)
            {
                return;
            }

            switch (e.Type)
            {
                case CoreEventType.CardFaceChanged:
                    Record(FlowTraceCategory.Rhythm, FlowTraceNames.CardFaceChanged, new Dictionary<string, string>
                    {
                        { "uid", e.CardUid.ToString() },
                        { "defId", ResolveDefId(registry, e.CardUid) },
                        { "faceUp", e.ResultValue != 0 ? "1" : "0" },
                        { "action", e.ActionName ?? string.Empty },
                        { "sourceDefId", e.SourceDefId ?? string.Empty },
                        { "cause", e.Cause ?? string.Empty },
                    });
                    break;
                case CoreEventType.ActionCountdownChanged:
                    Record(FlowTraceCategory.Rhythm, FlowTraceNames.ActionCountdownChanged, new Dictionary<string, string>
                    {
                        { "uid", e.CardUid.ToString() },
                        { "defId", ResolveDefId(registry, e.CardUid) },
                        { "delta", e.Delta.ToString() },
                        { "remaining", e.ResultValue.ToString() },
                    });
                    break;
                case CoreEventType.CardRhythmFireOpened:
                    Record(FlowTraceCategory.Rhythm, FlowTraceNames.RhythmFireOpened, new Dictionary<string, string>
                    {
                        { "uid", e.CardUid.ToString() },
                        { "defId", ResolveDefId(registry, e.CardUid) },
                    });
                    break;
                case CoreEventType.EnemyActionResolved:
                    Record(FlowTraceCategory.Rhythm, FlowTraceNames.EnemyActionVerdict, new Dictionary<string, string>
                    {
                        { "uid", e.CardUid.ToString() },
                        { "defId", e.SourceDefId ?? string.Empty },
                        { "verdict", e.Message ?? string.Empty },
                        { "detail", e.Cause ?? string.Empty },
                        { "patternFires", e.Amount.ToString() },
                        { "remaining", e.ResultValue.ToString() },
                    });
                    break;
                case CoreEventType.EffectTriggered:
                    // 全链路 EffectTriggered（原 CombatHit-only 记录已移到此处，见 #206）。
                    Record(FlowTraceCategory.CombatSummary, FlowTraceNames.EffectTriggered, new Dictionary<string, string>
                    {
                        { "sourceDefId", e.SourceDefId ?? string.Empty },
                        { "cardUid", e.CardUid.ToString() },
                        { "message", e.Message ?? string.Empty },
                    });
                    break;
                case CoreEventType.RelicGranted:
                    // 遗物挂载可观测：入栏事实（route 区分授予 / 自愈重挂）。
                    Record(FlowTraceCategory.CombatSummary, FlowTraceNames.RelicGranted, new Dictionary<string, string>
                    {
                        { "defId", e.Message ?? string.Empty },
                        { "route", string.IsNullOrEmpty(e.Cause) ? "grant" : e.Cause },
                        { "action", e.ActionName ?? string.Empty },
                    });
                    break;
                case CoreEventType.RelicEffectMountAudited:
                    // 遗物挂载审计：declared/implemented/mounted/modifiers 四数核对，
                    // 「有遗物无效果」可直接从 corelog 判定缺在哪一层。
                    Record(FlowTraceCategory.CombatSummary, FlowTraceNames.RelicMountAudit, new Dictionary<string, string>
                    {
                        { "defId", e.SourceDefId ?? string.Empty },
                        { "mounted", e.Amount.ToString() },
                        { "declared", e.Delta.ToString() },
                        { "modifiers", e.ResultValue.ToString() },
                        { "detail", e.Message ?? string.Empty },
                    });
                    break;
                case CoreEventType.PipelineFaultContained:
                    // ADR-0047 熔断事实入 corelog（此前只有 Console 报错，release 局无痕）。
                    Record(FlowTraceCategory.CombatSummary, FlowTraceNames.PipelineFault, new Dictionary<string, string>
                    {
                        { "actionName", e.ActionName ?? string.Empty },
                        { "depth", e.Amount.ToString() },
                        { "detail", e.Message ?? string.Empty },
                    });
                    break;
            }
        }

        private static void Record(string category, string name, Dictionary<string, string> payload)
        {
            FlowTraceRecorder.BeginSessionIfNeeded();
            FlowTraceRecorder.Record(
                category,
                name,
                payload,
                refBattleOpIndex: BattleTraceRecorder.LastOpIndex);
        }

        private static string ResolveDefId(CardRegistry registry, int uid)
        {
            if (registry == null || uid <= 0)
            {
                return string.Empty;
            }

            CardInstance card;
            return registry.TryGet(uid, out card) && card != null
                ? card.DefId ?? string.Empty
                : string.Empty;
        }
    }
}
