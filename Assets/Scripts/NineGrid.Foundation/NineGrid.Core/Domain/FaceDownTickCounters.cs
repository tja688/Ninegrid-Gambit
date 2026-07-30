using System;
using System.Collections.Generic;

namespace NineGrid.Core
{
    /// <summary>
    /// 背面专用回合计数条目（供查询 / 未来右键详情；ADR-0016）。
    /// </summary>
    public readonly struct FaceDownTickEntry
    {
        public FaceDownTickEntry(string registrationId, int remaining)
        {
            RegistrationId = registrationId ?? string.Empty;
            Remaining = remaining;
        }

        public string RegistrationId { get; }
        public int Remaining { get; }
    }

    /// <summary>
    /// 背面 Tick 到期记录（倒计时减至 0）。
    /// </summary>
    public readonly struct FaceDownTickFire
    {
        public FaceDownTickFire(int cardUid, string registrationId, int remainingAfter)
        {
            CardUid = cardUid;
            RegistrationId = registrationId ?? string.Empty;
            RemainingAfter = remainingAfter;
        }

        public int CardUid { get; }
        public string RegistrationId { get; }
        public int RemainingAfter { get; }
    }

    /// <summary>
    /// 背面专用回合计数：与 <see cref="CoreCounterKeys.AttackPatternCountdown"/> 隔离。
    /// 仅显式 <see cref="Register"/> 的 registrationId 在背面卡上于敌方行动报名时 −1；
    /// 正面或未注册不 Tick。减至 0 为一次性到期（效果侧自行 Flip / Unregister）。
    /// 查询接口供未来详情面板（ADR-0016）。
    /// </summary>
    public static class FaceDownTickCounters
    {
        public static string Key(string registrationId)
        {
            return CoreCounterKeys.FaceDownTickPrefix + (registrationId ?? string.Empty);
        }

        public static bool IsFaceDownTickKey(string counterKey)
        {
            return !string.IsNullOrEmpty(counterKey)
                && counterKey.StartsWith(CoreCounterKeys.FaceDownTickPrefix, StringComparison.Ordinal);
        }

        public static string RegistrationIdFromKey(string counterKey)
        {
            if (!IsFaceDownTickKey(counterKey))
            {
                return string.Empty;
            }

            return counterKey.Substring(CoreCounterKeys.FaceDownTickPrefix.Length);
        }

        /// <summary>注册或重置某背面计时（period≤0 视为 1）。</summary>
        public static void Register(CardInstance card, string registrationId, int period)
        {
            if (card == null || string.IsNullOrEmpty(registrationId))
            {
                return;
            }

            var p = period < 1 ? 1 : period;
            card.Counters.Set(Key(registrationId), p);
        }

        public static bool Unregister(CardInstance card, string registrationId)
        {
            if (card == null || string.IsNullOrEmpty(registrationId))
            {
                return false;
            }

            return card.Counters.Remove(Key(registrationId));
        }

        public static bool TryGetRemaining(CardInstance card, string registrationId, out int remaining)
        {
            remaining = 0;
            if (card == null || string.IsNullOrEmpty(registrationId))
            {
                return false;
            }

            var key = Key(registrationId);
            if (!card.Counters.Values.ContainsKey(key))
            {
                return false;
            }

            remaining = card.Counters.Get(key);
            return true;
        }

        /// <summary>列出该卡全部背面 Tick 注册（含剩余）；无注册则清空 <paramref name="into"/>。</summary>
        public static void Collect(CardInstance card, List<FaceDownTickEntry> into)
        {
            if (into == null)
            {
                return;
            }

            into.Clear();
            if (card == null)
            {
                return;
            }

            foreach (var pair in card.Counters.Values)
            {
                if (!IsFaceDownTickKey(pair.Key))
                {
                    continue;
                }

                into.Add(new FaceDownTickEntry(RegistrationIdFromKey(pair.Key), pair.Value));
            }
        }

        /// <summary>
        /// 对场上所有背面卡的 faceDownTick.* 各 −1。
        /// 减至 0 写入 <paramref name="fired"/>；正面卡跳过。
        /// </summary>
        public static void TickFaceDownBoard(
            BoardModel board,
            CardRegistry registry,
            List<FaceDownTickFire> fired)
        {
            if (fired != null)
            {
                fired.Clear();
            }

            if (board == null || registry == null)
            {
                return;
            }

            foreach (var uid in board.BoardCardUids())
            {
                CardInstance card;
                if (!registry.TryGet(uid, out card) || card == null || card.FaceUp)
                {
                    continue;
                }

                TickCard(card, fired);
            }
        }

        private static void TickCard(CardInstance card, List<FaceDownTickFire> fired)
        {
            var keys = new List<string>();
            foreach (var pair in card.Counters.Values)
            {
                if (IsFaceDownTickKey(pair.Key))
                {
                    keys.Add(pair.Key);
                }
            }

            for (var i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                var remaining = card.Counters.Get(key);
                if (remaining <= 0)
                {
                    continue;
                }

                remaining -= 1;
                card.Counters.Set(key, remaining);
                if (remaining > 0 || fired == null)
                {
                    continue;
                }

                fired.Add(new FaceDownTickFire(card.Uid, RegistrationIdFromKey(key), remaining));
            }
        }
    }
}
