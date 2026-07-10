using System;
using System.Collections.Generic;
using System.IO;
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

        public static bool Enabled
        {
            get => sEnabled;
            set => sEnabled = value;
        }

        public static BattleTraceSession CurrentSession => sSession;

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

        public static void BeginSessionIfNeeded(ulong seed = 0UL)
        {
            if (!sEnabled)
            {
                return;
            }

            try
            {
                if (sSession != null)
                {
                    return;
                }

                if (seed == 0UL)
                {
                    seed = TryReadSeed();
                }

                sSession = new BattleTraceSession
                {
                    schemaVersion = 1,
                    seed = seed.ToString(),
                    sessionId = DateTime.Now.ToString("yyyyMMdd-HHmmss"),
                    ops = new List<BattleTraceOp>(),
                };
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

        public static string ExportJson()
        {
            try
            {
                if (sSession == null || sSession.ops == null || sSession.ops.Count == 0)
                {
                    Debug.LogWarning("[BattleTrace] ExportJson: 无会话或无 ops。");
                    return null;
                }

                var json = BattleTraceJson.Serialize(sSession);
                var dir = Path.Combine(Application.persistentDataPath, "BattleTraces");
                Directory.CreateDirectory(dir);
                var fileName = "trace-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json";
                var path = Path.Combine(dir, fileName);
                File.WriteAllText(path, json, Encoding.UTF8);

                var summary = BuildTailSummary(sSession);
                Debug.Log("[BattleTrace] Exported: " + path + "\n" + summary);
                return path;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BattleTrace] ExportJson failed: " + ex.Message);
                return null;
            }
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

        private static ulong TryReadSeed()
        {
            try
            {
                return NineGridArchitecture.Current.GetModel<RunModel>().Seed.Value;
            }
            catch
            {
                return 0UL;
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
