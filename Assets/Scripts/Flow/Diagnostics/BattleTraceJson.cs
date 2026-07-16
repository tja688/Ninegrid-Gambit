using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NineGrid.Flow.Diagnostics
{
    /// <summary>
    /// BattleTrace 手写 JSON（避免 JsonUtility 对 List/嵌套的限制，零第三方依赖）。
    /// </summary>
    public static class BattleTraceJson
    {
        public static string Serialize(BattleTraceSession session)
        {
            if (session == null)
            {
                return "{}";
            }

            var sb = new StringBuilder(2048);
            sb.Append('{');
            AppendNumber(sb, "schemaVersion", session.schemaVersion);
            sb.Append(',');
            AppendString(sb, "seed", session.seed ?? "0");
            sb.Append(',');
            AppendString(sb, "sessionId", session.sessionId ?? string.Empty);
            sb.Append(',');
            AppendString(sb, "runTag", session.runTag ?? string.Empty);
            sb.Append(',');
            AppendString(sb, "runTagNote", session.runTagNote ?? string.Empty);
            sb.Append(',');
            sb.Append("\"ops\":[");
            var ops = session.ops;
            if (ops != null)
            {
                for (var i = 0; i < ops.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(',');
                    }

                    AppendOp(sb, ops[i]);
                }
            }

            sb.Append("]}");
            return sb.ToString();
        }

        private static void AppendOp(StringBuilder sb, BattleTraceOp op)
        {
            if (op == null)
            {
                sb.Append("null");
                return;
            }

            sb.Append('{');
            AppendNumber(sb, "opIndex", op.opIndex);
            sb.Append(',');
            AppendString(sb, "opKind", op.opKind);
            sb.Append(',');
            AppendString(sb, "reason", op.reason);
            sb.Append(',');
            AppendString(sb, "apiPath", op.apiPath);
            sb.Append(',');
            AppendString(sb, "phaseBefore", op.phaseBefore);
            sb.Append(',');
            AppendString(sb, "phaseAfter", op.phaseAfter);
            sb.Append(',');
            sb.Append("\"attacker\":");
            AppendCard(sb, op.attacker);
            sb.Append(',');
            sb.Append("\"target\":");
            AppendCard(sb, op.target);
            sb.Append(',');
            AppendNumber(sb, "eventStartIndex", op.eventStartIndex);
            sb.Append(',');
            AppendNumber(sb, "eventEndIndex", op.eventEndIndex);
            sb.Append(',');
            sb.Append("\"events\":[");
            AppendEvents(sb, op.events);
            sb.Append("],");
            sb.Append("\"presentation\":");
            AppendPresentation(sb, op.presentation);
            sb.Append(',');
            sb.Append("\"verdictHints\":");
            AppendVerdict(sb, op.verdictHints);
            sb.Append('}');
        }

        private static void AppendCard(StringBuilder sb, BattleTraceCardSnap card)
        {
            if (card == null)
            {
                sb.Append("null");
                return;
            }

            sb.Append('{');
            AppendNumber(sb, "uid", card.uid);
            sb.Append(',');
            AppendString(sb, "defId", card.defId);
            sb.Append(',');
            AppendString(sb, "kind", card.kind);
            sb.Append(',');
            AppendNumber(sb, "atk", card.atk);
            sb.Append(',');
            AppendNumber(sb, "hp", card.hp);
            sb.Append(',');
            AppendNumber(sb, "armor", card.armor);
            sb.Append('}');
        }

        private static void AppendEvents(StringBuilder sb, List<BattleTraceEventRow> events)
        {
            if (events == null)
            {
                return;
            }

            for (var i = 0; i < events.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                var e = events[i];
                if (e == null)
                {
                    sb.Append("null");
                    continue;
                }

                sb.Append('{');
                AppendNumber(sb, "sequence", e.sequence);
                sb.Append(',');
                AppendString(sb, "type", e.type);
                sb.Append(',');
                AppendString(sb, "actionName", e.actionName);
                sb.Append(',');
                AppendNumber(sb, "actorUid", e.actorUid);
                sb.Append(',');
                AppendNumber(sb, "targetUid", e.targetUid);
                sb.Append(',');
                AppendNumber(sb, "cardUid", e.cardUid);
                sb.Append(',');
                AppendNumber(sb, "amount", e.amount);
                sb.Append(',');
                AppendNumber(sb, "delta", e.delta);
                sb.Append(',');
                AppendNumber(sb, "remainingHp", e.remainingHp);
                sb.Append(',');
                AppendNumber(sb, "remainingArmor", e.remainingArmor);
                sb.Append(',');
                AppendString(sb, "sourceDefId", e.sourceDefId);
                sb.Append(',');
                AppendString(sb, "cause", e.cause);
                sb.Append(',');
                AppendString(sb, "summary", e.summary);
                sb.Append('}');
            }
        }

        private static void AppendPresentation(StringBuilder sb, BattleTracePresentation p)
        {
            if (p == null)
            {
                sb.Append("null");
                return;
            }

            sb.Append('{');
            AppendBool(sb, "accepted", p.accepted);
            sb.Append(',');
            AppendNumber(sb, "damageAmount", p.damageAmount);
            sb.Append(',');
            AppendBool(sb, "targetKilled", p.targetKilled);
            sb.Append(',');
            AppendBool(sb, "avatarDefeated", p.avatarDefeated);
            sb.Append(',');
            AppendBool(sb, "nodeClearedOrRewardPhase", p.nodeClearedOrRewardPhase);
            sb.Append(',');
            AppendString(sb, "rejectReason", p.rejectReason);
            sb.Append('}');
        }

        private static void AppendVerdict(StringBuilder sb, BattleTraceVerdictHints v)
        {
            if (v == null)
            {
                sb.Append("null");
                return;
            }

            sb.Append('{');
            AppendBool(sb, "avatarDefeated", v.avatarDefeated);
            sb.Append(',');
            AppendBool(sb, "targetKilled", v.targetKilled);
            sb.Append(',');
            AppendNumber(sb, "extraDamageDealtCount", v.extraDamageDealtCount);
            sb.Append(',');
            sb.Append("\"effectTriggeredIds\":[");
            if (v.effectTriggeredIds != null)
            {
                for (var i = 0; i < v.effectTriggeredIds.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(',');
                    }

                    sb.Append('"');
                    sb.Append(Escape(v.effectTriggeredIds[i]));
                    sb.Append('"');
                }
            }

            sb.Append("]}");
        }

        private static void AppendString(StringBuilder sb, string key, string value)
        {
            sb.Append('"');
            sb.Append(key);
            sb.Append("\":\"");
            sb.Append(Escape(value));
            sb.Append('"');
        }

        private static void AppendNumber(StringBuilder sb, string key, long value)
        {
            sb.Append('"');
            sb.Append(key);
            sb.Append("\":");
            sb.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendNumber(StringBuilder sb, string key, int value)
        {
            sb.Append('"');
            sb.Append(key);
            sb.Append("\":");
            sb.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendBool(StringBuilder sb, string key, bool value)
        {
            sb.Append('"');
            sb.Append(key);
            sb.Append("\":");
            sb.Append(value ? "true" : "false");
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
    }
}
