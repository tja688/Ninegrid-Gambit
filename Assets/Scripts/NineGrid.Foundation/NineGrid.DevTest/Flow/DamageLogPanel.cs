#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Flow.Diagnostics;
using UnityEngine;

namespace NineGrid.DevTest.Flow
{
    public sealed class DamageLogPanel : MonoBehaviour
    {
        private bool mVisible;
        private Vector2 mScrollPos;
        private readonly HashSet<int> mExpanded = new HashSet<int>();

        private GUIStyle mBoxStyle;
        private GUIStyle mRowBtnStyle;
        private GUIStyle mDamageStyle;
        private GUIStyle mHealStyle;
        private GUIStyle mKillStyle;
        private GUIStyle mSmallStyle;
        private GUIStyle mEventBgStyle;
        private bool mStylesBuilt;

        private const float RowH = 24f;
        private const float SubRowH = 17f;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
                mVisible = !mVisible;
        }

        private void OnGUI()
        {
            if (!mVisible) return;
            BuildStylesIfNeeded();

            float panelW = 500f;
            float panelH = Mathf.Min(Screen.height - 80, 700);
            var windowRect = new Rect(Screen.width - panelW - 10, 40, panelW, panelH);
            GUI.Box(windowRect, "伤害日志 (F1 关闭)");

            var innerRect = new Rect(10, 28, panelW - 20, panelH - 38);
            GUI.BeginGroup(innerRect);

            var session = BattleTraceRecorder.CurrentSession;
            if (session == null || session.ops == null || session.ops.Count == 0)
            {
                GUI.Label(new Rect(0, 4, innerRect.width, 24), "暂无战斗数据，开始一局战斗后自动记录。", mSmallStyle);
            }
            else
            {
                var arch = NineGridArchitecture.Current;
                var contentSys = arch != null ? arch.GetSystem<IContentSystem>() : null;
                var catalog = contentSys != null && contentSys.HasCatalog ? contentSys.Catalog : null;

                var ops = session.ops;
                float contentH = CalcTotalHeight(ops);
                float viewW = innerRect.width - 16;
                var viewRect = new Rect(0, 0, viewW, contentH);
                mScrollPos = GUI.BeginScrollView(new Rect(0, 0, innerRect.width, innerRect.height), mScrollPos, viewRect);

                float y = 0;
                for (int i = 0; i < ops.Count; i++)
                {
                    var op = ops[i];
                    if (op == null) continue;
                    y = DrawOp(viewW, y, op, catalog, i);
                }

                GUI.EndScrollView();
            }

            GUI.EndGroup();
        }

        private float DrawOp(float width, float y, BattleTraceOp op, GameContentCatalog catalog, int index)
        {
            bool expanded = mExpanded.Contains(index);
            string attackerName = ResolveDisplayName(catalog, op.attacker?.defId);
            string targetName = ResolveDisplayName(catalog, op.target?.defId);
            int dmg = op.presentation != null ? op.presentation.damageAmount : 0;

            string headerText = string.Format("#{0}  {1}  |  {2} → {3}  |  伤害:{4}  {5}",
                op.opIndex,
                FormatReason(op.reason),
                attackerName,
                targetName,
                dmg,
                expanded ? "▲" : "▼");

            float totalH = expanded ? CalcOpExpandedHeight(op) : RowH;

            // Full-row background box
            GUI.Box(new Rect(0, y, width, totalH), GUIContent.none, mBoxStyle);

            // Clickable header row
            if (GUI.Button(new Rect(2, y + 1, width - 4, RowH - 2), headerText, mRowBtnStyle))
            {
                if (expanded) mExpanded.Remove(index);
                else mExpanded.Add(index);
            }

            if (expanded)
            {
                float ey = y + RowH;
                float eventAreaH = totalH - RowH;
                if (eventAreaH > 0)
                    GUI.Box(new Rect(0, ey, width, eventAreaH), GUIContent.none, mEventBgStyle);

                if (op.events != null)
                {
                    for (int j = 0; j < op.events.Count; j++)
                    {
                        var ev = op.events[j];
                        if (ev == null) continue;
                        GUI.Label(new Rect(12, ey + 1, width - 16, SubRowH),
                            FormatEventRow(ev, catalog), PickEventStyle(ev.type));
                        ey += SubRowH;
                    }
                }

                if (op.verdictHints != null)
                {
                    if (op.verdictHints.targetKilled)
                    {
                        GUI.Label(new Rect(12, ey + 1, width - 16, SubRowH), "  ☠ 击杀目标", mKillStyle);
                        ey += SubRowH;
                    }
                    if (op.verdictHints.avatarDefeated)
                    {
                        GUI.Label(new Rect(12, ey + 1, width - 16, SubRowH), "  💀 玩家阵亡", mKillStyle);
                        ey += SubRowH;
                    }
                    if (op.verdictHints.effectTriggeredIds != null && op.verdictHints.effectTriggeredIds.Count > 0)
                    {
                        var triggered = new List<string>();
                        for (int k = 0; k < op.verdictHints.effectTriggeredIds.Count; k++)
                            triggered.Add(ResolveEffectName(catalog, op.verdictHints.effectTriggeredIds[k]));
                        GUI.Label(new Rect(12, ey + 1, width - 16, SubRowH),
                            "  ⚡ " + string.Join(", ", triggered), mSmallStyle);
                    }
                }
            }

            return y + totalH + 1;
        }

