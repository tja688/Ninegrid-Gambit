using UnityEngine;

namespace NineGrid.Battle
{
    /// <summary>
    /// 船锚余量：下方三个图标表示可撞击次数，撞一次熄灭一个。
    /// 归零即视为本场战斗打完（当前无真实数值，纯按次数走流程）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnchorHpTracker : MonoBehaviour
    {
        [Header("引用（留空自动解析）")]
        [Tooltip("船锚余量根物体。留空时按名字「船锚余量」查找。")]
        [SerializeField] Transform iconsRoot;
        [Tooltip("三个余量图标。留空时取 iconsRoot 的子物体。")]
        [SerializeField] GameObject[] icons;

        int _remaining;
        bool _resolved;

        public int Remaining => _remaining;
        public int Capacity => icons != null ? icons.Length : 0;
        public bool IsEmpty => _remaining <= 0;

        void Awake()
        {
            ResolveRefs();
            ResetFull();
        }

        /// <summary>点亮全部图标，余量拉满。</summary>
        public void ResetFull()
        {
            ResolveRefs();
            if (icons == null)
            {
                _remaining = 0;
                return;
            }

            for (var i = 0; i < icons.Length; i++)
            {
                if (icons[i] != null)
                {
                    icons[i].SetActive(true);
                }
            }

            _remaining = icons.Length;
        }

        /// <summary>消耗一次余量并熄灭一个图标；返回消耗后是否已归零。</summary>
        public bool ConsumeOne()
        {
            ResolveRefs();
            if (_remaining <= 0)
            {
                return true;
            }

            _remaining--;

            // 从末尾往前熄灭。
            if (icons != null && _remaining >= 0 && _remaining < icons.Length)
            {
                var icon = icons[_remaining];
                if (icon != null)
                {
                    icon.SetActive(false);
                }
            }

            return _remaining <= 0;
        }

        void ResolveRefs()
        {
            if (_resolved)
            {
                return;
            }

            if (iconsRoot == null)
            {
                var go = GameObject.Find("船锚余量");
                iconsRoot = go != null ? go.transform : null;
            }

            var needFill = icons == null || icons.Length == 0;
            if (!needFill)
            {
                for (var i = 0; i < icons.Length; i++)
                {
                    if (icons[i] == null)
                    {
                        needFill = true;
                        break;
                    }
                }
            }

            if (needFill && iconsRoot != null)
            {
                var filled = new GameObject[iconsRoot.childCount];
                for (var i = 0; i < iconsRoot.childCount; i++)
                {
                    filled[i] = iconsRoot.GetChild(i).gameObject;
                }

                icons = filled;
            }

            _resolved = true;
        }
    }
}
