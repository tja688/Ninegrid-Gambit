using System;
using UnityEngine;

namespace NineGrid.Flow.InfoNotice
{
    /// <summary>
    /// 消息提示栏打字机入场：从中心一字一字向两边蹦出。
    /// 整段固定在 <see cref="DefaultDurationSeconds"/> 内出完，作为所有提示的底层规则。
    /// </summary>
    public static class InfoNoticeCenterOutReveal
    {
        public const float DefaultDurationSeconds = 0.2f;

        /// <summary>
        /// 生成中心向外的逐字揭示顺序。奇数取正中，偶数取偏左的中心字，随后左右交替。
        /// </summary>
        public static int[] BuildOrder(int charCount)
        {
            if (charCount <= 0)
            {
                return Array.Empty<int>();
            }

            var order = new int[charCount];
            var written = 0;
            var center = (charCount - 1) / 2;
            order[written++] = center;

            var left = center - 1;
            var right = center + 1;
            while (left >= 0 || right < charCount)
            {
                if (left >= 0)
                {
                    order[written++] = left--;
                }

                if (right < charCount)
                {
                    order[written++] = right++;
                }
            }

            return order;
        }

        /// <summary>
        /// 按已经过时间算出本帧应揭示到第几个字（至少 1，至多 charCount）。
        /// </summary>
        public static int CountRevealedAt(float elapsedSeconds, float durationSeconds, int charCount)
        {
            if (charCount <= 0)
            {
                return 0;
            }

            if (durationSeconds <= 0.001f || elapsedSeconds >= durationSeconds)
            {
                return charCount;
            }

            var t = Mathf.Clamp01(elapsedSeconds / durationSeconds);
            return Mathf.Max(1, Mathf.CeilToInt(t * charCount));
        }
    }
}
