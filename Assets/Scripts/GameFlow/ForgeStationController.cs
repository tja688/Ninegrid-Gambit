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
    /// Factory Text：拖拽或悬停矿石时显示其信息（≤51 字）；无聚焦时单行展示完整预计伤害与熔炼加成说明。
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
                text = GetIdleDisplayText(board);
            }

            SetFactoryText(text, focusCard != null ? OreDisplayFormatter.MaxChars : OreDisplayFormatter.MaxIdleChars);
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

        string GetIdleDisplayText(HydraulicMaterialBoard board)
        {
            var combat = BattleController.Instance != null ? BattleController.Instance.Combat : null;
            var combatReady = combat != null;

            var frontSmelt = OreDisplayFormatter.GetAnvilOreCount(board, 0);
            var midSmelt = OreDisplayFormatter.GetAnvilOreCount(board, 1);
            var backSmelt = OreDisplayFormatter.GetAnvilOreCount(board, 2);

            int frontDamage;
            int midDamage;
            int backDamage;
            int totalDamage;
            if (combatReady)
            {
                var anvilStacks = board.GetAnvilStacks();
                totalDamage = combat.PreviewDamage(anvilStacks);
                frontDamage = combat.GetSlotDamage(0);
                midDamage = combat.GetSlotDamage(1);
                backDamage = combat.GetSlotDamage(2);
            }
            else
            {
                frontDamage = board.GetPieceCount(0);
                midDamage = board.GetPieceCount(1);
                backDamage = board.GetPieceCount(2);
                totalDamage = 0;
            }

            return OreDisplayFormatter.BuildIdleForgeText(
                frontDamage, midDamage, backDamage, totalDamage,
                frontSmelt, midSmelt, backSmelt, combatReady);
        }

        void SetFactoryText(string text, int maxChars = OreDisplayFormatter.MaxChars)
        {
            var notice = UiSystem.Instance != null ? UiSystem.Instance.Notice : null;
            if (notice == null)
            {
                return;
            }

            if (text.Length > maxChars)
            {
                text = text.Substring(0, maxChars);
            }

            // 相同文案也要允许重新点亮；已在显示时须强制刷新（TextAnimator hideText 首次后不再更新）。
            if (text == _lastFactoryText && IsFactoryTextVisible(notice))
            {
                return;
            }

            if (IsFactoryTextVisible(notice)
                && notice.ActiveChannel == NoticeChannel.Factory
                && notice.TryUpdateActiveText(NoticeChannel.Factory, text))
            {
                _lastFactoryText = text;
                return;
            }

            notice.Show(NoticeChannel.Factory, text, 0f);
            _lastFactoryText = text;
        }

        static bool IsFactoryTextVisible(NoticeSystem notice)
        {
            if (notice == null || !notice.IsShowing || notice.ActiveChannel != NoticeChannel.Factory)
            {
                return false;
            }

            var ui = UiSystem.Instance;
            return ui != null && ui.FactoryTextObject != null && ui.FactoryTextObject.activeSelf;
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