        private static float CalcOpExpandedHeight(BattleTraceOp op)
        {
            float h = RowH;
            if (op.events != null) h += op.events.Count * SubRowH;
            if (op.verdictHints != null)
            {
                if (op.verdictHints.targetKilled) h += SubRowH;
                if (op.verdictHints.avatarDefeated) h += SubRowH;
                if (op.verdictHints.effectTriggeredIds != null && op.verdictHints.effectTriggeredIds.Count > 0) h += SubRowH;
            }
            return h;
        }

        private float CalcTotalHeight(List<BattleTraceOp> ops)
        {
            float h = 0;
            for (int i = 0; i < ops.Count; i++)
            {
                var op = ops[i];
                if (op == null) continue;
                h += (mExpanded.Contains(i) ? CalcOpExpandedHeight(op) : RowH) + 1;
            }
            return Mathf.Max(h, 100);
        }

        private static string FormatReason(string reason)
        {
            if (string.IsNullOrEmpty(reason)) return "CombatHit";
            switch (reason)
            {
                case "PlayerAttack": return "玩家攻击";
                case "CounterAttack": return "反击";
                case "PlayerAttackTauntRedirect": return "嘲讽转移";
                default: return reason;
            }
        }

        private static string FormatEventRow(BattleTraceEventRow ev, GameContentCatalog catalog)
        {
            var parts = new List<string> { ev.type };
            if (ev.amount != 0) parts.Add("量:" + ev.amount);
            if (ev.delta != 0) parts.Add("Δ:" + ev.delta);
            if (!string.IsNullOrEmpty(ev.sourceDefId))
                parts.Add(ResolveDisplayName(catalog, ev.sourceDefId));
            if (!string.IsNullOrEmpty(ev.cause))
                parts.Add(ev.cause);
            if (ev.remainingHp != 0 || ev.remainingArmor != 0)
                parts.Add("HP:" + ev.remainingHp + " 甲:" + ev.remainingArmor);
            return "  └ " + string.Join("  |  ", parts);
        }

        private GUIStyle PickEventStyle(string eventType)
        {
            switch (eventType)
            {
                case "DamageDealt": return mDamageStyle;
                case "CardKilled": return mKillStyle;
                case "Healed": return mHealStyle;
                default: return mSmallStyle;
            }
        }

        private void BuildStylesIfNeeded()
        {
            if (mStylesBuilt) return;
            mStylesBuilt = true;

            var bgTex = MakeTex(new Color(0.13f, 0.13f, 0.16f, 0.92f));
            var evBgTex = MakeTex(new Color(0.08f, 0.08f, 0.10f, 0.92f));
            var btnTex = MakeTex(new Color(0.22f, 0.22f, 0.25f, 1f));
            var hoverTex = MakeTex(new Color(0.32f, 0.32f, 0.35f, 1f));

            mBoxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = bgTex },
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
            };

            mEventBgStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = evBgTex },
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
            };

            mRowBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                normal = { background = btnTex, textColor = new Color(0.92f, 0.88f, 0.5f) },
                hover = { background = hoverTex, textColor = new Color(1f, 0.95f, 0.6f) },
                active = { background = btnTex, textColor = new Color(0.92f, 0.88f, 0.5f) },
                padding = new RectOffset(6, 4, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(0, 0, 0, 0),
            };

            mSmallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                wordWrap = true,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) },
            };

            mDamageStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                normal = { textColor = new Color(1f, 0.32f, 0.28f) },
            };

            mHealStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                normal = { textColor = new Color(0.3f, 1f, 0.35f) },
            };

            mKillStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.18f, 0.1f) },
            };
        }

        private static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        private static string ResolveDisplayName(GameContentCatalog catalog, string defId)
        {
            if (string.IsNullOrEmpty(defId)) return "?";
            if (catalog == null) return defId;
            if (catalog.TryGetCard(defId, out var cd))
                return cd.DisplayName;
            if (catalog.TryGetSkill(defId, out var sd))
                return sd.DisplayName;
            return defId;
        }

        private static string ResolveEffectName(GameContentCatalog catalog, string effectId)
        {
            if (string.IsNullOrEmpty(effectId)) return "?";
            if (catalog == null) return effectId;
            if (catalog.TryGetEffect(effectId, out var ed))
                return string.IsNullOrEmpty(ed.DesignText) ? ed.Id : ed.DesignText;
            if (catalog.TryGetSkill(effectId, out var sd))
                return sd.DisplayName;
            return effectId;
        }
    }
}

#endif
