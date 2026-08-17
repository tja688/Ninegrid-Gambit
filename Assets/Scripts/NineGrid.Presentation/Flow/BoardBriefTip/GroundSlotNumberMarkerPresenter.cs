using System;
using System.Collections.Generic;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NineGrid.Flow.BoardBriefTip
{
    /// <summary>
    /// 场地格位数字标记显隐控制器：
    /// 仅在战斗中出现（普通对战、Boss 战、教学关卡、QuickTest）；
    /// 在选房阶段（RoomChoice）、消费房（RoomEvent）、奖励（RewardChoice）、主菜单等其它阶段自动隐藏。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GroundSlotNumberMarkerPresenter : MonoBehaviour
    {
        public const string GroundAnchorsObjectName = "GroundAnchors";
        public const string DefaultMarkerNameKeyword = "数字标记";

        private static GroundSlotNumberMarkerPresenter sInstance;

        [Tooltip("9 个槽位内的数字标记物体缓存；留空则运行时自动按槽位子物体扫描。")]
        [SerializeField] private GameObject[] markerObjects;

        private bool? _lastAppliedVisibility;

        public static GroundSlotNumberMarkerPresenter EnsureExists()
        {
            var found = FindFirstObjectByType<GroundSlotNumberMarkerPresenter>(FindObjectsInactive.Include);
            if (found != null)
            {
                found.EnsureBindings();
                AdoptInstance(found);
                return found;
            }

            if (sInstance != null)
            {
                sInstance.EnsureBindings();
                return sInstance;
            }

            var root = FindSceneObjectByName(GroundAnchorsObjectName);
            if (root != null)
            {
                sInstance = root.GetComponent<GroundSlotNumberMarkerPresenter>();
                if (sInstance == null)
                {
                    sInstance = root.AddComponent<GroundSlotNumberMarkerPresenter>();
                }

                sInstance.EnsureBindings();
                return sInstance;
            }

            var go = new GameObject(nameof(GroundSlotNumberMarkerPresenter));
            sInstance = go.AddComponent<GroundSlotNumberMarkerPresenter>();
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
            AdoptInstance(this);
            RefreshVisibility();
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
            RefreshVisibility();
        }

        /// <summary>
        /// 确保 9 个格位的数字标记子物体绑定。
        /// </summary>
        public void EnsureBindings()
        {
            if (markerObjects != null && markerObjects.Length > 0 && HasAnyValidMarker(markerObjects))
            {
                return;
            }

            markerObjects = CollectSlotNumberMarkers();
            _lastAppliedVisibility = null;
        }

        /// <summary>
        /// 根据当前流程壳状态刷新数字标记显隐。
        /// </summary>
        public void RefreshVisibility()
        {
            EnsureBindings();
            var shouldBeVisible = IsInBattleState();
            SetVisible(shouldBeVisible);
        }

        /// <summary>
        /// 显式设置所有数字标记的显隐状态。
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (_lastAppliedVisibility.HasValue && _lastAppliedVisibility.Value == visible)
            {
                return;
            }

            if (markerObjects == null || markerObjects.Length == 0)
            {
                return;
            }

            for (var i = 0; i < markerObjects.Length; i++)
            {
                var go = markerObjects[i];
                if (go != null && go.activeSelf != visible)
                {
                    go.SetActive(visible);
                }
            }

            _lastAppliedVisibility = visible;
        }

        private static bool IsInBattleState()
        {
            var arch = NineGridArchitecture.Current ?? NineGridArchitecture.Interface;
            if (arch == null)
            {
                return false;
            }

            var shell = arch.GetSystem<IGameFlowShellSystem>();
            if (shell == null)
            {
                return false;
            }

            return shell.State.Value == GameFlowShellState.BattleStub;
        }

        private static GameObject[] CollectSlotNumberMarkers()
        {
            var root = FindSceneObjectByName(GroundAnchorsObjectName);
            if (root == null)
            {
                return Array.Empty<GameObject>();
            }

            var slots = CardSlotAnchorUtility.GetSortedSlotTransforms(root.transform, GroundSlotTopology.MaxSlot);
            var markers = new List<GameObject>(slots.Count);

            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null)
                {
                    continue;
                }

                var markerGo = FindMarkerInSlot(slot);
                if (markerGo != null)
                {
                    markers.Add(markerGo);
                }
            }

            // 若按 sorted slot 未找齐，回退遍历所有子节点查找
            if (markers.Count == 0)
            {
                for (var i = 0; i < root.transform.childCount; i++)
                {
                    var child = root.transform.GetChild(i);
                    var markerGo = FindMarkerInSlot(child);
                    if (markerGo != null && !markers.Contains(markerGo))
                    {
                        markers.Add(markerGo);
                    }
                }
            }

            return markers.ToArray();
        }

        private static GameObject FindMarkerInSlot(Transform slot)
        {
            if (slot == null)
            {
                return null;
            }

            // 1. 优先按名称关键字匹配（如 "数字标记" / "数字标记 (1)"）
            for (var i = 0; i < slot.childCount; i++)
            {
                var child = slot.GetChild(i);
                if (child.name.IndexOf(DefaultMarkerNameKeyword, StringComparison.OrdinalIgnoreCase) >= 0
                    || child.name.IndexOf("数字", StringComparison.OrdinalIgnoreCase) >= 0
                    || child.name.IndexOf("Marker", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return child.gameObject;
                }
            }

            // 2. 兜底查找带有 SpriteRenderer 的直接子物体
            for (var i = 0; i < slot.childCount; i++)
            {
                var child = slot.GetChild(i);
                if (child.GetComponent<SpriteRenderer>() != null)
                {
                    return child.gameObject;
                }
            }

            // 3. 槽位下若有任意单一子物体，直接作为标记
            if (slot.childCount > 0)
            {
                return slot.GetChild(0).gameObject;
            }

            return null;
        }

        private static bool HasAnyValidMarker(GameObject[] list)
        {
            for (var i = 0; i < list.Length; i++)
            {
                if (list[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AdoptInstance(GroundSlotNumberMarkerPresenter presenter)
        {
            if (presenter == null)
            {
                return;
            }

            if (sInstance == null || !ReferenceEquals(sInstance, presenter))
            {
                if (sInstance != null
                    && sInstance != presenter
                    && sInstance.gameObject != null
                    && sInstance.gameObject.name == nameof(GroundSlotNumberMarkerPresenter))
                {
                    Destroy(sInstance.gameObject);
                }

                sInstance = presenter;
            }
        }

        private static GameObject FindSceneObjectByName(string objectName)
        {
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var found = FindDeep(roots[i].transform, objectName);
                if (found != null)
                {
                    return found.gameObject;
                }
            }

            return null;
        }

        private static Transform FindDeep(Transform root, string objectName)
        {
            if (root == null)
            {
                return null;
            }

            if (string.Equals(root.name, objectName, StringComparison.Ordinal))
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
