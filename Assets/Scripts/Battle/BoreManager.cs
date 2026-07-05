using System;
using UnityEngine;

namespace NineGrid.Battle
{
    /// <summary>
    /// 玩家船体钻头（bores）管理：前 / 中 / 后三枚钻头对应锻造台左 / 中 / 右。
    /// 未铸造前全部隐藏；某个锻造台打出伤害潜力后，亮出对应钻头。
    /// 数值 / 伤害潜力尚未接入，当前只按「该锻造台有材料」决定是否亮钻头。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoreManager : MonoBehaviour
    {
        public const int BoreCount = 3;

        [Header("引用（留空自动解析）")]
        [Tooltip("bores 根物体（player 下）。留空时按名字 bores 查找。")]
        [SerializeField] Transform boresRoot;
        [Tooltip("前/中/后三枚钻头，对应锻造台左/中/右。留空时取 boresRoot 前三个子物体。")]
        [SerializeField] Transform[] bores = new Transform[BoreCount];

        readonly bool[] _lit = new bool[BoreCount];
        bool _resolved;

        public event Action<int> BoreShown;
        public event Action BoresHidden;

        public bool HasAnyBore
        {
            get
            {
                for (var i = 0; i < _lit.Length; i++)
                {
                    if (_lit[i])
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        void Awake()
        {
            ResolveRefs();
            HideAll();
        }

        /// <summary>隐藏所有钻头（战斗开始 / 铸造前状态）。</summary>
        public void HideAll()
        {
            ResolveRefs();
            for (var i = 0; i < bores.Length && i < _lit.Length; i++)
            {
                _lit[i] = false;
                SetBoreActive(i, false);
            }

            BoresHidden?.Invoke();
        }

        /// <summary>按锻造台占用情况亮出对应钻头（true 的位置亮起，累加不清除既有）。</summary>
        public void ShowBores(bool[] occupied)
        {
            ResolveRefs();
            if (occupied == null)
            {
                return;
            }

            for (var i = 0; i < BoreCount && i < occupied.Length; i++)
            {
                if (occupied[i])
                {
                    ShowBore(i);
                }
            }
        }

        /// <summary>亮出单枚钻头。</summary>
        public void ShowBore(int index)
        {
            ResolveRefs();
            if (index < 0 || index >= BoreCount || index >= bores.Length)
            {
                return;
            }

            _lit[index] = true;
            SetBoreActive(index, true);
            BoreShown?.Invoke(index);
        }

        void SetBoreActive(int index, bool active)
        {
            if (bores == null || index < 0 || index >= bores.Length)
            {
                return;
            }

            var bore = bores[index];
            if (bore != null)
            {
                bore.gameObject.SetActive(active);
            }
        }

        void ResolveRefs()
        {
            if (_resolved)
            {
                return;
            }

            if (boresRoot == null)
            {
                var go = GameObject.Find("bores");
                boresRoot = go != null ? go.transform : transform;
            }

            var needFill = bores == null || bores.Length < BoreCount;
            if (!needFill)
            {
                for (var i = 0; i < BoreCount; i++)
                {
                    if (bores[i] == null)
                    {
                        needFill = true;
                        break;
                    }
                }
            }

            if (needFill && boresRoot != null)
            {
                var filled = new Transform[BoreCount];
                var count = Mathf.Min(BoreCount, boresRoot.childCount);
                for (var i = 0; i < count; i++)
                {
                    filled[i] = boresRoot.GetChild(i);
                }

                // 保留已在 Inspector 指定的项。
                if (bores != null)
                {
                    for (var i = 0; i < BoreCount && i < bores.Length; i++)
                    {
                        if (bores[i] != null)
                        {
                            filled[i] = bores[i];
                        }
                    }
                }

                bores = filled;
            }

            _resolved = true;
        }
    }
}
