using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.LivingUI.Unity
{
    /// <summary>
    /// 稳定 Hover 命中区根：子区矩形烘焙自主菜单终态，不随预演/pop 载体移动，避免自激。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LivingUiStableHitZoneRoot : MonoBehaviour
    {
        [Serializable]
        public struct Zone
        {
            [Tooltip("对应载体 ID（主菜单右侧三键：12/11/2）。")]
            public int CarrierId;

            [Tooltip("世界空间轴对齐命中矩形（中心+尺寸由 Min/Size 表达）。")]
            public Rect WorldRect;
        }

        [Tooltip("稳定命中区列表；由装配代码按 MainMenu terminal 烘焙，运行时不再跟随载体。")]
        [SerializeField] private Zone[] zones = Array.Empty<Zone>();

        public IReadOnlyList<Zone> Zones => zones;

        public void SetZones(Zone[] next)
        {
            zones = next ?? Array.Empty<Zone>();
        }

        public bool TryHit(Vector2 worldPoint, out int carrierId)
        {
            if (zones != null)
            {
                for (var i = 0; i < zones.Length; i++)
                {
                    if (zones[i].WorldRect.Contains(worldPoint))
                    {
                        carrierId = zones[i].CarrierId;
                        return true;
                    }
                }
            }

            carrierId = 0;
            return false;
        }
    }
}
