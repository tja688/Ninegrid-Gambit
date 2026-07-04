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
                // TODO: 接入被拖拽矿石的真实信息。
                text = "矿石信息：待接入";
            }
            else
            {
                // TODO: 接入真实伤害计算。当前显示各台材料数占位。
                var f = board.GetPieceCount(0);
                var m = board.GetPieceCount(1);
                var r = board.GetPieceCount(2);
                text = $"前{f} 中{m} 后{r}·伤害待接入";
            }

            SetFactoryText(text);
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
