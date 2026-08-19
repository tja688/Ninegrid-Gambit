using NineGrid.Core;
using NineGrid.Flow.Tutorial;
using NineGrid.Presentation.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NineGrid.Flow.BoardBriefTip
{
    /// <summary>
    /// 场景 <c>楼层提示</c>：<c>大楼层提示</c> 显示当前地下城环境名（教程期间显示 密林_翡翠迷雾）；
    /// <c>小房间提示</c> 显示房间类型名（教程期间显示 教程）。
    /// 仅在教程期间特化显示，常规模式（后续第2、3次对局）恢复常规房间与环境信息。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloorHintPresenter : MonoBehaviour
    {
        public const string RootObjectName = "楼层提示";
        public const string FloorLevelHintObjectName = "大楼层提示";
        public const string RoomHintObjectName = "小房间提示";

        public const string TutorialFloorLevelHint = "密林_翡翠迷雾";
        public const string TutorialRoomHint = "教程";

        private static FloorHintPresenter sInstance;

        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text floorLevelText;
        [SerializeField] private TMP_Text roomText;

        private int mLastFloor = int.MinValue;
        private int mLastNodeIndex = int.MinValue;
        private string mLastDifficultyId = null;
        private RoomKind mLastRoom = (RoomKind)(-1);
        private bool mLastIsTutorial = false;

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

        public static bool IsTutorialActive(int nodeIndex = 0, RunModel run = null)
        {
            var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            var shell = arch?.GetSystem<IGameFlowShellSystem>();
            if (shell != null && shell.IsTutorialMode)
            {
                return true;
            }

            if (TutorialCoach.IsSteps1To9Active || TutorialCoach.IsDoorTutorialActive)
            {
                return true;
            }

            if (TutorialCoach.EntryKind == TutorialEntryKind.Menu)
            {
                return true;
            }

            if (run == null && arch != null)
            {
                run = arch.GetModel<RunModel>();
            }

            var actualNodeIndex = nodeIndex > 0
                ? nodeIndex
                : (run?.NodeIndex != null ? run.NodeIndex.Value : 0);

            if (actualNodeIndex <= 1
                && (shell == null || !shell.IsQuickTestMode)
                && !TutorialProgressStore.IsSteps1To9Completed())
            {
                return true;
            }

            return false;
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
            var nodeIndex = run.NodeIndex.Value;
            var difficultyId = run.DifficultyId != null
                ? run.DifficultyId.Value
                : NineGrid.Core.Content.RunDifficultyIds.Default;
            var room = run.Room.Value;
            var isTutorial = IsTutorialActive(nodeIndex, run);

            if (floor == mLastFloor
                && nodeIndex == mLastNodeIndex
                && room == mLastRoom
                && isTutorial == mLastIsTutorial
                && string.Equals(mLastDifficultyId, difficultyId, System.StringComparison.Ordinal))
            {
                return;
            }

            mLastIsTutorial = isTutorial;
            Apply(floor, room, nodeIndex, difficultyId);
        }

        public void Apply(int floor, RoomKind room, int nodeIndex, string difficultyId)
        {
            var self = EnsureExists();
            if (!ReferenceEquals(self, this))
            {
                self.Apply(floor, room, nodeIndex, difficultyId);
                return;
            }

            EnsureBindings();
            mLastFloor = floor;
            mLastNodeIndex = nodeIndex;
            mLastDifficultyId = difficultyId;
            mLastRoom = room;

            var isTutorial = IsTutorialActive(nodeIndex);

            if (floorLevelText != null)
            {
                floorLevelText.text = isTutorial
                    ? BoardBriefTipCopy.TutorialFloorLevelHint
                    : BoardBriefTipCopy.FormatFloorLevelHint(floor, nodeIndex, difficultyId);
            }

            if (roomText != null)
            {
                roomText.text = isTutorial
                    ? BoardBriefTipCopy.TutorialRoomHint
                    : BoardBriefTipCopy.FormatRoomHint(room);
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

            if (root == null)
            {
                return;
            }

            if (floorLevelText == null)
            {
                floorLevelText = FindHintText(root.transform, FloorLevelHintObjectName);
            }

            if (roomText == null)
            {
                roomText = FindHintText(root.transform, RoomHintObjectName);
            }
        }

        private static TMP_Text FindHintText(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrEmpty(objectName))
            {
                return null;
            }

            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var candidate = transforms[i];
                if (candidate == null || candidate.name != objectName)
                {
                    continue;
                }

                var text = candidate.GetComponent<TMP_Text>();
                if (text != null)
                {
                    return text;
                }
            }

            return null;
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
