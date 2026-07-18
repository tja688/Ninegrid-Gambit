using System;
using System.Collections.Generic;
using System.Text;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Flow.Diagnostics
{
    /// <summary>
    /// 统一门禁旁路 BattleTrace 记录器。打点失败一律吞掉，不影响结算路径。
    /// </summary>
    public static class BattleTraceRecorder
    {
        private static BattleTraceSession sSession;
        private static bool sEnabled =
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            true;
#else
            false;
#endif

#if UNITY_EDITOR
        static BattleTraceRecorder()
        {
            // 首次触达即从 EditorPrefs 套回，不依赖 InitializeOnLoad 执行顺序。
            sEnabled = DiagTraceExportPreferences.GetTrackRecording(DiagTraceTrack.Battle);
        }
#endif

        public static bool Enabled
        {
            get => sEnabled;
            set
            {
#if UNITY_EDITOR
                DiagTraceExportPreferences.SetTrackRecording(DiagTraceTrack.Battle, value);
#else
                sEnabled = value;
#endif
            }
        }

        /// <summary>由偏好系统写入，避免与 <see cref="Enabled"/> setter 互相递归。</summary>
        internal static void SetEnabledFromPreferences(bool enabled)
        {
            sEnabled = enabled;
        }

        public static BattleTraceSession CurrentSession => sSession;

        public static bool HasOps =>
            sSession != null && sSession.ops != null && sSession.ops.Count > 0;

        /// <summary>
        /// 进入 Play 时复位「本局已导出」标记，供 Editor 钩子调用。
        /// </summary>
        public static void NotifyEnteredPlayMode()
        {
            DiagTraceShared.NotifyEnteredPlayMode();
        }

        public static void Clear()
        {
            try
            {
                sSession = null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BattleTrace] Clear failed: " + ex.Message);
            }
        }

        /// <summary>
        /// 失败重开 / 再点开始：若已有上一局内容则先落盘，再强制新 sessionId，两边清空对齐。
        /// </summary>
        public static void RotateSessionForNewRun(ulong seed = 0UL)
        {
            try
            {
                var shouldRotate = HasOps
                    || FlowTraceRecorder.HasPriorRunMarker()
                    || PerfTraceRecorder.HasEvents
                    || RegistryTraceRecorder.HasEvents;
                if (!shouldRotate)
                {
                    BeginSessionIfNeeded(seed);
                    FlowTraceRecorder.BeginSessionIfNeeded(seed);
                    PerfTraceRecorder.BeginSessionIfNeeded(seed);
                    RegistryTraceRecorder.BeginSessionIfNeeded(seed);
                    return;
                }

                ExportBothNow(silentIfEmpty: true);
                Clear();
                FlowTraceRecorder.Clear();
                PerfTraceRecorder.Clear();
                RegistryTraceRecorder.Clear();
                DiagBeatClock.Reset();
                ChoreoTraceContext.Reset();
                DiagTraceShared.ForceNewSessionIdentity(seed);
                BeginSessionIfNeeded(seed);
                FlowTraceRecorder.BeginSessionIfNeeded(seed);
                PerfTraceRecorder.BeginSessionIfNeeded(seed);
                RegistryTraceRecorder.BeginSessionIfNeeded(seed);
                Debug.Log(
                    "[BattleTrace] 新开局已轮转诊断会话 sessionId="
                    + DiagTraceShared.CurrentSessionId
                    + " seed="
                    + DiagTraceShared.CurrentSeed);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BattleTrace] RotateSessionForNewRun failed: " + ex.Message);
            }
        }

        public static void BeginSessionIfNeeded(ulong seed = 0UL)
        {
            if (!sEnabled)
            {
                return;
            }

            try
            {
                DiagTraceShared.EnsureSessionIdentity(seed);
                if (sSession != null)
                {
                    sSession.sessionId = DiagTraceShared.CurrentSessionId;
                    sSession.seed = DiagTraceShared.CurrentSeed;
                    DiagTraceShared.StampSessionRunMetadata(out sSession.runTag, out sSession.runTagNote);
                    return;
                }

                sSession = new BattleTraceSession
                {
                    schemaVersion = 1,
                    seed = DiagTraceShared.CurrentSeed,
                    sessionId = DiagTraceShared.CurrentSessionId,
                    ops = new List<BattleTraceOp>(),
                };
                DiagTraceShared.StampSessionRunMetadata(out sSession.runTag, out sSession.runTagNote);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BattleTrace] BeginSession failed: " + ex.Message);
            }
        }

        public static void RecordOp(BattleTraceOp op)
        {
            if (!sEnabled || op == null)
            {
                return;
            }

            try
            {
                BeginSessionIfNeeded();
                if (sSession == null)
                {
                    return;
                }

                op.opIndex = sSession.ops.Count;
                sSession.ops.Add(op);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BattleTrace] RecordOp failed: " + ex.Message);
            }
        }

        /// <summary>
        /// 最近一条已记录 Op 的 opIndex；无会话返回 -1。
        /// </summary>
        public static int LastOpIndex
        {
            get
            {
                if (sSession == null || sSession.ops == null || sSession.ops.Count == 0)
                {
                    return -1;
                }

                return sSession.ops.Count - 1;
            }
        }

        public static BattleTraceCardSnap TryCaptureCard(int uid)
        {
            try
            {
                if (uid <= 0)
                {
                    return null;
                }

                var arch = NineGridArchitecture.Current;
                if (arch == null || !arch.GetModel<CardRegistry>().TryGet(uid, out var card) || card == null)
                {
                    return null;
                }

                var stats = arch.GetSystem<IStatSystem>();
                return new BattleTraceCardSnap
                {
                    uid = uid,
                    defId = card.DefId ?? string.Empty,
                    kind = card.Kind.ToString(),
                    atk = stats.GetEffectiveInt(card, StatId.Attack),
                    hp = stats.GetEffectiveInt(card, StatId.Hp),
                    armor = stats.GetEffectiveInt(card, StatId.Armor),
                };
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BattleTrace] TryCaptureCard failed: " + ex.Message);
                return null;
            }
        }

        public static List<BattleTraceEventRow> SliceEvents(int startIndex, int endIndex)
        {
            var rows = new List<BattleTraceEventRow>();
            try
            {
                var arch = NineGridArchitecture.Current;
                if (arch == null)
                {
                    return rows;
                }

                var entries = arch.GetSystem<IActionPipelineSystem>().EventLog.Entries;
                if (startIndex < 0)
                {
                    startIndex = 0;
                }

                if (endIndex > entries.Count)
                {
                    endIndex = entries.Count;
                }

                for (var i = startIndex; i < endIndex; i++)
                {
                    rows.Add(ToEventRow(entries[i]));
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BattleTrace] SliceEvents failed: " + ex.Message);
            }

            return rows;
        }

        public static BattleTraceEventRow ToEventRow(CoreGameEvent e)
        {
            var label = e.Type.ToString();
            try
            {
                if (PresentationEventMap.TryGet(e.Type, out var mapEntry) && mapEntry != null)
                {
                    label = mapEntry.Label;
                }
            }
            catch
            {
                // ignore map miss
            }

            var summary = "#" + e.Sequence + " " + label;
            if (e.CardUid != 0)
            {
                summary += " card=" + e.CardUid;
            }

            if (e.Amount != 0 || e.Delta != 0)
            {
                summary += " amount=" + e.Amount + " delta=" + e.Delta;
            }

            if (e.RemainingHp != 0 || e.RemainingArmor != 0)
            {
                summary += " hp=" + e.RemainingHp + " armor=" + e.RemainingArmor;
            }

            if (!string.IsNullOrEmpty(e.SourceDefId) || !string.IsNullOrEmpty(e.Cause))
            {
                summary += " source=" + e.SourceDefId + " cause=" + e.Cause;
            }

            if (!string.IsNullOrEmpty(e.Message))
            {
                summary += " " + e.Message;
            }

            return new BattleTraceEventRow
            {
                sequence = e.Sequence,
                type = e.Type.ToString(),
                actionName = e.ActionName ?? string.Empty,
                actorUid = e.ActorUid,
                targetUid = e.TargetUid,
                cardUid = e.CardUid,
                amount = e.Amount,
                delta = e.Delta,
                remainingHp = e.RemainingHp,
                remainingArmor = e.RemainingArmor,
                sourceDefId = e.SourceDefId ?? string.Empty,
                cause = e.Cause ?? string.Empty,
                summary = summary,
            };
        }

        public static BattleTraceVerdictHints BuildVerdictHints(
            IReadOnlyList<BattleTraceEventRow> events,
            bool targetKilled,
            bool avatarDefeated)
        {
            var hints = new BattleTraceVerdictHints
            {
                targetKilled = targetKilled,
                avatarDefeated = avatarDefeated,
                effectTriggeredIds = new List<string>(),
            };

            if (events == null)
            {
                return hints;
            }

            var damageCount = 0;
            for (var i = 0; i < events.Count; i++)
            {
                var row = events[i];
                if (row == null)
                {
                    continue;
                }

                if (row.type == nameof(CoreEventType.DamageDealt))
                {
                    damageCount++;
                }

                if (row.type == nameof(CoreEventType.EffectTriggered)
                    && !string.IsNullOrEmpty(row.sourceDefId)
                    && !hints.effectTriggeredIds.Contains(row.sourceDefId))
                {
                    hints.effectTriggeredIds.Add(row.sourceDefId);
                }
            }

            // 主伤害之外的额外 DamageDealt（效果触发等）
            hints.extraDamageDealtCount = Math.Max(0, damageCount - 1);
            return hints;
        }

        /// <summary>
        /// 导出当前局战斗日志。Editor → Assets/Notes/Logs/OtherLog/BattleLog。
        /// </summary>
        /// <param name="silentIfEmpty">无数据时不打 Warning。</param>
        /// <param name="automatic">自动落盘时受 <see cref="DiagTraceExportPreferences"/> 约束。</param>
        public static string ExportJson(bool silentIfEmpty = false, bool automatic = true)
        {
            try
            {
                if (!DiagTraceExportPreferences.ShouldWriteFile(DiagTraceTrack.Battle, automatic))
                {
                    return null;
                }

                if (sSession == null || sSession.ops == null || sSession.ops.Count == 0)
                {
                    if (!silentIfEmpty)
                    {
                        Debug.LogWarning("[BattleTrace] ExportJson: 无会话或无 ops。");
                    }

                    return null;
                }

                var json = BattleTraceJson.Serialize(sSession);
                var dir = DiagTraceShared.ResolveNotesDir("Logs/OtherLog/BattleLog");
                var fileName = DiagTraceShared.BuildFileName(
                    "battlelog",
                    sSession.sessionId,
                    sSession.seed);
                var path = DiagTraceShared.WriteUtf8File(dir, fileName, json);
                if (!string.IsNullOrEmpty(path))
                {
                    Debug.Log("[BattleTrace] Exported: " + path + "\n" + BuildTailSummary(sSession));
                }

                return path;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BattleTrace] ExportJson failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Play 结束时调用：有 ops 则导出，无数据则静默跳过。同一次退出只导出一次。
        /// </summary>
        public static string ExportJsonIfAny()
        {
            return ExportOnPlayExit("ExportJsonIfAny");
        }

        /// <summary>
        /// Play 退出导出入口（OnDestroy / ExitingPlayMode 共用，去重）。
        /// 同时尝试导出 FlowTrace。
        /// </summary>
        public static string ExportOnPlayExit(string source)
        {
            try
            {
                if (DiagTraceShared.AlreadyExportedThisPlayExit)
                {
                    return null;
                }

                if (!DiagTraceExportPreferences.AutoExportEnabled)
                {
                    return null;
                }

                string battlePath = null;
                if (sEnabled
                    && sSession != null
                    && sSession.ops != null
                    && sSession.ops.Count > 0)
                {
                    battlePath = ExportJson(automatic: true);
                    if (!string.IsNullOrEmpty(battlePath))
                    {
                        Debug.Log("[BattleTrace] Play 结束已导出战斗日志（" + source + "）：" + battlePath);
                    }
                }

                ChoreoTraceContext.AppendExportSummaryEvents();
                FlowTraceRecorder.ExportOnPlayExit(source);
                PerfTraceRecorder.ExportOnPlayExit(source);
                RegistryTraceRecorder.ExportJson(silentIfEmpty: true, automatic: true);
                DiagTraceShared.MarkExportedThisPlayExit();
                return battlePath;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BattleTrace] ExportOnPlayExit failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 立即导出四轨（Battle + CoreLog + PerfLog + RegistryLog）（DevKeys / 胜负落盘 / 重开轮转）。不受 Play 退出去重影响。
        /// </summary>
        /// <param name="automatic">false = 手动加记，不受自动落盘开关限制。</param>
        public static void ExportBothNow(bool silentIfEmpty = false, bool automatic = true)
        {
            try
            {
                ExportJson(silentIfEmpty, automatic);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BattleTrace] ExportBothNow battle: " + ex.Message);
            }

            try
            {
                FlowTraceRecorder.ExportJson(silentIfEmpty, automatic);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BattleTrace] ExportBothNow flow: " + ex.Message);
            }

            try
            {
                PerfTraceRecorder.ExportJson(silentIfEmpty, automatic);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BattleTrace] ExportBothNow perf: " + ex.Message);
            }

            try
            {
                RegistryTraceRecorder.ExportJson(silentIfEmpty, automatic);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BattleTrace] ExportBothNow registry: " + ex.Message);
            }
        }

        /// <summary>手动导出单轨（编辑器窗口 / DevTest 点对点加记）。</summary>
        public static string ExportTrackNow(DiagTraceTrack track, bool silentIfEmpty = false)
        {
            return track switch
            {
                DiagTraceTrack.Battle => ExportJson(silentIfEmpty, automatic: false),
                DiagTraceTrack.Core => FlowTraceRecorder.ExportJson(silentIfEmpty, automatic: false),
                DiagTraceTrack.Perf => PerfTraceRecorder.ExportJson(silentIfEmpty, automatic: false),
                DiagTraceTrack.Registry => RegistryTraceRecorder.ExportJson(silentIfEmpty, automatic: false),
                _ => null,
            };
        }

        public static string ResolveExportDirectory()
        {
            return DiagTraceShared.ResolveNotesDir("Logs/OtherLog/BattleLog");
        }

        public static string ConsumePendingReason(string fallback = "CombatHit")
        {
            try
            {
                var reason = CombatHitSink.PendingTraceReason;
                CombatHitSink.PendingTraceReason = null;
                return string.IsNullOrEmpty(reason) ? fallback : reason;
            }
            catch
            {
                return fallback;
            }
        }

        public static string CurrentPhaseName()
        {
            try
            {
                return NineGridArchitecture.Current.GetSystem<IPhaseSystem>().CurrentPhase.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string BuildTailSummary(BattleTraceSession session)
        {
            var sb = new StringBuilder();
            sb.Append("ops=").Append(session.ops.Count);
            var from = Math.Max(0, session.ops.Count - 2);
            for (var i = from; i < session.ops.Count; i++)
            {
                var op = session.ops[i];
                if (op == null)
                {
                    continue;
                }

                sb.Append(" | [#").Append(op.opIndex)
                    .Append(' ').Append(op.opKind)
                    .Append('/').Append(op.reason)
                    .Append(" dmg=").Append(op.presentation != null ? op.presentation.damageAmount : 0)
                    .Append(" kill=").Append(op.presentation != null && op.presentation.targetKilled)
                    .Append(" defeat=").Append(op.presentation != null && op.presentation.avatarDefeated)
                    .Append(']');
            }

            return sb.ToString();
        }
    }
}
