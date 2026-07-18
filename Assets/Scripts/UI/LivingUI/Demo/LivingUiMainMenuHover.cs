using NineGrid.LivingUI.Unity;
using UnityEngine;

namespace NineGrid.LivingUI.Demo
{
    /// <summary>
    /// 主菜单右侧三小载体 hover 局部变体：稳定区命中 → 该载体 pop（位/尺寸）→ 标签 RigidTravel 跟随。
    /// </summary>
    [DefaultExecutionOrder(-150)]
    [DisallowMultipleComponent]
    public sealed class LivingUiMainMenuHover : MonoBehaviour
    {
        [Tooltip("灵动 UI 导演；留空时同物体 GetComponent。")]
        [SerializeField] private LivingUiDirector director;

        [Tooltip("构型样板来源；留空时同物体 GetComponent。")]
        [SerializeField] private LivingUiSceneLayoutSource layoutSource;

        [Tooltip("稳定命中区根；留空时运行时按名 StableHitZoneRoot 查找。")]
        [SerializeField] private LivingUiStableHitZoneRoot hitZones;

        [Tooltip("指针换算相机；留空时 Camera.main。")]
        [SerializeField] private Camera pointerCamera;

        [Tooltip("Hover 时载体中心上移量（世界单位）。")]
        [SerializeField] private float popOffsetY = 0.12f;

        [Tooltip("Hover 时尺寸放大倍率。")]
        [SerializeField] private float popSizeMul = 1.08f;

        private int _hoveredCarrierId;
        private Vector3 _restPosition;
        private Vector2 _restSize;

        private void Awake()
        {
            if (director == null) director = GetComponent<LivingUiDirector>();
            if (layoutSource == null) layoutSource = GetComponent<LivingUiSceneLayoutSource>();
            if (pointerCamera == null) pointerCamera = Camera.main;
            if (hitZones == null)
            {
                var found = GameObject.Find("StableHitZoneRoot");
                if (found != null) hitZones = found.GetComponent<LivingUiStableHitZoneRoot>();
            }

            if (director == null || layoutSource == null || hitZones == null)
            {
                Debug.LogError("[LivingUI] MainMenuHover 缺少 Director/LayoutSource/HitZones，已禁用。");
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (director == null || hitZones == null) return;

            if (director.CommittedLayout != LivingUiLayoutId.MainMenu
                || director.PreviewLayout.HasValue
                || director.IsTransitioning)
            {
                ClearHover();
                return;
            }

            var world = ScreenToWorld(Input.mousePosition);
            var inside = hitZones.TryHit(world, out var carrierId);
            if (!inside)
            {
                ClearHover();
                return;
            }

            if (_hoveredCarrierId != carrierId)
            {
                ClearHover();
                CaptureRest(carrierId);
                _hoveredCarrierId = carrierId;
            }

            ApplyPop();
        }

        private void OnDisable()
        {
            ClearHover();
        }

        private void CaptureRest(int carrierId)
        {
            if (!layoutSource.Carriers.TryGetValue(carrierId, out var renderer) || renderer == null) return;
            _restPosition = renderer.transform.position;
            _restSize = renderer.size;
        }

        private void ApplyPop()
        {
            if (_hoveredCarrierId == 0) return;
            if (!layoutSource.Carriers.TryGetValue(_hoveredCarrierId, out var renderer) || renderer == null) return;
            renderer.transform.position = new Vector3(
                _restPosition.x,
                _restPosition.y + popOffsetY,
                _restPosition.z);
            renderer.size = _restSize * popSizeMul;
        }

        private void ClearHover()
        {
            if (_hoveredCarrierId == 0) return;
            if (layoutSource != null
                && layoutSource.Carriers.TryGetValue(_hoveredCarrierId, out var renderer)
                && renderer != null)
            {
                renderer.transform.position = _restPosition;
                renderer.size = _restSize;
            }

            _hoveredCarrierId = 0;
        }

        private Vector2 ScreenToWorld(Vector3 screen)
        {
            var cam = pointerCamera != null ? pointerCamera : Camera.main;
            if (cam == null) return Vector2.zero;
            var p = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, Mathf.Abs(cam.transform.position.z)));
            return new Vector2(p.x, p.y);
        }
    }
}
