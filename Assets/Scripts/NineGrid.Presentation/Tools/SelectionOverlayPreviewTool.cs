using System.Collections.Generic;
using DG.Tweening;
using NineGrid.Presentation.Performance;
using UnityEngine;

namespace NineGrid.Presentation.Tools
{
    /// <summary>
    /// 选择层预览壳：按 3 弹出 Inspector 配置的 N 选 1，串联入场 / 悬停 / 退场原子表演。
    /// 挂到场景任意物体，配置表演器引用与选项数量即可试效果。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SelectionOverlayPreviewTool : MonoBehaviour
    {
        private enum PreviewPhase
        {
            Idle,
            Entering,
            Interactive,
            Dismissing,
        }

        [Header("Hotkey")]
        [SerializeField] private KeyCode previewKey = KeyCode.Alpha3;

        [Header("Choice")]
        [SerializeField, Min(1)] private int optionCount = 3;
        [SerializeField] private Vector3 containerLocalOffset = new(0f, 0.5f, 0f);
        [SerializeField, Min(0)] private int baseSortingOrder = 420;

        [Header("Layout")]
        [SerializeField] private SelectionFanLayout fanLayout = new();

        [Header("Assets")]
        [SerializeField] private GameObject cardPreviewPrefab;
        [SerializeField] private Transform optionsRoot;
        [SerializeField] private Transform confirmTargetSlot;
        [SerializeField] private Camera worldCamera;

        [Header("Performances")]
        [SerializeField] private SelectionOverlayEntrancePerformance entrancePerformance;
        [SerializeField] private SelectionOptionHoverPerformance hoverPerformance;
        [SerializeField] private SelectionOptionFallOffPerformance fallOffPerformance;
        [SerializeField] private SelectionOptionConfirmPerformance confirmPerformance;

        private readonly List<Transform> optionActors = new();
        private readonly List<SelectionOptionHoverPerformance.OptionActor> hoverActors = new();
        private readonly List<float> optionBaseRotations = new();
        private readonly List<int> optionSortingOrders = new();

        private Transform optionsContainer;
        private PreviewPhase phase = PreviewPhase.Idle;
        private int hoveredIndex = -1;
        private int pendingFallCount;
        private bool confirmFinished;

        private void Awake()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            EnsurePerformancesOnSameObject();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (Input.GetKeyDown(previewKey))
            {
                if (phase == PreviewPhase.Idle)
                {
                    BeginPreview();
                }
                else
                {
                    CancelPreview();
                }

                return;
            }

            if (phase != PreviewPhase.Interactive || optionActors.Count == 0)
            {
                return;
            }

            int hovered = DetermineHoveredIndex();
            if (hovered != hoveredIndex)
            {
                hoveredIndex = hovered;
                if (hoveredIndex < 0)
                {
                    hoverPerformance?.PlayReset();
                }
                else
                {
                    hoverPerformance?.PlayHover(hoveredIndex);
                }
            }

            if (hoveredIndex >= 0 && Input.GetMouseButtonDown(0))
            {
                BeginDismiss(hoveredIndex);
            }
        }

