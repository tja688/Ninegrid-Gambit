using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NineGrid.Cards;
using UnityEngine;

namespace NineGrid.Flow.Diagnostics
{
    /// <summary>
    /// 共享 BoardSnap 采集：PerfLog / RegistryLog 复用，避免重复实现。
    /// </summary>
    public static class DiagBoardSnapCapture
    {
        public struct CardSnap
        {
            public int Uid;
            public int Slot;
            public int XCm;
            public int YCm;
            public bool Active;
            public bool FieldDead;
            public string Mode;
            public bool Tween;
        }

        public static Dictionary<int, CardSnap> CaptureLiveBoard(IReadOnlyCollection<int> tweeningUids = null)
        {
            var result = new Dictionary<int, CardSnap>();
            var cards = CardEntityLifecycleHook.CardsOrNull();
            var field = GroundFieldGeometryHook.FieldOrNull();
            if (cards == null)
            {
                return result;
            }

            foreach (var card in cards.EnumerateCards())
            {
                if (card?.Transform == null)
                {
                    continue;
                }

                var go = card.GameObject;
                var active = go != null && go.activeInHierarchy;
                var slot = -1;
                if (field != null && field.TryGetSlotOf(card.Uid, out var found))
                {
                    slot = found;
                }

                var tween = false;
                if (tweeningUids != null)
                {
                    foreach (var tweenUid in tweeningUids)
                    {
                        if (tweenUid == card.Uid)
                        {
                            tween = true;
                            break;
                        }
                    }
                }
                var pos = card.Transform.position;
                result[card.Uid] = new CardSnap
                {
                    Uid = card.Uid,
                    Slot = slot,
                    XCm = CardPresentationProbe.QuantizeCm(pos.x),
                    YCm = CardPresentationProbe.QuantizeCm(pos.y),
                    Active = active,
                    FieldDead = card.IsFieldDead,
                    Mode = card.DisplayMode.ToString(),
                    Tween = tween,
                };
            }

            return result;
        }

        public static string BuildFullCardsString(Dictionary<int, CardSnap> cards)
        {
            if (cards == null || cards.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder(cards.Count * 24);
            foreach (var kv in cards)
            {
                if (sb.Length > 0)
                {
                    sb.Append(';');
                }

                AppendCardLine(sb, kv.Value);
            }

            return sb.ToString();
        }

        public static void AppendCardLine(StringBuilder sb, CardSnap c)
        {
            sb.Append(c.Uid)
                .Append(",slot=").Append(c.Slot)
                .Append(",xy=").Append(CmToStr(c.XCm)).Append(',').Append(CmToStr(c.YCm))
                .Append(",active=").Append(c.Active ? '1' : '0')
                .Append(",fieldDead=").Append(c.FieldDead ? '1' : '0')
                .Append(",mode=").Append(c.Mode ?? string.Empty)
                .Append(",tween=").Append(c.Tween ? '1' : '0');
        }

        private static string CmToStr(int cm)
        {
            return (cm / 100f).ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
