using System;
using System.Collections.Generic;
using NineGrid.Battle.Combat;
using NineGrid.Data;
using UnityEngine;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NineGrid.GameFlow
{
    /// <summary>
    /// 矿石库（矿仓）面板：展示矿舱内容；事件/商店流程中可进入「点选矿石」模式。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OreInventoryPanel : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("面板根物体。留空取自身。")]
        [SerializeField] GameObject panelRoot;
        [Tooltip("用于判断点击是否落在面板内的碰撞盒（可选）。留空则按任意外部点击关闭。")]
        [SerializeField] Collider2D panelBounds;
        [SerializeField] Camera worldCamera;
        [Tooltip("矿仓矿石列表文本（留空时自动查找子级 TMP_Text）。")]
        [SerializeField] TMP_Text oreListText;
        [Tooltip("矿石槽位根节点。留空时在面板根下查找 solt 子物体。")]
        [SerializeField] Transform oreSlotsRoot;

        readonly List<(Transform slot, SpriteRenderer renderer, BoxCollider2D collider)> _slots = new();
        readonly List<int> _slotDeckIndices = new();

        int _openedFrame = -1;
        bool _selectionMode;
        bool _allowOutsideClose = true;
        Action<int> _onOreSelected;
        Action _onSelectionCancelled;

        public event Action Opened;
        public event Action Closed;

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;
        public bool IsSelectionMode => _selectionMode;

        /// <summary>悬停槽位时返回矿舱 deck 索引；未悬停或槽位为空时返回 false。</summary>
        public bool TryGetHoveredDeckIndex(out int deckIndex)
        {
            deckIndex = -1;
            if (!IsOpen || !TryGetPointerWorldPosition(out var worldPoint))
            {
                return false;
            }

            CacheSlots();
            for (var i = 0; i < _slots.Count; i++)
            {
                var (_, _, collider) = _slots[i];
                if (collider == null || !collider.enabled || !collider.OverlapPoint(worldPoint))
                {
                    continue;
                }

                if (i >= _slotDeckIndices.Count)
                {
                    return false;
                }

                deckIndex = _slotDeckIndices[i];
                return true;
            }

            return false;
        }

        void Awake()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            if (panelBounds == null)
            {
                panelBounds = GetComponent<Collider2D>();
            }

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            CacheSlots();
            SetActive(false);
        }

        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                OpenView();
            }
        }

        /// <summary>只读浏览矿舱（战斗内读 CombatModel，否则读 RunData）。</summary>
        public void Open() => OpenView();

        /// <summary>只读浏览矿舱（战斗内读 CombatModel，否则读 RunData）。</summary>
        public void OpenView()
        {
            _selectionMode = false;
            _allowOutsideClose = true;
            _onOreSelected = null;
            _onSelectionCancelled = null;
            OpenInternal(PopulateForView);
        }

        /// <summary>事件选矿：点击槽位后回调 deck 索引。</summary>
        public void BeginSelection(Action<int> onSelected, Action onCancelled = null, string prompt = null)
        {
            TryBeginSelection(onSelected, onCancelled, prompt);
        }

        /// <summary>进入选矿模式；矿舱为空或无可点槽位时返回 false。</summary>
        public bool TryBeginSelection(Action<int> onSelected, Action onCancelled = null, string prompt = null)
        {
            if (onSelected == null)
            {
                return false;
            }

            var run = RunData.Ensure();
            if (run.Deck.Count == 0)
            {
                return false;
            }

            _selectionMode = true;
            _allowOutsideClose = onCancelled != null;
            _onOreSelected = onSelected;
            _onSelectionCancelled = onCancelled;

            if (!string.IsNullOrEmpty(prompt) && oreListText != null)
            {
                oreListText.text = prompt;
            }

            OpenInternal(PopulateForSelection);

            if (_slotDeckIndices.Count == 0)
            {
                AbortSelectionSilently();
                return false;
            }

            return IsOpen && IsSelectionMode;
        }

        /// <summary>强制关闭选矿面板，不触发取消回调（调试 PASS 等）。</summary>
        public void AbortSelectionSilently()
        {
            _selectionMode = false;
            _allowOutsideClose = true;
            _onOreSelected = null;
            _onSelectionCancelled = null;
            SetActive(false);
            Closed?.Invoke();
        }

        public void Close()
        {
            if (_selectionMode)
            {
                _onSelectionCancelled?.Invoke();
            }

            _selectionMode = false;
            _allowOutsideClose = true;
            _onOreSelected = null;
            _onSelectionCancelled = null;
            SetActive(false);
            Closed?.Invoke();
        }

        void OpenInternal(Action populate)
        {
            _openedFrame = Time.frameCount;
            populate?.Invoke();
            SetActive(true);
            Opened?.Invoke();
        }

        void PopulateForView()
        {
            var combat = BattleController.Instance != null ? BattleController.Instance.Combat : null;
            if (combat != null)
            {
                PopulateGroupedText(combat.State.Deck.Count, GroupCombatDeck(combat));
                PopulateCombatDeckSlots(combat);
                return;
            }

            var run = RunData.Ensure();
            PopulateGroupedText(run.Deck.Count, GroupRunDeck(run));
            PopulateRunDeckSlots(run);
        }

        void PopulateForSelection()
        {
            var run = RunData.Ensure();
            if (run.Deck.Count == 0)
            {
                if (oreListText != null)
                {
                    oreListText.text = "矿舱空空，无法选择。";
                }

                ClearSlotSprites();
                return;
            }

            if (oreListText != null && !oreListText.text.Contains("选择"))
            {
                oreListText.text = "点击一块矿石确认选择。";
            }

            PopulateRunDeckSlots(run);
        }

        void PopulateGroupedText(int total, Dictionary<string, int> counts)
        {
            if (oreListText == null)
            {
                oreListText = GetComponentInChildren<TMP_Text>(true);
            }

            if (oreListText == null)
            {
                return;
            }

            if (total == 0)
            {
                oreListText.text = "矿舱空空";
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.Append("矿舱（").Append(total).Append("）\n");
            foreach (var kv in counts)
            {
                sb.Append(kv.Key).Append(" x").Append(kv.Value).Append('\n');
            }

            oreListText.text = sb.ToString();
        }

        static Dictionary<string, int> GroupCombatDeck(NineGrid.Battle.Combat.CombatModel combat)
        {
            var counts = new Dictionary<string, int>();
            foreach (var card in combat.State.Deck)
            {
                var name = card.DisplayName;
                counts[name] = counts.TryGetValue(name, out var c) ? c + 1 : 1;
            }

            return counts;
        }

        static Dictionary<string, int> GroupRunDeck(RunData run)
        {
            var counts = new Dictionary<string, int>();
            var catalog = LoadOreCatalog();
            foreach (var entry in run.Deck)
            {
                var name = ResolveOreDisplayName(catalog, entry.OreId);
                counts[name] = counts.TryGetValue(name, out var c) ? c + 1 : 1;
            }

            return counts;
        }

        void PopulateRunDeckSlots(RunData run)
        {
            CacheSlots();
            _slotDeckIndices.Clear();
            var catalog = LoadOreCatalog();

            for (var i = 0; i < _slots.Count; i++)
            {
                var (_, renderer, collider) = _slots[i];
                if (i < run.Deck.Count)
                {
                    var entry = run.Deck[i];
                    _slotDeckIndices.Add(i);
                    ApplySlotVisual(renderer, ResolveOreIcon(catalog, entry.OreId));
                    SetSlotCollider(collider, true);
                }
                else
                {
                    ClearSlot(i);
                }
            }
        }

        void PopulateCombatDeckSlots(CombatModel combat)
        {
            CacheSlots();
            _slotDeckIndices.Clear();
            var deck = combat.State.Deck;

            for (var i = 0; i < _slots.Count; i++)
            {
                var (_, renderer, collider) = _slots[i];
                if (i < deck.Count)
                {
                    var card = deck[i];
                    _slotDeckIndices.Add(i);
                    ApplySlotVisual(renderer, card?.Icon);
                    SetSlotCollider(collider, true);
                }
                else
                {
                    ClearSlot(i);
                }
            }
        }

        static void ApplySlotVisual(SpriteRenderer renderer, Sprite icon)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.enabled = true;
            if (icon != null)
            {
                renderer.sprite = icon;
                renderer.color = Color.white;
            }
            else
            {
                renderer.sprite = null;
                renderer.color = new Color(0.6f, 0.5f, 0.4f, 1f);
            }
        }

        static Sprite ResolveOreIcon(OreCatalog catalog, string oreId)
        {
            if (catalog != null && catalog.TryGet(oreId, out var ore))
            {
                return ore.Icon;
            }

            return null;
        }

        void ClearSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Count)
            {
                return;
            }

            var (_, renderer, collider) = _slots[slotIndex];
            if (renderer != null)
            {
                renderer.enabled = false;
                renderer.sprite = null;
            }

            SetSlotCollider(collider, false);
        }

        static void SetSlotCollider(BoxCollider2D collider, bool enabled)
        {
            if (collider != null)
            {
                collider.enabled = enabled;
            }
        }

        void ClearSlotSprites()
        {
            CacheSlots();
            _slotDeckIndices.Clear();
            for (var i = 0; i < _slots.Count; i++)
            {
                var (_, renderer, collider) = _slots[i];
                if (renderer != null)
                {
                    renderer.enabled = false;
                    renderer.sprite = null;
                }

                if (collider != null)
                {
                    collider.enabled = false;
                }
            }
        }

        void CacheSlots()
        {
            if (_slots.Count > 0)
            {
                return;
            }

            var root = oreSlotsRoot != null ? oreSlotsRoot : panelRoot != null ? panelRoot.transform : transform;
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (!child.name.StartsWith("solt", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var renderer = child.GetComponent<SpriteRenderer>();
                var collider = child.GetComponent<BoxCollider2D>();
                _slots.Add((child, renderer, collider));
            }
        }

        void Update()
        {
            if (!IsOpen || Time.frameCount == _openedFrame)
            {
                return;
            }

            if (!FlowInput.PrimaryClickThisFrame())
            {
                return;
            }

            if (_selectionMode && TryPickOreSlot())
            {
                return;
            }

            if (!_allowOutsideClose || ClickedInsidePanel())
            {
                return;
            }

            Close();
        }

        bool TryPickOreSlot()
        {
            if (!TryGetPointerWorldPosition(out var worldPoint))
            {
                return false;
            }

            CacheSlots();
            for (var i = 0; i < _slots.Count; i++)
            {
                var (_, _, collider) = _slots[i];
                if (collider == null || !collider.enabled || !collider.OverlapPoint(worldPoint))
                {
                    continue;
                }

                if (i >= _slotDeckIndices.Count)
                {
                    return false;
                }

                var deckIndex = _slotDeckIndices[i];
                var callback = _onOreSelected;
                _selectionMode = false;
                _onOreSelected = null;
                _onSelectionCancelled = null;
                _allowOutsideClose = true;
                SetActive(false);
                Closed?.Invoke();
                callback?.Invoke(deckIndex);
                return true;
            }

            return false;
        }

        bool ClickedInsidePanel()
        {
            if (panelBounds == null)
            {
                return false;
            }

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
                if (worldCamera == null)
                {
                    return false;
                }
            }

            var screen = PointerScreenPosition();
            var depth = Mathf.Abs(worldCamera.transform.position.z);
            var world = worldCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
            return panelBounds.OverlapPoint(world);
        }

        bool TryGetPointerWorldPosition(out Vector2 worldPoint)
        {
            worldPoint = default;
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (worldCamera == null)
            {
                return false;
            }

            var screen = PointerScreenPosition();
            var depth = Mathf.Abs(worldCamera.transform.position.z);
            worldPoint = worldCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
            return true;
        }

        void SetActive(bool active)
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(active);
            }
        }

        static Vector2 PointerScreenPosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }
#endif
            return Input.mousePosition;
        }

        static string ResolveOreDisplayName(OreCatalog catalog, string oreId)
        {
            if (catalog != null && catalog.TryGet(oreId, out var ore))
            {
                return ore.DisplayName;
            }

            return oreId;
        }

        static OreCatalog LoadOreCatalog() => GameDataCatalogs.Ore;
    }
}
