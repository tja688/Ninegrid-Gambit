using System;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 炸牌散点：在场地矩形内做抖动网格采样——保证不叠点、尽量铺满整个 GroundPanel，
    /// 每次运行带随机抖动，不依赖任何宿主位置。泡泡菜单是圆周排布，这里针对
    /// 「整场分散炸开」改为宽高比自适应的网格，避免单卡孤立叠在场地边缘。
    /// </summary>
    public static class BurstScatterPointSampler
    {
        /// <summary>单卡落点抖动相对整场尺寸的比例（中心附近小偏移，避免每局钉死在正中）。</summary>
        private const float SingleJitterRatio = 0.12f;

        /// <summary>格内抖动幅度相对格宽/格高的比例；<0.5 保证相邻格永不重叠。</summary>
        private const float CellJitterRatio = 0.32f;

        /// <summary>网格排不满的尾行自动水平居中，避免散点偏向一侧。</summary>
        private const float TailRowCenterBias = 0.5f;

        public static Vector3[] SampleInRect(Rect rect, int count)
        {
            if (count <= 0)
            {
                return Array.Empty<Vector3>();
            }

            if (count == 1)
            {
                var cx = rect.x + rect.width * (0.5f + UnityEngine.Random.Range(-SingleJitterRatio, SingleJitterRatio));
                var cy = rect.y + rect.height * (0.5f + UnityEngine.Random.Range(-SingleJitterRatio, SingleJitterRatio));
                return new[] { new Vector3(cx, cy, 0f) };
            }

            var aspect = rect.width / Mathf.Max(0.001f, rect.height);
            var cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count * aspect)));
            var rows = Mathf.Max(1, Mathf.CeilToInt((float)count / cols));
            var cellW = rect.width / cols;
            var cellH = rect.height / rows;
            var jitterW = cellW * CellJitterRatio;
            var jitterH = cellH * CellJitterRatio;

            var leftover = count - cols * (rows - 1);
            var tailCount = Mathf.Max(0, Mathf.Min(cols, leftover));

            var points = new Vector3[count];
            for (var i = 0; i < count; i++)
            {
                var row = i / cols;
                var col = i % cols;
                var isTailRow = row == rows - 1;
                var offsetX = 0f;
                if (isTailRow && tailCount < cols)
                {
                    // 尾行不满时水平居中。
                    offsetX = (cols - tailCount) * cellW * TailRowCenterBias;
                }

                var px = rect.x + offsetX + cellW * (col + 0.5f)
                         + UnityEngine.Random.Range(-jitterW, jitterW);
                var py = rect.y + cellH * (row + 0.5f)
                         + UnityEngine.Random.Range(-jitterH, jitterH);
                points[i] = new Vector3(px, py, 0f);
            }

            return points;
        }
    }
}
