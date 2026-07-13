#if UNITY_EDITOR || DEVELOPMENT_BUILD

using NineGrid.Flow;
using UnityEngine;

namespace NineGrid.DevTest.Flow
{
    /// <summary>
    /// 局内 \ 键 Debug 快速模式：长按弹出菜单，\1 全局 x1，\2 全局 x2（再按 x2）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InBattleDebugQuickModeInputHandler : MonoBehaviour
    {
        private const float LongPressSeconds = 0.35f;

        private enum InputState
        {
            Idle,
            PendingLongPress,
            MenuOpen,
        }

        [Tooltip("运行时自动查找 MainGameLoopManagerSingleton.Instance；也可手动拖入覆盖。")]
        [SerializeField] private MainGameLoopManagerSingleton loopManager;

        private InputState _state = InputState.Idle;
        private float _backslashDownRealtime;
        private bool _longPressTriggered;

        private void Awake()
        {
            if (loopManager == null)
            {
                loopManager = GetComponent<MainGameLoopManagerSingleton>();
            }

            if (loopManager == null)
            {
                loopManager = MainGameLoopManagerSingleton.Instance;
            }
        }

        private void Update()
        {
            if (!CanHandleInput())
            {
                ResetToIdle(closeMenu: true);
                return;
            }

            switch (_state)
            {
                case InputState.Idle:
                    PollIdle();
                    break;
                case InputState.PendingLongPress:
                    PollPendingLongPress();
                    break;
                case InputState.MenuOpen:
                    PollMenuOpen();
                    break;
            }
        }

        private bool CanHandleInput()
        {
            if (loopManager == null)
            {
                loopManager = MainGameLoopManagerSingleton.Instance;
            }

            return loopManager != null && loopManager.CanAcceptInBattleDebugQuickMode;
        }

        private void PollIdle()
        {
            if (!Input.GetKeyDown(KeyCode.Backslash))
            {
                return;
            }

            _backslashDownRealtime = Time.unscaledTime;
            _longPressTriggered = false;
            _state = InputState.PendingLongPress;
        }

        private void PollPendingLongPress()
        {
            if (!_longPressTriggered
                && Input.GetKey(KeyCode.Backslash)
                && Time.unscaledTime - _backslashDownRealtime >= LongPressSeconds)
            {
                _longPressTriggered = true;
                OpenMenu();
                return;
            }

            if (!Input.GetKey(KeyCode.Backslash))
            {
                ResetToIdle(closeMenu: false);
            }
        }

        private void PollMenuOpen()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ResetToIdle(closeMenu: true);
                return;
            }

            if (TryReadDigitKeyDown(out var digit))
            {
                ApplyDigitAction(digit);
            }
        }

        private void OpenMenu()
        {
            _state = InputState.MenuOpen;
            RefreshMenuNotice();
        }

        private void ApplyDigitAction(char digit)
        {
            switch (digit)
            {
                case '1':
                    loopManager.ApplyInBattleDebugQuickModeTimeScaleX1();
                    RefreshMenuNotice();
                    break;
                case '2':
                    loopManager.ApplyInBattleDebugQuickModeTimeScaleX2();
                    RefreshMenuNotice();
                    break;
            }
        }

        private void RefreshMenuNotice()
        {
            loopManager.ShowInBattleDebugQuickModeNotice(loopManager.BuildInBattleDebugQuickModeMenuText());
        }

        private void ResetToIdle(bool closeMenu)
        {
            if (closeMenu && _state == InputState.MenuOpen)
            {
                loopManager.HideInBattleDebugQuickModeNotice();
            }

            _state = InputState.Idle;
            _longPressTriggered = false;
        }

        private static bool TryReadDigitKeyDown(out char digit)
        {
            for (var value = 0; value <= 9; value++)
            {
                var alpha = KeyCode.Alpha0 + value;
                var keypad = KeyCode.Keypad0 + value;
                if (Input.GetKeyDown(alpha) || Input.GetKeyDown(keypad))
                {
                    digit = (char)('0' + value);
                    return true;
                }
            }

            digit = default;
            return false;
        }
    }
}

#endif
