using System;
using System.Collections.Generic;
using DG.Tweening;
using NineGrid.Battle.Combat;
using NineGrid.GameFlow;
using NineGrid.UI;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NineGrid.GameFlow
{
    /// <summary>
    /// 锻造台材料布置：拖拽、三台磁悬浮堆叠、桌面取回。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HydraulicMaterialBoard : MonoBehaviour
    {
        const int MaxPerAnvil = 10;

        [System.Serializable]
        sealed class AnvilStation
        {
            public Transform surface;
            public Collider2D zone;
            [System.NonSerialized] public List<HydraulicMaterialPiece> stack;

            public List<HydraulicMaterialPiece> Stack =>
                stack ??= new List<HydraulicMaterialPiece>(MaxPerAnvil);
        }

        [Header("Refs")]
        [SerializeField] HydraulicMaterialLane lane;
        [SerializeField] Transform sceneRoot;
        [SerializeField] Camera worldCamera;
        [SerializeField] AnvilStation[] anvils = new AnvilStation[3];

        [Header("Layout")]
        [SerializeField] float anvilBaseLift = 0.35f;
        [SerializeField] float anvilTopPadding = 0.2f;
        [SerializeField] float preferredStackSpacing = 0.28f;
        [SerializeField] float rearrangeDuration = 0.28f;
        [SerializeField] Ease rearrangeEase = Ease.OutCubic;
        [SerializeField] int dragSortingOrder = 40;

        [Header("Input")]
        [SerializeField] bool ignoreWhenPointerOverUi = true;

        HydraulicMaterialPiece _dragged;
        HydraulicMaterialPiece _hovered;
        HydraulicMaterialPiece _hoverVisualPiece;
        HydraulicMaterialHome _dragOriginHome;
        int _dragOriginTableSlot = -1;
        int _dragOriginAnvil = -1;
        int _dragOriginStack = -1;
        Vector3 _dragOriginPosition;
        Vector3 _dragOriginScale;
        Vector3 _dragOffset;

        public event Action DragBegun;
        public event Action<CardInstance, int> PlacedOnAnvil;
        public event Action AnvilRelayouted;
        public event Action PlacedOnTable;
        public event Action DragCancelled;
        public event Action AnvilFullRejected;

        public bool IsDragging => _dragged != null;

        /// <summary>当前被拖拽的矿石数据（null=未拖拽）。</summary>
        public CardInstance DraggedCard => _dragged?.Card;

        /// <summary>鼠标悬停的可交互矿石（未拖拽时）。</summary>
        public HydraulicMaterialPiece HoveredPiece => _hovered;

        /// <summary>获取三个砧台上的矿石牌组（按堆叠顺序，底→顶）。供伤害计算读取。</summary>
        public List<CardInstance>[] GetAnvilStacks()
        {
            var result = new List<CardInstance>[anvils != null ? anvils.Length : 0];
            for (var i = 0; i < result.Length; i++)
            {
                var list = new List<CardInstance>();
                var stack = anvils[i]?.Stack;
                if (stack != null)
                {
                    for (var j = 0; j < stack.Count; j++)
                    {
                        if (stack[j] != null && stack[j].Card != null)
                            list.Add(stack[j].Card);
                    }
                }
                result[i] = list;
            }
            return result;
        }

        /// <summary>获取桌面未上砧的矿石列表。供锻造提交时一并消耗。</summary>
        public List<CardInstance> GetTableCards()
        {
            var result = new List<CardInstance>();
            if (lane == null) return result;
            var pieces = lane.Pieces;
            if (pieces == null) return result;
            for (var i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                if (p != null && p.Home == HydraulicMaterialHome.Table && p.Card != null)
                    result.Add(p.Card);
            }
            return result;
        }

        /// <summary>锻造台数量（通常 3：左/中/右）。</summary>
        public int AnvilCount => anvils != null ? anvils.Length : 0;

        /// <summary>指定锻造台上的材料数量。</summary>
        public int GetPieceCount(int anvilIndex)
        {
            if (anvils == null || anvilIndex < 0 || anvilIndex >= anvils.Length)
            {
                return 0;
            }

            return anvils[anvilIndex]?.Stack.Count ?? 0;
        }

        /// <summary>任一锻造台上是否有材料（决定「开始锻造」是否可点）。</summary>
        public bool HasAnyMaterial()
        {
            if (anvils == null)
            {
                return false;
            }

            for (var i = 0; i < anvils.Length; i++)
            {
                if (GetPieceCount(i) > 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 填充各锻造台是否有材料的占用表（长度不足时按能填的填），返回占用的锻造台数量。
        /// 供铸造完成后按台亮出对应钻头。
        /// </summary>
        public int GetOccupancy(bool[] result)
        {
            if (result == null)
            {
                return 0;
            }

            var occupied = 0;
            for (var i = 0; i < result.Length; i++)
            {
                var has = GetPieceCount(i) > 0;
                result[i] = has;
                if (has)
                {
                    occupied++;
                }
            }

            return occupied;
        }

        void Awake()
        {
            ResolveRefs();
        }

        void Update()
        {
            var scene = HydraulicSceneController.Instance;
            if (scene == null || !scene.IsActive)
            {
                if (_dragged != null)
                {
                    CancelDragToOrigin();
                }

                return;
            }

            if (_dragged != null)
            {
                _hovered = null;
                TickDrag();
                if (WasPointerReleased())
                {
                    EndDrag();
                }

                return;
            }

            UpdateHoveredPiece();

            if (FlowInput.TryGameplayClick() && TryGetPointerWorld(out var world))
            {
                if (ignoreWhenPointerOverUi && IsPointerOverUi())
                {
                    return;
                }

                var piece = lane != null ? lane.FindInteractableAt(world) : null;
                if (piece != null)
                {
                    BeginDrag(piece, world);
                }
            }
        }

        void LateUpdate()
        {
            // 压过 SceneElementPointerSelector，拖拽中保持 outline。
            if (_dragged != null)
            {
                _dragged.SetSelected(true);
            }
        }

        void OnDestroy()
        {
            if (_dragged != null)
            {
                _dragged.KillMotion();
                _dragged = null;
            }
        }

        public void ResetBoard()
        {
            if (_dragged != null)
            {
                _dragged.KillMotion();
                _dragged = null;
            }

            if (anvils == null)
            {
                return;
            }

            for (var i = 0; i < anvils.Length; i++)
            {
                anvils[i]?.Stack.Clear();
            }
        }

        void UpdateHoveredPiece()
        {
            if (ignoreWhenPointerOverUi && IsPointerOverUi())
            {
                SetHoverVisual(null);
                _hovered = null;
                return;
            }

            if (!TryGetPointerWorld(out var world))
            {
                SetHoverVisual(null);
                _hovered = null;
                return;
            }

            _hovered = lane != null ? lane.FindInteractableAt(world) : null;
            SetHoverVisual(_hovered);
        }

        void SetHoverVisual(HydraulicMaterialPiece piece)
        {
            if (_hoverVisualPiece == piece)
            {
                return;
            }

            if (_hoverVisualPiece != null && _hoverVisualPiece != _dragged)
            {
                _hoverVisualPiece.SetSelected(false);
            }

            _hoverVisualPiece = piece;

            if (_hoverVisualPiece != null && _hoverVisualPiece != _dragged)
            {
                _hoverVisualPiece.SetSelected(true);
            }
        }

        void BeginDrag(HydraulicMaterialPiece piece, Vector3 pointerWorld)
        {
            DragBegun?.Invoke();
            _dragged = piece;
            _dragOriginHome = piece.Home;
            _dragOriginTableSlot = piece.TableSlot;
            _dragOriginAnvil = piece.AnvilIndex;
            _dragOriginStack = piece.StackIndex;
            _dragOriginPosition = piece.RestPosition;
            _dragOriginScale = piece.transform.localScale;
            _dragOffset = piece.transform.position - pointerWorld;

            if (piece.Home == HydraulicMaterialHome.Table)
            {
                lane.ReleaseTableSlot(piece.TableSlot);
            }
            else if (piece.Home == HydraulicMaterialHome.Anvil)
            {
                RemoveFromAnvil(piece.AnvilIndex, piece);
                RelayoutAnvil(piece.AnvilIndex, animate: true);
            }

            piece.BeginDrag(dragSortingOrder);
        }

        void TickDrag()
        {
            if (_dragged == null || !TryGetPointerWorld(out var world))
            {
                return;
            }

            _dragged.FollowPointer(world + _dragOffset);
            _dragged.SetSelected(true);
        }

        void EndDrag()
        {
            if (_dragged == null)
            {
                return;
            }

            var piece = _dragged;
            _dragged = null;
            piece.SetSelected(false);

            if (!TryGetPointerWorld(out var world))
            {
                world = piece.transform.position;
            }

            var dropPoint = world + _dragOffset;
            var anvilIndex = FindAnvilAt(dropPoint);

            if (anvilIndex >= 0)
            {
                if (TryPlaceOnAnvil(piece, anvilIndex, dropPoint.y))
                {
                    return;
                }

                // 目标台已满：若来自该台则插回，否则回原位。
                ReturnToOrigin(piece);
                return;
            }

            // 未落在锻造台：尝试回桌面。
            if (lane != null && lane.TryPlaceOnFreeTableSlot(piece))
            {
                PlacedOnTable?.Invoke();
                return;
            }

            ReturnToOrigin(piece);
        }

        void CancelDragToOrigin()
        {
            if (_dragged == null)
            {
                return;
            }

            var piece = _dragged;
            _dragged = null;
            piece.SetSelected(false);
            DragCancelled?.Invoke();
            ReturnToOrigin(piece);
        }

        void ReturnToOrigin(HydraulicMaterialPiece piece)
        {
            if (piece == null)
            {
                return;
            }

            if (_dragOriginHome == HydraulicMaterialHome.Anvil && _dragOriginAnvil >= 0)
            {
                if (TryPlaceOnAnvil(piece, _dragOriginAnvil, _dragOriginPosition.y))
                {
                    return;
                }
            }

            if (_dragOriginHome == HydraulicMaterialHome.Table && lane != null)
            {
                if (_dragOriginTableSlot >= 0 && lane.TryPlaceOnTableSlot(piece, _dragOriginTableSlot))
                {
                    return;
                }

                if (lane.TryPlaceOnFreeTableSlot(piece))
                {
                    return;
                }
            }

            // 兜底：尽量回锻造台原位，否则藏回池。
            if (_dragOriginAnvil >= 0 && TryPlaceOnAnvil(piece, _dragOriginAnvil, _dragOriginPosition.y))
            {
                return;
            }

            piece.SetPoolHidden(lane != null ? lane.TableScale : _dragOriginScale);
        }

        bool TryPlaceOnAnvil(HydraulicMaterialPiece piece, int anvilIndex, float dropY)
        {
            if (piece == null || anvils == null || anvilIndex < 0 || anvilIndex >= anvils.Length)
            {
                return false;
            }

            var anvil = anvils[anvilIndex];
            if (anvil == null || anvil.surface == null)
            {
                return false;
            }

            var stack = anvil.Stack;

            // 已在该台列表中则先移除再插入。
            stack.Remove(piece);

            if (stack.Count >= MaxPerAnvil)
            {
                AnvilFullRejected?.Invoke();
                return false;
            }

            var insertAt = stack.Count;
            for (var i = 0; i < stack.Count; i++)
            {
                var other = stack[i];
                if (other != null && dropY < other.RestPosition.y)
                {
                    insertAt = i;
                    break;
                }
            }

            stack.Insert(insertAt, piece);
            PlacedOnAnvil?.Invoke(piece.Card, anvilIndex);
            RelayoutAnvil(anvilIndex, animate: true);
            return true;
        }

        /// <summary>衍生物（碎屑矿渣等）直接叠放到指定铸造台顶，不走管道。</summary>
        public bool TrySpawnDerivedOnAnvil(CardInstance card, int anvilIndex)
        {
            if (card == null || anvils == null || anvilIndex < 0 || anvilIndex >= anvils.Length)
            {
                return false;
            }

            var anvil = anvils[anvilIndex];
            if (anvil == null || anvil.surface == null || lane == null)
            {
                return false;
            }

            var stack = anvil.Stack;
            if (stack.Count >= MaxPerAnvil)
            {
                return false;
            }

            var piece = lane.TryTakePooledPiece();
            if (piece == null)
            {
                return false;
            }

            piece.BindCard(card);
            stack.Add(piece);
            return true;
        }

        void RemoveFromAnvil(int anvilIndex, HydraulicMaterialPiece piece)
        {
            if (anvils == null || anvilIndex < 0 || anvilIndex >= anvils.Length)
            {
                return;
            }

            anvils[anvilIndex]?.Stack.Remove(piece);
        }

        void RelayoutAnvil(int anvilIndex, bool animate)
        {
            if (anvils == null || anvilIndex < 0 || anvilIndex >= anvils.Length)
            {
                return;
            }

            var anvil = anvils[anvilIndex];
            if (anvil == null || anvil.surface == null)
            {
                return;
            }

            var stack = anvil.Stack;

            // 清掉空引用。
            for (var i = stack.Count - 1; i >= 0; i--)
            {
                if (stack[i] == null)
                {
                    stack.RemoveAt(i);
                }
            }

            var count = stack.Count;
            if (count == 0)
            {
                return;
            }

            var minY = anvil.surface.position.y + anvilBaseLift;
            var maxY = minY + preferredStackSpacing * (MaxPerAnvil - 1);
            if (anvil.zone != null)
            {
                maxY = anvil.zone.bounds.max.y - anvilTopPadding;
            }

            var span = Mathf.Max(0.01f, maxY - minY);
            var spacing = count <= 1
                ? 0f
                : Mathf.Min(preferredStackSpacing, span / (count - 1));

            var anvilScale = lane != null ? lane.AnvilScale : Vector3.one * 0.2f;
            var duration = animate ? rearrangeDuration : 0f;

            for (var i = 0; i < count; i++)
            {
                var piece = stack[i];
                if (piece == null)
                {
                    continue;
                }

                var rest = new Vector3(anvil.surface.position.x, minY + spacing * i, 0f);
                piece.PlaceOnAnvil(anvilIndex, i, rest, anvilScale, duration, rearrangeEase, enableBob: true);
            }

            AnvilRelayouted?.Invoke();
        }

        int FindAnvilAt(Vector2 worldPoint)
        {
            if (anvils == null)
            {
                return -1;
            }

            for (var i = 0; i < anvils.Length; i++)
            {
                var anvil = anvils[i];
                if (anvil?.zone != null && anvil.zone.enabled && anvil.zone.OverlapPoint(worldPoint))
                {
                    return i;
                }
            }

            return -1;
        }

        void ResolveRefs()
        {
            if (sceneRoot == null)
            {
                sceneRoot = transform;
            }

            if (lane == null)
            {
                lane = GetComponent<HydraulicMaterialLane>()
                    ?? GetComponentInChildren<HydraulicMaterialLane>(true);
            }

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            EnsureAnvil(0, "锻造台台面左");
            EnsureAnvil(1, "锻造台台面中");
            EnsureAnvil(2, "锻造台台面右");
        }

        void EnsureAnvil(int index, string objectName)
        {
            if (anvils == null || anvils.Length < 3)
            {
                anvils = new AnvilStation[3];
            }

            if (anvils[index] == null)
            {
                anvils[index] = new AnvilStation();
            }

            if (anvils[index].surface == null)
            {
                anvils[index].surface = FindDeepChild(sceneRoot, objectName);
            }

            if (anvils[index].zone == null && anvils[index].surface != null)
            {
                anvils[index].zone = anvils[index].surface.GetComponent<Collider2D>();
            }
        }

        bool TryGetPointerWorld(out Vector3 world)
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (worldCamera == null)
            {
                world = default;
                return false;
            }

            var screen = GetPointerScreenPosition();
            if (float.IsNaN(screen.x) || float.IsNaN(screen.y))
            {
                world = default;
                return false;
            }

            var depth = Mathf.Abs(worldCamera.transform.position.z);
            world = worldCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
            world.z = 0f;
            return true;
        }

        static Vector2 GetPointerScreenPosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }
#endif
            return Input.mousePosition;
        }

        static bool WasPointerPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.leftButton.wasPressedThisFrame;
            }
#endif
            return Input.GetMouseButtonDown(0);
        }

        static bool WasPointerReleased()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.leftButton.wasReleasedThisFrame;
            }
#endif
            return Input.GetMouseButtonUp(0);
        }

        static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
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
