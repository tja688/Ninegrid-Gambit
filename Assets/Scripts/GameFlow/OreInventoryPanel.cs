using System;
using System.Collections.Generic;
using NineGrid.Battle.Combat;
using UnityEngine;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NineGrid.GameFlow
{
    /// <summary>
    /// 矿石库（矿仓）小面板：由「查看矿石库」按钮开启，点面板外部自动关闭。
    /// 面板内 TMP_Text 展示当前矿舱（牌库）的矿石列表与数量。
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

        int _openedFrame = -1;

        public event Action Opened;
        public event Action Closed;

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

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
                Open();
            }
        }

        public void Open()
        {
            _openedFrame = Time.frameCount;
            PopulateOreList();
            SetActive(true);
            Opened?.Invoke();
        }

        /// <summary>从战斗模型读取矿舱（牌库）内容，刷新面板文本。</summary>
        void PopulateOreList()
        {
            if (oreListText == null)
            {
                oreListText = GetComponentInChildren<TMP_Text>(true);
            }

            if (oreListText == null) return;

            var combat = BattleController.Instance != null ? BattleController.Instance.Combat : null;
            if (combat == null)
            {
                oreListText.text = "\u6218\u6597\u672A\u5F00\u59CB";
                return;
            }

            var deck = combat.State.Deck;
            if (deck.Count == 0)
            {
                oreListText.text = "\u77FF\u8231\u7A7A\u7A7A";
                return;
            }

            // 按矿石名分组计数
            var counts = new Dictionary<string, int>();
            foreach (var card in deck)
            {
                var name = card.DisplayName;
                counts[name] = counts.TryGetValue(name, out var c) ? c + 1 : 1;
            }

            var sb = new System.Text.StringBuilder();
            sb.Append("\u77FF\u8231\uFF08").Append(deck.Count).Append("\uFF09\n");
            foreach (var kv in counts)
            {
                sb.Append(kv.Key).Append(" x").Append(kv.Value).Append('\n');
            }

            oreListText.text = sb.ToString();
        }

        public void Close()
        {
            SetActive(false);
            Closed?.Invoke();
        }

        void Update()
        {
            if (!IsOpen || Time.frameCount == _openedFrame)
            {
                return;
            }

            if (!PrimaryClickThisFrame())
            {
                return;
            }

            if (ClickedInsidePanel())
            {
                return;
            }

            Close();
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

        void SetActive(bool active)
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(active);
            }
        }

        static bool PrimaryClickThisFrame()
        {
            if (Input.GetMouseButtonDown(0))
            {
                return true;
            }

#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                return true;
            }
#endif
            return false;
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
    }
}
