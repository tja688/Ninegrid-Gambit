using System;
using System.Collections.Generic;
using NineGrid.Battle.Combat;
using NineGrid.Presentation.Visuals;
using NineGrid.UI;
using UnityEngine;

namespace NineGrid.GameFlow
{
    /// <summary>
    /// 锻造台三按钮 + 显示屏 Factory Text。
    /// - 开始锻造：锻造台有料才可点，触发锤头下压铸造（PlayHydraulic）。
    /// - 退出锻造：常规退场看对面，保留桌面 / 锻造台材料。
    /// - 查看矿石库：开合矿仓小面板。
    /// Factory Text（<=51 字）：拖拽或悬停矿石时显示其信息；无聚焦时显示前/中/后钻头预计伤害与合计。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ForgeStationController : MonoBehaviour
    {
        [Header("引用（留空自动解析）")]
        [SerializeField] HydraulicSceneController hydraulicScene;
        [SerializeField] SceneElementPointerSelector pointerSelector;
        [SerializeField] SelectableSceneElement forgeButton;
        [SerializeField] SelectableSceneElement exitButton;
        [SerializeField] SelectableSceneElement inventoryButton;
        [SerializeField] OreInventoryPanel inventoryPanel;

        [Header("文案")]
        [TextArea(1, 2)]
        [SerializeField] string emptyForgeNotice = "锻造台空空，先丢矿石上去。";

        bool _interactionScopeApplied;
        bool _wasActive;
        string _lastFactoryText;

        CardInstance _oreDisplayCard;
        List<string> _oreDisplaySegments;
        int _oreDisplaySegmentIndex;
        float _oreDisplayCarouselTimer;

        const float OreDisplayCarouselInterval = 2.4f;

        public event Action ForgeRequested;
        public event Action ForgeExited;
        public event Action EmptyWarningShown;

        void Start()
        {
            ResolveRefs();
        }

        void Update()
        {
            if (hydraulicScene == null || forgeButton == null || exitButton == null
                || inventoryButton == null || inventoryPanel == null)
            {
                ResolveRefs();
            }

            if (hydraulicScene == null)
            {
                return;
            }

            SyncInteractionScope();

            var active = hydraulicScene.IsActive;

            if (active)
            {
                UpdateFactoryText();
                HandleButtons();
            }
            else if (_wasActive)
            {
                // 退出锻造：收起显示屏 Factory Text。
                HideFactoryText();
                inventoryPanel?.Close();
            }

            _wasActive = active;
        }

        void OnDisable()
        {
            ReleaseInteractionScope();
        }

        void OnDestroy()
        {
            ReleaseInteractionScope();
        }

        void HandleButtons()
        {
            if (pointerSelector == null || !FlowInput.PrimaryClickThisFrame())
            {
                return;
            }

            var hovered = pointerSelector.Hovered;
            if (hovered == null)
            {
                return;
            }

            if (hovered == forgeButton)
            {
                TryForge();
            }
            else if (hovered == exitButton)
            {
                ForgeExited?.Invoke();
                hydraulicScene.Exit();
            }
            else if (hovered == inventoryButton)
            {
                inventoryPanel?.Toggle();
            }
        }

        void TryForge()
        {
            ForgeRequested?.Invoke();
            var board = hydraulicScene.MaterialBoard;
            if (board == null || !board.HasAnyMaterial())
            {
                SetFactoryText(emptyForgeNotice);
                EmptyWarningShown?.Invoke();
                return;
            }

            hydraulicScene.PlayHydraulic();
        }

        void UpdateFactoryText()
        {
            var board = hydraulicScene.MaterialBoard;
            if (board == null)
            {
                return;
            }

            string text;
            CardInstance focusCard = null;

            if (board.IsDragging)
            {
                focusCard = board.DraggedCard;
            }
            else
            {
                focusCard = board.HoveredPiece?.Card;
            }

            if (focusCard != null)
            {
                text = GetOreDisplayText(focusCard);
            }
            else
            {
                ClearOreDisplayState();
                // 非聚焦：显示前/中/后各台伤害 + 合计（真实数值）
                var combat = BattleController.Instance != null ? BattleController.Instance.Combat : null;
                if (combat != null)
                {
                    var anvilStacks = board.GetAnvilStacks();
                    var total = combat.PreviewDamage(anvilStacks);
                    var f = combat.GetSlotDamage(0);
                    var m = combat.GetSlotDamage(1);
                    var r = combat.GetSlotDamage(2);
                    text = $"\u524D{f} \u4E2D{m} \u540E{r} | \u5408\u8BA1{total}";
                }
                else
                {
                    var f = board.GetPieceCount(0);
                    var m = board.GetPieceCount(1);
                    var r = board.GetPieceCount(2);
                    text = $"\u524D{f} \u4E2D{m} \u540E{r} | \u5F85\u63A5\u5165";
                }
            }

            SetFactoryText(text);
        }

        string GetOreDisplayText(CardInstance card)
        {
            if (card == null)
            {
                ClearOreDisplayState();
                return "\u77FF\u77F3\u4FE1\u606F\uFF1A\u672A\u77E5";
            }

            if (_oreDisplayCard != card)
            {
                _oreDisplayCard = card;
                _oreDisplaySegments = OreDisplayFormatter.BuildForgeSegments(card);
                _oreDisplaySegmentIndex = 0;
                _oreDisplayCarouselTimer = 0f;
            }
            else if (_oreDisplaySegments == null || _oreDisplaySegments.Count == 0)
            {
                _oreDisplaySegments = OreDisplayFormatter.BuildForgeSegments(card);
                _oreDisplaySegmentIndex = 0;
            }

            if (_oreDisplaySegments.Count <= 1)
            {
                return _oreDisplaySegments[0];
            }

            _oreDisplayCarouselTimer += Time.unscaledDeltaTime;
            if (_oreDisplayCarouselTimer >= OreDisplayCarouselInterval)
            {
                _oreDisplayCarouselTimer = 0f;
                _oreDisplaySegmentIndex = (_oreDisplaySegmentIndex + 1) % _oreDisplaySegments.Count;
            }

            return _oreDisplaySegments[_oreDisplaySegmentIndex];
        }

        void ClearOreDisplayState()
        {
            _oreDisplayCard = null;
            _oreDisplaySegments = null;
            _oreDisplaySegmentIndex = 0;
            _oreDisplayCarouselTimer = 0f;
        }

        void SetFactoryText(string text)
        {
            if (text == _lastFactoryText)
            {
                return;
            }

            var notice = UiSystem.Instance != null ? UiSystem.Instance.Notice : null;
            if (notice == null)
            {
                return;
            }

            if (text.Length > OreDisplayFormatter.MaxChars)
            {
                text = text.Substring(0, OreDisplayFormatter.MaxChars);
            }

            notice.Show(NoticeChannel.Factory, text, 0f);
            _lastFactoryText = text;
        }

        void HideFactoryText()
        {
            ClearOreDisplayState();
            _lastFactoryText = null;
            var notice = UiSystem.Instance != null ? UiSystem.Instance.Notice : null;
            if (notice != null && notice.IsShowing && notice.ActiveChannel == NoticeChannel.Factory)
            {
                notice.Hide();
            }
        }

        void SyncInteractionScope()
        {
            if (pointerSelector == null || hydraulicScene == null)
            {
                return;
            }

            if (!hydraulicScene.IsActive && !hydraulicScene.IsBusy)
            {
                ReleaseInteractionScope();
                return;
            }

            if (hydraulicScene.IsActive)
            {
                pointerSelector.ApplyInteractionScope(this, forgeButton, exitButton, inventoryButton);
            }
            else
            {
                // 过场/液压演出期间彻底封住底层世界物体点击。
                pointerSelector.ApplyInteractionScope(this);
            }

            _interactionScopeApplied = true;
        }

        void ReleaseInteractionScope()
        {
            if (!_interactionScopeApplied || pointerSelector == null)
            {
                _interactionScopeApplied = false;
                return;
            }

            pointerSelector.ClearInteractionScope(this);
            _interactionScopeApplied = false;
        }

        void ResolveRefs()
        {
            if (hydraulicScene == null)
            {
                hydraulicScene = HydraulicSceneController.Instance
                    ?? FindFirstObjectByType<HydraulicSceneController>(FindObjectsInactive.Include);
            }

            if (pointerSelector == null)
            {
                pointerSelector = FindFirstObjectByType<SceneElementPointerSelector>();
            }

            if (forgeButton == null)
            {
                forgeButton = FindSelectable("开始锻造") ?? FindSelectable("开始锻造按钮");
            }

            if (exitButton == null)
            {
                exitButton = FindSelectable("退出锻造场景按钮")
                    ?? FindSelectable("退出锻造")
                    ?? FindSelectable("退出锻造按钮");
            }

            if (inventoryButton == null)
            {
                inventoryButton = FindSelectable("查看矿石库") ?? FindSelectable("查看矿石库按钮");
            }

            if (inventoryPanel == null)
            {
                inventoryPanel = FindFirstObjectByType<OreInventoryPanel>(FindObjectsInactive.Include);
            }
        }

        static SelectableSceneElement FindSelectable(string objectName)
        {
            var go = GameObject.Find(objectName);
            return go != null ? go.GetComponent<SelectableSceneElement>() : null;
        }
    }
}
