using NineGrid.Core;
using TMPro;
using UnityEngine;

namespace NineGrid.Flow.BoardBriefTip
{
    /// <summary>
    /// 场景 <c>楼层提示</c>：显示「第 X 层 · 节点 Y」，随 <see cref="RunModel"/> 刷新。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloorHintPresenter : MonoBehaviour
    {
        public const string RootObjectName = "楼层提示";

        private static FloorHintPresenter sInstance;

        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text bodyText;

        private int mLastFloor = int.MinValue;
        private int mLastNodeIndex = int.MinValue;

        public static FloorHintPresenter EnsureExists()
        {
            if (sInstance != null)
            {
                sInstance.EnsureBindings();
                return sInstance;
            }

            sInstance = FindFirstObjectByType<FloorHintPresenter>();
            if (sInstance != null)
            {
                sInstance.EnsureBindings();
                return sInstance;
            }

            var found = FindRoot();
            if (found == null)
            {
                var go = new GameObject(nameof(FloorHintPresenter));
                sInstance = go.AddComponent<FloorHintPresenter>();
                return sInstance;
            }

            var presenter = found.GetComponent<FloorHintPresenter>();
            if (presenter == null)
            {
                presenter = found.AddComponent<FloorHintPresenter>();
            }

            presenter.root = found;
            presenter.EnsureBindings();
            sInstance = presenter;
            return presenter;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EnsureExists();
        }

        private void Awake()
        {
            sInstance = this;
            EnsureBindings();
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(sInstance, this))
            {
                sInstance = null;
            }
        }

        private void LateUpdate()
        {
            var arch = NineGridArchitecture.Current;
            var run = arch != null ? arch.GetModel<RunModel>() : null;
            if (run == null)
            {
                return;
            }

            var floor = run.Floor.Value;
            var node = run.NodeIndex.Value;
            if (floor == mLastFloor && node == mLastNodeIndex)
            {
                return;
            }

            Apply(floor, node);
        }

        public void Apply(int floor, int nodeIndex)
        {
            EnsureBindings();
            mLastFloor = floor;
            mLastNodeIndex = nodeIndex;
            var text = BoardBriefTipCopy.FormatFloorHint(floor, nodeIndex);
            if (bodyText != null)
            {
                bodyText.text = text;
            }
        }

        public void EnsureBindings()
        {
            if (root == null)
            {
                root = gameObject.name == RootObjectName ? gameObject : FindRoot();
            }

            if (bodyText == null && root != null)
            {
                bodyText = root.GetComponentInChildren<TMP_Text>(true);
            }
        }

        private static GameObject FindRoot()
        {
            var all = Resources.FindObjectsOfTypeAll<Transform>();
            for (var i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null || t.name != RootObjectName)
                {
                    continue;
                }

                if (!t.gameObject.scene.IsValid())
                {
                    continue;
                }

                return t.gameObject;
            }

            return GameObject.Find(RootObjectName);
        }
    }
}
