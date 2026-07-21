using NineGrid.LivingUI.Unity;
using UnityEngine;

namespace NineGrid.LivingUI.Demo
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class LivingUiDemoInput : MonoBehaviour
    {
        [Tooltip("灵动 UI 权威导演；留空时运行时从同物体 GetComponent 自动装配，缺失则禁用本组件。")]
        [SerializeField] private LivingUiDirector director;

        [Tooltip("Hover 指针换算相机；留空时运行时自动取 Camera.main，缺失则禁用 Hover。")]
        [SerializeField] private Camera pointerCamera;

        [Tooltip("稳定 Hover 区使用的载体 ID；运行时固定读取基础战斗构型终态，不跟随运动载体。")]
        [Range(1, 12)]
        [SerializeField] private int hoverCarrierId = 12;

        private bool _hovering;

        private void Awake()
        {
            if (director == null) director = GetComponent<LivingUiDirector>();
            if (pointerCamera == null) pointerCamera = Camera.main;
            if (director == null)
            {
                Debug.LogError("[LivingUI] DemoInput 未找到 LivingUiDirector，已禁用。");
                enabled = false;
            }
        }

        private void Update()
        {
            HandleKeyboard();
            HandleStableHover();
        }

        private void OnDisable()
        {
            if (_hovering && director != null) director.ClearPreview();
            _hovering = false;
        }

        private void HandleKeyboard()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) director.Commit(LivingUiLayoutId.MainMenu);
            if (Input.GetKeyDown(KeyCode.Alpha2)) director.Commit(LivingUiLayoutId.CharacterChoice);
            if (Input.GetKeyDown(KeyCode.Alpha3)) ToggleBattleReward();
            if (Input.GetKeyDown(KeyCode.Alpha4)) director.Commit(LivingUiLayoutId.Room);
            if (Input.GetKeyDown(KeyCode.Alpha5)) director.Commit(LivingUiLayoutId.Route);
        }

        private void ToggleBattleReward()
        {
            var next = director.CommittedLayout == LivingUiLayoutId.Battle
                ? LivingUiLayoutId.RewardChoice
                : LivingUiLayoutId.Battle;
            director.Commit(next);
            _hovering = false;
        }

        private void HandleStableHover()
        {
            var canPreview = director.CommittedLayout == LivingUiLayoutId.Battle;
            var inside = canPreview && pointerCamera != null && IsPointerInsideStableZone();
            if (inside == _hovering) return;

            _hovering = inside;
            if (_hovering)
            {
                director.Preview(LivingUiLayoutId.DeckPreview);
            }
            else
            {
                director.ClearPreview();
            }
        }

        private bool IsPointerInsideStableZone()
        {
            var pointer = Input.mousePosition;
            var world = pointerCamera.ScreenToWorldPoint(new Vector3(pointer.x, pointer.y, Mathf.Abs(pointerCamera.transform.position.z)));
            return director.GetTerminalRect(LivingUiLayoutId.Battle, hoverCarrierId).Contains(world);
        }
    }
}
