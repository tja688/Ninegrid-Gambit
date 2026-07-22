using System;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 炸牌散点：在圆心圆周上等角分布 N 点；相位可随机，保证每次朝向不同且不叠点。
    /// </summary>
    public static class BurstScatterPointSampler
    {
        public static Vector3[] SampleOnCircle(Vector3 center, float radius, int count, float phaseRadians)
        {
            if (count <= 0)
            {
                return Array.Empty<Vector3>();
            }

            var safeRadius = Mathf.Max(0f, radius);
            var points = new Vector3[count];
            var step = (Mathf.PI * 2f) / count;
            for (var i = 0; i < count; i++)
            {
                var angle = phaseRadians + step * i;
                points[i] = center + new Vector3(
                    Mathf.Cos(angle) * safeRadius,
                    Mathf.Sin(angle) * safeRadius,
                    0f);
            }

            return points;
        }

        public static float RandomPhaseRadians()
        {
            return UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        }
    }
}
