using System.Collections.Generic;
using DG.Tweening;
using NineGrid.Presentation.Visuals;
using UnityEngine;

namespace NineGrid.GameFlow
{
    /// <summary>
    /// 液压场景材料滑道：管道倾倒、10 桌面槽位、材料池。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HydraulicMaterialLane : MonoBehaviour
    {
        public const int TableSlotCount = 10;

        [Header("Refs")]
        [SerializeField] Transform sceneRoot;
        [SerializeField] Transform pipeMask;
        [SerializeField] Transform spawnPoint;
        [SerializeField] Transform pipeExitPoint;
        [SerializeField] Transform slotStart;
        [SerializeField] Transform slotEnd;
        [SerializeField] Transform materialsRoot;
        [SerializeField] HydraulicMaterialPiece[] pieces;
        [SerializeField] Sprite[] materialSprites;
        [SerializeField] Material outlineMaterial;

        [Header("Motion")]
        [SerializeField] float pipeSlideDuration = 0.55f;
        [SerializeField] float platformSlideDuration = 0.7f;
        [SerializeField] Ease pipeSlideEase = Ease.InQuad;
        [SerializeField] Ease platformSlideEase = Ease.OutCubic;
        [SerializeField] Vector3 tableScale = new Vector3(0.325f, 0.325f, 0.325f);
        [SerializeField] Vector3 anvilScale = new Vector3(0.21f, 0.21f, 0.21f);

        [Header("Visual")]
        [SerializeField] string sortingLayerName = "Factory";
        [SerializeField] int sortingOrder = 2;
        [SerializeField] Color materialTint = new Color(0.635f, 0.506f, 0.435f, 1f);

        readonly List<Tween> _activeTweens = new List<Tween>(8);
        readonly bool[] _tableOccupied = new bool[TableSlotCount];
        Vector3[] _slotPositions;
        bool _laneReady;

        public Vector3 TableScale => tableScale;
        public Vector3 AnvilScale => anvilScale;

        public int SettledOnTableCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < _tableOccupied.Length; i++)
                {
                    if (_tableOccupied[i])
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public bool CanDeliver
        {
            get
            {
                if (!_laneReady || pieces == null)
                {
                    return false;
                }

                return FindFreePieceIndex() >= 0 && FindFreeTableSlot() >= 0;
            }
        }

        void Awake()
        {
            ResolveRefs();
            SetupPipeMask();
            CacheSlots();
            EnsurePiecePool();
            _laneReady = true;
            ResetLane();
        }

        void OnDestroy()
        {
            KillTweens();
        }

        public void ResetLane()
        {
            KillTweens();

            for (var i = 0; i < _tableOccupied.Length; i++)
            {
                _tableOccupied[i] = false;
            }

            if (pieces == null)
            {
                return;
            }

            for (var i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] != null)
                {
                    pieces[i].SetPoolHidden(tableScale);
                }
            }
        }

        public bool TryDeliver()
        {
            EnsureReady();

            if (spawnPoint == null || pipeExitPoint == null || pieces == null)
            {
                return false;
            }

            var pieceIndex = FindFreePieceIndex();
            var slotIndex = FindFreeTableSlot();
            if (pieceIndex < 0 || slotIndex < 0)
            {
                return false;
            }

            var piece = pieces[pieceIndex];
            if (piece == null)
            {
                return false;
            }

            var slot = GetSlotPosition(slotIndex);
            _tableOccupied[slotIndex] = true;

            piece.BeginSlide(spawnPoint.position, tableScale);
            piece.SetMaskInteraction(SpriteMaskInteraction.VisibleOutsideMask);

            var captured = piece;
            var capturedSlot = slotIndex;
            var sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Append(captured.transform.DOMove(pipeExitPoint.position, pipeSlideDuration).SetEase(pipeSlideEase));
            sequence.AppendCallback(() =>
            {
                if (captured != null)
                {
                    captured.SetMaskInteraction(SpriteMaskInteraction.None);
                }
            });
            sequence.Append(captured.transform.DOMove(slot, platformSlideDuration).SetEase(platformSlideEase));
            sequence.OnComplete(() =>
            {
                if (captured == null)
                {
                    return;
                }

                captured.SnapToTable(capturedSlot, slot, tableScale);
                captured.SetMaskInteraction(SpriteMaskInteraction.None);
            });

            _activeTweens.Add(sequence);
            return true;
        }

        public HydraulicMaterialPiece FindInteractableAt(Vector2 worldPoint)
        {
            if (pieces == null)
            {
                return null;
            }

            HydraulicMaterialPiece best = null;
            var bestOrder = int.MinValue;

            for (var i = 0; i < pieces.Length; i++)
            {
                var piece = pieces[i];
                if (piece == null || !piece.isActiveAndEnabled || !piece.IsInteractable)
                {
                    continue;
                }

                var selectable = piece.Selectable;
                if (selectable == null || !selectable.ContainsWorldPoint(worldPoint))
                {
                    var col = piece.GetComponent<Collider2D>();
                    if (col == null || !col.OverlapPoint(worldPoint))
                    {
                        continue;
                    }
                }

                var order = piece.SpriteRenderer != null ? piece.SpriteRenderer.sortingOrder : 0;
                if (best == null || order >= bestOrder)
                {
                    best = piece;
                    bestOrder = order;
                }
            }

            return best;
        }

        public void ReleaseTableSlot(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < _tableOccupied.Length)
            {
                _tableOccupied[slotIndex] = false;
            }
        }

        public bool TryPlaceOnFreeTableSlot(HydraulicMaterialPiece piece)
        {
            var slot = FindFreeTableSlot();
            return slot >= 0 && TryPlaceOnTableSlot(piece, slot);
        }

        public bool TryPlaceOnTableSlot(HydraulicMaterialPiece piece, int slotIndex)
        {
            if (piece == null || slotIndex < 0 || slotIndex >= TableSlotCount)
            {
                return false;
            }

            if (_tableOccupied[slotIndex])
            {
                return false;
            }

            _tableOccupied[slotIndex] = true;
            piece.SettleOnTable(slotIndex, GetSlotPosition(slotIndex), tableScale, 0.22f, Ease.OutCubic);
            piece.SetMaskInteraction(SpriteMaskInteraction.None);
            return true;
        }

        public Vector3 GetSlotPosition(int index)
        {
            if (_slotPositions != null && index >= 0 && index < _slotPositions.Length)
            {
                return _slotPositions[index];
            }

            return pipeExitPoint != null ? pipeExitPoint.position : Vector3.zero;
        }

        void EnsureReady()
        {
            if (_laneReady)
            {
                return;
            }

            ResolveRefs();
            SetupPipeMask();
            CacheSlots();
            EnsurePiecePool();
            _laneReady = true;
        }

        void ResolveRefs()
        {
            if (sceneRoot == null)
            {
                sceneRoot = transform;
            }

            if (pipeMask == null)
            {
                pipeMask = FindDeepChild(sceneRoot, "管道遮盖图");
            }

            if (spawnPoint == null)
            {
                spawnPoint = FindDeepChild(sceneRoot, "MaterialSpawn");
            }

            if (pipeExitPoint == null)
            {
                pipeExitPoint = FindDeepChild(sceneRoot, "MaterialPipeExit");
            }

            if (slotStart == null)
            {
                slotStart = FindDeepChild(sceneRoot, "MaterialSlot_0");
            }

            if (slotEnd == null)
            {
                slotEnd = FindDeepChild(sceneRoot, "MaterialSlot_9");
            }

            if (materialsRoot == null)
            {
                var existing = FindDeepChild(sceneRoot, "Materials");
                if (existing != null)
                {
                    materialsRoot = existing;
                }
                else
                {
                    var go = new GameObject("Materials");
                    go.transform.SetParent(sceneRoot, false);
                    materialsRoot = go.transform;
                }
            }
        }

        void CacheSlots()
        {
            _slotPositions = new Vector3[TableSlotCount];
            var start = slotStart != null
                ? slotStart.position
                : new Vector3(-3.9375f, -3.47f, 0f);
            var end = slotEnd != null
                ? slotEnd.position
                : new Vector3(3.4375f, -3.47f, 0f);

            for (var i = 0; i < TableSlotCount; i++)
            {
                var t = TableSlotCount == 1 ? 0f : i / (float)(TableSlotCount - 1);
                _slotPositions[i] = Vector3.Lerp(start, end, t);
            }

            // 同步中间槽位标记，便于 Scene 里预览。
            SyncSlotMarkers(start, end);
        }

        void SyncSlotMarkers(Vector3 start, Vector3 end)
        {
            for (var i = 0; i < TableSlotCount; i++)
            {
                var name = $"MaterialSlot_{i}";
                var marker = FindDeepChild(sceneRoot, name);
                if (marker == null)
                {
                    var go = new GameObject(name);
                    go.transform.SetParent(sceneRoot, false);
                    marker = go.transform;
                }

                var t = TableSlotCount == 1 ? 0f : i / (float)(TableSlotCount - 1);
                marker.position = Vector3.Lerp(start, end, t);
            }
        }

        void EnsurePiecePool()
        {
            if (pieces != null && pieces.Length == TableSlotCount && !HasNullPiece())
            {
                for (var i = 0; i < pieces.Length; i++)
                {
                    ConfigurePiece(pieces[i], i);
                }

                return;
            }

            var list = new List<HydraulicMaterialPiece>(TableSlotCount);
            if (pieces != null)
            {
                for (var i = 0; i < pieces.Length; i++)
                {
                    if (pieces[i] != null)
                    {
                        list.Add(pieces[i]);
                    }
                }
            }

            // 回收旧 material 命名物体。
            CollectLegacyMaterials(list);

            while (list.Count < TableSlotCount)
            {
                list.Add(CreatePiece(list.Count));
            }

            pieces = list.ToArray();
            for (var i = 0; i < pieces.Length; i++)
            {
                ConfigurePiece(pieces[i], i);
            }
        }

        bool HasNullPiece()
        {
            for (var i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] == null)
                {
                    return true;
                }
            }

            return false;
        }

        void CollectLegacyMaterials(List<HydraulicMaterialPiece> list)
        {
            if (sceneRoot == null)
            {
                return;
            }

            for (var i = 0; i < sceneRoot.childCount; i++)
            {
                var child = sceneRoot.GetChild(i);
                if (child == null || !child.name.StartsWith("material"))
                {
                    continue;
                }

                var piece = child.GetComponent<HydraulicMaterialPiece>();
                if (piece == null)
                {
                    piece = child.gameObject.AddComponent<HydraulicMaterialPiece>();
                }

                if (!list.Contains(piece))
                {
                    list.Add(piece);
                }
            }
        }

        HydraulicMaterialPiece CreatePiece(int index)
        {
            var go = new GameObject($"material_{index}");
            go.transform.SetParent(materialsRoot != null ? materialsRoot : sceneRoot, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = ResolveSprite(index);
            renderer.color = materialTint;
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = sortingOrder;

            var piece = go.AddComponent<HydraulicMaterialPiece>();
            return piece;
        }

        void ConfigurePiece(HydraulicMaterialPiece piece, int index)
        {
            if (piece == null)
            {
                return;
            }

            piece.transform.SetParent(materialsRoot != null ? materialsRoot : sceneRoot, true);
            piece.name = $"material_{index}";
            piece.ConfigureSelectable();

            var renderer = piece.SpriteRenderer;
            if (renderer != null)
            {
                if (renderer.sprite == null)
                {
                    renderer.sprite = ResolveSprite(index);
                }

                renderer.color = materialTint;
                renderer.sortingLayerName = sortingLayerName;
                renderer.sortingOrder = sortingOrder;
            }

            var selectable = piece.Selectable;
            if (selectable != null)
            {
                selectable.ConfigureOutline(outlineMaterial, widthPixels: 2);
            }
        }

        Sprite ResolveSprite(int index)
        {
            if (materialSprites != null && materialSprites.Length > 0)
            {
                return materialSprites[index % materialSprites.Length];
            }

            // 回退：尝试从已有 SpriteRenderer 上拿。
            if (pieces != null)
            {
                for (var i = 0; i < pieces.Length; i++)
                {
                    if (pieces[i] != null && pieces[i].SpriteRenderer != null && pieces[i].SpriteRenderer.sprite != null)
                    {
                        return pieces[i].SpriteRenderer.sprite;
                    }
                }
            }

            return null;
        }

        int FindFreePieceIndex()
        {
            if (pieces == null)
            {
                return -1;
            }

            for (var i = 0; i < pieces.Length; i++)
            {
                var piece = pieces[i];
                if (piece != null && piece.Home == HydraulicMaterialHome.Pool)
                {
                    return i;
                }
            }

            return -1;
        }

        int FindFreeTableSlot()
        {
            for (var i = 0; i < _tableOccupied.Length; i++)
            {
                if (!_tableOccupied[i])
                {
                    return i;
                }
            }

            return -1;
        }

        void SetupPipeMask()
        {
            if (pipeMask == null)
            {
                return;
            }

            var renderer = pipeMask.GetComponent<SpriteRenderer>();
            Sprite maskSprite = null;
            if (renderer != null)
            {
                maskSprite = renderer.sprite;
                renderer.enabled = false;
            }

            var mask = pipeMask.GetComponent<SpriteMask>();
            if (mask == null)
            {
                mask = pipeMask.gameObject.AddComponent<SpriteMask>();
            }

            if (maskSprite != null)
            {
                mask.sprite = maskSprite;
            }

            mask.alphaCutoff = 0.2f;
            mask.isCustomRangeActive = false;
            mask.frontSortingLayerID = SortingLayer.NameToID(sortingLayerName);
            mask.backSortingLayerID = SortingLayer.NameToID(sortingLayerName);
        }

        void KillTweens()
        {
            for (var i = 0; i < _activeTweens.Count; i++)
            {
                var tween = _activeTweens[i];
                if (tween != null && tween.IsActive())
                {
                    tween.Kill();
                }
            }

            _activeTweens.Clear();

            if (pieces == null)
            {
                return;
            }

            for (var i = 0; i < pieces.Length; i++)
            {
                pieces[i]?.KillMotion();
            }
        }

        static Transform FindDeepChild(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            var direct = root.Find(childName);
            if (direct != null)
            {
                return direct;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }

                var nested = FindDeepChild(child, childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }
    }
}
