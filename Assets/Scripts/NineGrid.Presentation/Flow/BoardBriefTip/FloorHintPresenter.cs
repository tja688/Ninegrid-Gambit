using NineGrid.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

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
            var found = FindRoot();
            if (found != null)
            {
                var presenter = found.GetComponent<FloorHintPresenter>();
                if (presenter == null)
                {
                    presenter = found.AddComponent<FloorHintPresenter>();
                }

                presenter.root = found;
                presenter.EnsureBindings();
                AdoptInstance(presenter);
                return presenter;
            }

            if (sInstance != null)
            {
                sInstance.EnsureBindings();
                return sInstance;
            }

            sInstance = FindFirstObjectByType<FloorHintPresenter>(FindObjectsInactive.Include);
            if (sInstance != null)
            {
                sInstance.EnsureBindings();
                return sInstance;
            }

            var go = new GameObject(nameof(FloorHintPresenter));
            sInstance = go.AddComponent<FloorHintPresenter>();
            return sInstance;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EnsureExists();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureExists();
        }

        private void Awake()
        {
            EnsureBindings();
            if (gameObject.name == RootObjectName)
            {
                AdoptInstance(this);
            }
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
            var self = EnsureExists();
            if (!ReferenceEquals(self, this))
            {
                self.Apply(floor, nodeIndex);
                return;
            }

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
            if (root == null || root.name != RootObjectName)
            {
                var found = FindRoot();
                if (found != null)
                {
                    root = found;
                }
                else if (gameObject.name == RootObjectName)
                {
                    root = gameObject;
                }
            }

            if (bodyText == null && root != null)
            {
                bodyText = root.GetComponentInChildren<TMP_Text>(true);
            }
        }

        private static void AdoptInstance(FloorHintPresenter presenter)
        {
            if (presenter == null)
            {
                return;
            }

            if (sInstance != null
                && !ReferenceEquals(sInstance, presenter)
                && sInstance.gameObject != null
                && sInstance.gameObject.name == nameof(FloorHintPresenter))
            {
                var orphan = sInstance.gameObject;
                sInstance = presenter;
                Object.Destroy(orphan);
                return;
            }

            sInstance = presenter;
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
