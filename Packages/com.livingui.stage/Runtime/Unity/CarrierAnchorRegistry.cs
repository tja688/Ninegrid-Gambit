using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.LivingUI.Unity
{
    /// <summary>
    /// B 类锚点映射位：按名发布载体局部锚点的当前世界位姿。
    /// LivingUI 不拥有/不搬游戏对象；游戏系统自取位姿摆放。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CarrierAnchorRegistry : MonoBehaviour
    {
        [Serializable]
        private struct NamedAnchor
        {
            [Tooltip("锚点逻辑名；游戏系统按此名查询。")]
            public string Name;

            [Tooltip("锚点 Transform；留空则用本物体。")]
            public Transform Point;
        }

        [Tooltip("本载体上注册的 B 类锚点；主菜单切片可留空。")]
        [SerializeField] private NamedAnchor[] anchors = Array.Empty<NamedAnchor>();

        private readonly Dictionary<string, Transform> _map = new(StringComparer.Ordinal);

        private void Awake()
        {
            Rebuild();
        }

        public void Rebuild()
        {
            _map.Clear();
            if (anchors == null) return;
            for (var i = 0; i < anchors.Length; i++)
            {
                var entry = anchors[i];
                if (string.IsNullOrEmpty(entry.Name)) continue;
                _map[entry.Name] = entry.Point != null ? entry.Point : transform;
            }
        }

        public bool TryGetWorldPose(string name, out Vector3 position, out Quaternion rotation)
        {
            if (_map.TryGetValue(name, out var point) && point != null)
            {
                position = point.position;
                rotation = point.rotation;
                return true;
            }

            position = default;
            rotation = Quaternion.identity;
            return false;
        }
    }
}
