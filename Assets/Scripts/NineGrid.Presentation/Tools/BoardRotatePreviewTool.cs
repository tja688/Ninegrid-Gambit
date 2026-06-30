using System.Collections.Generic;
using DG.Tweening;
using NineGrid.Presentation.Flow.Board;
using NineGrid.Presentation.Shared;
using UnityEngine;

namespace NineGrid.Presentation.Tools
{
    /// <summary>
    /// 棋盘旋转预览壳：按 4 生成占位演员，按 5 顺时针跳一格；可在 Inspector 调整演员数量后重新按 4 生成。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardRotatePreviewTool : MonoBehaviour
    {
        [Header("Hotkeys")]
        [SerializeField] private KeyCode spawnKey = KeyCode.Alpha4;
        [SerializeField] private KeyCode rotateKey = KeyCode.Alpha5;

        [Header("Ring")]
        [SerializeField] private Transform ringSlotRoot;
        [SerializeField] private Transform[] ringSlots;
        [Tooltip("当 ringSlots 为空且 ringSlotRoot 为 NineGridAnchors 时，按标准顺时针外圈索引解析。")]
        [SerializeField] private int[] standardBoardRingIndices = { 0, 1, 2, 5, 8, 7, 6, 3 };
        [SerializeField] private bool clockwise = true;

        [Header("Preview Actors")]
        [SerializeField, Min(1)] private int previewActorCount = 8;
        [SerializeField] private Transform previewActorsRoot;
        [SerializeField] private GameObject cardPreviewPrefab;
        [SerializeField] private int baseSortingOrder = 6;

        [Header("Performance")]
        [SerializeField] private BoardRotateFlow boardRotatePerformance;

        private readonly List<Transform> previewActors = new();
        private readonly List<Transform> resolvedRingSlots = new();
        private int ringOffset;

        private void Awake()
        {
            EnsurePerformanceReference();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (Input.GetKeyDown(spawnKey))
            {
                SpawnPreviewActors();
                return;
            }

            if (Input.GetKeyDown(rotateKey))
            {
                PlayOneRotationStep();
            }
        }

        [ContextMenu("Spawn Preview Actors (Same As Key 4)")]
        public void SpawnPreviewActors()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning($"[{nameof(BoardRotatePreviewTool)}] Enter Play Mode first.", this);
                return;
            }

            EnsurePerformanceReference();
            ClearPreviewActors();
            ringOffset = 0;

            if (!TryResolveRingSlots())
            {
                Debug.LogWarning($"[{nameof(BoardRotatePreviewTool)}] ring slots could not be resolved.", this);
                return;
            }

            int count = Mathf.Clamp(previewActorCount, 1, resolvedRingSlots.Count);
            Transform parent = previewActorsRoot != null ? previewActorsRoot : transform;

            for (var i = 0; i < count; i++)
            {
                Transform slot = resolvedRingSlots[i];
                if (slot == null)
                {
                    continue;
                }

                Transform actor = CreatePreviewActor(parent, i);
                actor.position = slot.position;
                actor.localScale = Vector3.one;
                previewActors.Add(actor);
            }
        }

        [ContextMenu("Play One Rotation Step (Same As Key 5)")]
        public void PlayOneRotationStep()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning($"[{nameof(BoardRotatePreviewTool)}] Enter Play Mode first.", this);
                return;
            }

            if (previewActors.Count == 0)
            {
                Debug.LogWarning($"[{nameof(BoardRotatePreviewTool)}] No preview actors. Press {spawnKey} first.", this);
                return;
            }

            if (!TryResolveRingSlots())
            {
                return;
            }

            if (boardRotatePerformance.IsPlaying)
            {
                return;
            }

            EnsurePerformanceReference();

            int ringCount = resolvedRingSlots.Count;
            var targets = new Transform[previewActors.Count];
            int direction = clockwise ? 1 : -1;
            for (var i = 0; i < previewActors.Count; i++)
            {
                int slotIndex = (i + ringOffset + direction + ringCount) % ringCount;
                targets[i] = resolvedRingSlots[slotIndex];
            }

            boardRotatePerformance.Play(previewActors, targets);
            ringOffset = (ringOffset + direction + ringCount) % ringCount;
        }

        [ContextMenu("Clear Preview Actors")]
        public void ClearPreviewActors()
        {
            boardRotatePerformance?.StopAndRestore();

            for (var i = previewActors.Count - 1; i >= 0; i--)
            {
                Transform actor = previewActors[i];
                if (actor == null)
                {
                    continue;
                }

                actor.DOKill();
                Destroy(actor.gameObject);
            }

            previewActors.Clear();
            ringOffset = 0;
        }

        private bool TryResolveRingSlots()
        {
            resolvedRingSlots.Clear();

            if (ringSlots != null && ringSlots.Length > 0)
            {
                for (var i = 0; i < ringSlots.Length; i++)
                {
                    if (ringSlots[i] != null)
                    {
                        resolvedRingSlots.Add(ringSlots[i]);
                    }
                }

                return resolvedRingSlots.Count > 0;
            }

            if (ringSlotRoot == null)
            {
                return false;
            }

            if (standardBoardRingIndices != null && standardBoardRingIndices.Length > 0)
            {
                for (var i = 0; i < standardBoardRingIndices.Length; i++)
                {
                    int childIndex = standardBoardRingIndices[i];
                    if (childIndex < 0 || childIndex >= ringSlotRoot.childCount)
                    {
                        continue;
                    }

                    resolvedRingSlots.Add(ringSlotRoot.GetChild(childIndex));
                }

                if (resolvedRingSlots.Count > 0)
                {
                    return true;
                }
            }

            for (var i = 0; i < ringSlotRoot.childCount; i++)
            {
                resolvedRingSlots.Add(ringSlotRoot.GetChild(i));
            }

            return resolvedRingSlots.Count > 0;
        }

        private Transform CreatePreviewActor(Transform parent, int index)
        {
            Transform actor = SelectionOptionVisual.CreatePreviewCard(
                parent,
                index,
                cardPreviewPrefab,
                Vector3.zero,
                0f,
                baseSortingOrder + index);
            return actor;
        }

        private void EnsurePerformanceReference()
        {
            if (boardRotatePerformance == null)
            {
                boardRotatePerformance = GetComponent<BoardRotateFlow>();
            }

            if (boardRotatePerformance == null)
            {
                boardRotatePerformance = gameObject.AddComponent<BoardRotateFlow>();
            }
        }

        private void OnDestroy()
        {
            ClearPreviewActors();
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
            {
                boardRotatePerformance?.StopAndRestore();
            }
        }
    }
}
