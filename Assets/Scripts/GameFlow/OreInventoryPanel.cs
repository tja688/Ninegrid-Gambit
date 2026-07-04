using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NineGrid.GameFlow
{
    /// <summary>
    /// 矿石库（矿仓）小面板：由「查看矿石库」按钮开启，点面板外部自动关闭。
    /// 槽位里的玩家持有矿石图 + 悬停信息尚未接入真实仓库数据；先做开合与外部点击关闭。
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

        int _openedFrame = -1;

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
            SetActive(true);
        }

        public void Close()
        {
            SetActive(false);
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
