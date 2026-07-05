using NineGrid.Battle.Combat;
using NineGrid.Data;
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
    /// Factory Text（<=50 字）：拖拽矿石时显示其信息；无拖拽时显示前/中/后钻头预计伤害与合计（数值待接入，先占位）。
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

        bool _wasActive;
        string _lastFactoryText;

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
                hydraulicScene.Exit();
            }
            else if (hovered == inventoryButton)
            {
                inventoryPanel?.Toggle();
            }
        }

        void TryForge()
        {
            var board = hydraulicScene.MaterialBoard;
            if (board == null || !board.HasAnyMaterial())
            {
                SetFactoryText(emptyForgeNotice);
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
            if (board.IsDragging)
            {
                // 拖拽时：显示被拖矿石信息（临时覆盖伤害预览）
                var card = board.DraggedCard;
                if (card != null)
                {
                    var pts = card.GetBaseValue();
                    var traits = GetTraitText(card.Traits);
                    text = traits.Length > 0
                        ? $"{card.DisplayName} {pts}\u70B9 {traits}"
                        : $"{card.DisplayName} {pts}\u70B9";
                }
                else
                {
                    text = "\u77FF\u77F3\u4FE1\u606F\uFF1A\u672A\u77E5";
                }
            }
            else
            {
                // 非拖拽：显示前/中/后各台伤害 + 合计（真实数值）
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

        /// <summary>将 OreTrait 位标志转为可读词条文本（如「淬火+熔核」）。</summary>
        static string GetTraitText(OreTrait traits)
        {
            if (traits == OreTrait.None) return "";

            var parts = new System.Collections.Generic.List<string>(4);
            if ((traits & OreTrait.Quench2) != 0) parts.Add("\u6DEC\u706B2");
            else if ((traits & OreTrait.Quench) != 0) parts.Add("\u6DEC\u706B");
            if ((traits & OreTrait.Preheat) != 0) parts.Add("\u9884\u70ED");
            if ((traits & OreTrait.Core) != 0) parts.Add("\u7194\u6838");
            if ((traits & OreTrait.Unity) != 0) parts.Add("\u9F50\u5FC3");
            if ((traits & OreTrait.Sociable) != 0) parts.Add("\u5408\u7FA4");
            if ((traits & OreTrait.Twin) != 0) parts.Add("\u53CC\u6676");
            if ((traits & OreTrait.Symbiosis2) != 0) parts.Add("\u5171\u751F2");
            else if ((traits & OreTrait.Symbiosis) != 0) parts.Add("\u5171\u751F");
            if ((traits & OreTrait.Station) != 0) parts.Add("\u9A7B\u53F0");
            if ((traits & OreTrait.Debris) != 0) parts.Add("\u788E\u5C51");
            if ((traits & OreTrait.Ember) != 0) parts.Add("\u4F59\u70EC");

            return string.Join("+", parts);
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

            if (text.Length > 50)
            {
                text = text.Substring(0, 50);
            }

            notice.Show(NoticeChannel.Factory, text, 0f);
            _lastFactoryText = text;
        }

        void HideFactoryText()
        {
            _lastFactoryText = null;
            var notice = UiSystem.Instance != null ? UiSystem.Instance.Notice : null;
            if (notice != null && notice.IsShowing && notice.ActiveChannel == NoticeChannel.Factory)
            {
                notice.Hide();
            }
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
