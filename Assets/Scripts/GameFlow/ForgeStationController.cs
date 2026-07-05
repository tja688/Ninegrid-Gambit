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
    /// Factory Text（<=51 字）：拖拽或悬停矿石时显示其信息；无聚焦时轮播伤害预览、熔炼加成现状与叠矿规则。
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

        List<string> _idleDisplaySegments;
        int _idleDisplaySegmentIndex;
        float _idleDisplayCarouselTimer;
        string _idleDisplaySignature;

        const float OreDisplayCarouselInterval = 2.4f;
        const float IdleDisplayCarouselInterval = 3.2f;

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
                ClearIdleDisplayState();
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

            var frontSmelt = OreDisplayFormatter.GetSmeltCycleCount(board.GetPieceCount(0));
            var midSmelt = OreDisplayFormatter.GetSmeltCycleCount(board.GetPieceCount(1));
            var backSmelt = OreDisplayFormatter.GetSmeltCycleCount(board.GetPieceCount(2));

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

            var signature =
                $"{frontDamage}|{midDamage}|{backDamage}|{totalDamage}|{frontSmelt}|{midSmelt}|{backSmelt}|{combatReady}";
            if (_idleDisplaySegments == null || signature != _idleDisplaySignature)
            {
                _idleDisplaySegments = OreDisplayFormatter.BuildIdleForgeSegments(
                    frontDamage, midDamage, backDamage, totalDamage,
                    frontSmelt, midSmelt, backSmelt, combatReady);
                _idleDisplaySignature = signature;
                _idleDisplaySegmentIndex = 0;
                _idleDisplayCarouselTimer = 0f;
            }

            if (_idleDisplaySegments.Count <= 1)
            {
                return _idleDisplaySegments[0];
            }

            _idleDisplayCarouselTimer += Time.unscaledDeltaTime;
            if (_idleDisplayCarouselTimer >= IdleDisplayCarouselInterval)
            {
                _idleDisplayCarouselTimer = 0f;
                _idleDisplaySegmentIndex = (_idleDisplaySegmentIndex + 1) % _idleDisplaySegments.Count;
            }

            return _idleDisplaySegments[_idleDisplaySegmentIndex];
        }

        void ClearIdleDisplayState()
        {
            _idleDisplaySegments = null;
            _idleDisplaySegmentIndex = 0;
            _idleDisplayCarouselTimer = 0f;
            _idleDisplaySignature = null;
        }

        void SetFactoryText(string text)
        {
            var notice = UiSystem.Instance != null ? UiSystem.Instance.Notice : null;
            if (notice == null)
            {
                return;
            }

            if (text.Length > OreDisplayFormatter.MaxChars)
            {
                text = text.Substring(0, OreDisplayFormatter.MaxChars);
            }

            // 其他系统可能会直接把 FactoryText 关掉；相同文案也要允许重新点亮。
            if (text == _lastFactoryText && IsFactoryTextVisible(notice))
            {
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
            ClearIdleDisplayState();
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
