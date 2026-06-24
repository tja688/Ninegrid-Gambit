using NineGrid.Presentation.Performance;
using NineGrid.Presentation.Visuals;
using UnityEngine;

namespace NineGrid.Presentation.Tools
{
    /// <summary>
    /// 场地 Card 状态面板预览：小键盘 1 涨攻击、2 降生命、3 涨护甲。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StatusPanelPreviewTool : MonoBehaviour
    {
        [Header("Hotkeys (Numpad)")]
        [SerializeField] private KeyCode attackUpKey = KeyCode.Keypad1;
        [SerializeField] private KeyCode lifeDownKey = KeyCode.Keypad2;
        [SerializeField] private KeyCode armorUpKey = KeyCode.Keypad3;

        [Header("Target")]
        [SerializeField] private Transform previewCardActor;
        [SerializeField] private StatusPanelUpdatePerformance statusPanelUpdatePerformance;

        private TableNineCardStatusView cardStatusView;

        private void Awake()
        {
            EnsureReferences();
        }

        private void Start()
        {
            EnsureReferences();
            cardStatusView?.DebugSnapDefaults();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureReferences();
            if (cardStatusView == null)
            {
                return;
            }

            if (Input.GetKeyDown(attackUpKey))
            {
                cardStatusView.DebugIncrementAttack();
                return;
            }

            if (Input.GetKeyDown(lifeDownKey))
            {
                cardStatusView.DebugDecrementLife();
                return;
            }

            if (Input.GetKeyDown(armorUpKey))
            {
                cardStatusView.DebugIncrementArmor();
            }
        }

        [ContextMenu("Debug/Snap Defaults (Atk1 Life5 Armor0)")]
        private void ContextSnapDefaults()
        {
            EnsureReferences();
            cardStatusView?.DebugSnapDefaults();
        }

        private void EnsureReferences()
        {
            if (statusPanelUpdatePerformance == null)
            {
                statusPanelUpdatePerformance = GetComponent<StatusPanelUpdatePerformance>();
            }

            if (previewCardActor == null)
            {
                GameObject card = GameObject.Find("Card");
                if (card != null)
                {
                    previewCardActor = card.transform;
                }
            }

            if (previewCardActor == null)
            {
                return;
            }

            cardStatusView = previewCardActor.GetComponent<TableNineCardStatusView>();
            if (cardStatusView == null)
            {
                cardStatusView = previewCardActor.GetComponentInChildren<TableNineCardStatusView>(true);
            }

            if (cardStatusView == null)
            {
                cardStatusView = previewCardActor.gameObject.AddComponent<TableNineCardStatusView>();
            }

            if (statusPanelUpdatePerformance != null)
            {
                statusPanelUpdatePerformance.ConfigurePreviewView(cardStatusView);
            }
            else
            {
                cardStatusView.EnsureBindings();
            }
        }
    }
}