        [ContextMenu("Play Preview (Same As Key 3)")]
        public void BeginPreview()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning($"[{nameof(SelectionOverlayPreviewTool)}] Enter Play Mode first.", this);
                return;
            }

            CancelPreview();
            EnsurePerformancesOnSameObject();
            BuildOptions();

            if (optionActors.Count == 0)
            {
                return;
            }

            phase = PreviewPhase.Entering;
            hoveredIndex = -1;

            hoverPerformance?.SetOptions(hoverActors);
            entrancePerformance?.Play(optionActors, OnEntranceFinished);
        }

        [ContextMenu("Cancel Preview")]
        public void CancelPreview()
        {
            phase = PreviewPhase.Idle;
            hoveredIndex = -1;
            pendingFallCount = 0;
            confirmFinished = false;

            entrancePerformance?.StopAndRestore();
            hoverPerformance?.StopAndRestore();
            fallOffPerformance?.StopAndRestore();
            confirmPerformance?.StopAndRestore();

            if (optionsContainer != null)
            {
                optionsContainer.DOKill();
                Destroy(optionsContainer.gameObject);
                optionsContainer = null;
            }

            SelectionOptionVisual.DestroyActors(optionActors);
            hoverActors.Clear();
            optionBaseRotations.Clear();
            optionSortingOrders.Clear();
        }

        private void OnEntranceFinished()
        {
            if (phase != PreviewPhase.Entering)
            {
                return;
            }

            phase = PreviewPhase.Interactive;
        }

        private void BeginDismiss(int selectedIndex)
        {
            if (selectedIndex < 0 || selectedIndex >= optionActors.Count)
            {
                return;
            }

            phase = PreviewPhase.Dismissing;
            hoveredIndex = -1;
            hoverPerformance?.StopAndRestore();

            pendingFallCount = 0;
            confirmFinished = false;

            for (var i = 0; i < optionActors.Count; i++)
            {
                if (i == selectedIndex)
                {
                    continue;
                }

                pendingFallCount++;
                int index = i;
                Transform actor = optionActors[i];
                fallOffPerformance?.Play(
                    actor,
                    index,
                    selectedIndex,
                    optionBaseRotations[index],
                    worldCamera,
                    OnFallFinished);
            }

            if (pendingFallCount == 0)
            {
                OnFallFinished();
            }

            Transform selected = optionActors[selectedIndex];
            confirmPerformance?.Play(
                selected,
                confirmTargetSlot,
                optionSortingOrders[selectedIndex],
                optionActors.Count,
                OnConfirmFinished);
        }

        private void OnFallFinished()
        {
            pendingFallCount = Mathf.Max(0, pendingFallCount - 1);
            TryFinishDismiss();
        }

        private void OnConfirmFinished()
        {
            confirmFinished = true;
            TryFinishDismiss();
        }

        private void TryFinishDismiss()
        {
            if (phase != PreviewPhase.Dismissing || pendingFallCount > 0 || !confirmFinished)
            {
                return;
            }

            Debug.Log($"[{nameof(SelectionOverlayPreviewTool)}] Selection preview finished.", this);
            CancelPreview();
        }

        private void BuildOptions()
        {
            Transform parent = optionsRoot != null ? optionsRoot : transform;
            var containerObject = new GameObject("SelectionOverlayPreview");
            optionsContainer = containerObject.transform;
            optionsContainer.SetParent(parent, false);
            optionsContainer.localPosition = containerLocalOffset;

            int count = Mathf.Max(1, optionCount);
            for (var i = 0; i < count; i++)
            {
                Vector3 localPosition = fanLayout.GetLocalPosition(i, count);
                float rotationZ = fanLayout.GetRotationZ(i);
                int sortingOrder = baseSortingOrder + i;

                Transform actor = SelectionOptionVisual.CreatePreviewCard(
                    optionsContainer,
                    i,
                    cardPreviewPrefab,
                    localPosition,
                    rotationZ,
                    sortingOrder);

                actor.localScale = Vector3.zero;
                optionActors.Add(actor);
                optionBaseRotations.Add(rotationZ);
                optionSortingOrders.Add(sortingOrder);
                hoverActors.Add(new SelectionOptionHoverPerformance.OptionActor(
                    actor,
                    localPosition,
                    rotationZ,
                    sortingOrder));
            }
        }

        private int DetermineHoveredIndex()
        {
            if (worldCamera == null || !TryGetPointerWorld(out Vector3 pointerWorld))
            {
                return -1;
            }

            for (var i = optionActors.Count - 1; i >= 0; i--)
            {
                Transform actor = optionActors[i];
                if (actor == null)
                {
                    continue;
                }

                var collider = actor.GetComponentInChildren<Collider2D>();
                if (collider != null && collider.OverlapPoint(pointerWorld))
                {
                    return i;
                }

                var bounds = ResolveActorBounds(actor);
                if (bounds.HasValue && bounds.Value.Contains(pointerWorld))
                {
                    return i;
                }
            }

            return -1;
        }

        private static Bounds? ResolveActorBounds(Transform actor)
        {
            var renderers = actor.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers.Length == 0)
            {
                return null;
            }

            Bounds bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private bool TryGetPointerWorld(out Vector3 worldPoint)
        {
            worldPoint = Vector3.zero;
            if (worldCamera == null)
            {
                return false;
            }

            Vector3 screen = Input.mousePosition;
            float depth = Mathf.Abs(worldCamera.transform.position.z);
            worldPoint = worldCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
            worldPoint.z = 0f;
            return true;
        }

        private void EnsurePerformancesOnSameObject()
        {
            if (entrancePerformance == null)
            {
                entrancePerformance = GetComponent<SelectionOverlayEntrancePerformance>();
            }

            if (hoverPerformance == null)
            {
                hoverPerformance = GetComponent<SelectionOptionHoverPerformance>();
            }

            if (fallOffPerformance == null)
            {
                fallOffPerformance = GetComponent<SelectionOptionFallOffPerformance>();
            }

            if (confirmPerformance == null)
            {
                confirmPerformance = GetComponent<SelectionOptionConfirmPerformance>();
            }

            if (entrancePerformance == null)
            {
                entrancePerformance = gameObject.AddComponent<SelectionOverlayEntrancePerformance>();
            }

            if (hoverPerformance == null)
            {
                hoverPerformance = gameObject.AddComponent<SelectionOptionHoverPerformance>();
            }

            if (fallOffPerformance == null)
            {
                fallOffPerformance = gameObject.AddComponent<SelectionOptionFallOffPerformance>();
            }

            if (confirmPerformance == null)
            {
                confirmPerformance = gameObject.AddComponent<SelectionOptionConfirmPerformance>();
            }
        }

        private void OnDestroy()
        {
            CancelPreview();
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
            {
                CancelPreview();
            }
        }
    }
}
