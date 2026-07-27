#if UNITY_EDITOR || DEVELOPMENT_BUILD

using NineGrid.Flow;
using NineGrid.DevTest.Commands;
using UnityEngine;

namespace NineGrid.DevTest.Flow
{
    /// <summary>
    /// 局内 \ 键快速模式：长按弹出菜单，\1/\2 调速，释放关闭。
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

        [Tooltip("运行时查找场景中的 GameFlowController；也可手动拖入覆盖。")]
        [SerializeField] private GameFlowController loopManager;

        private InputState _state = InputState.Idle;
        private float _backslashDownRealtime;
        private bool _longPressTriggered;

        private void Awake()
        {
            if (loopManager == null)
            {
                loopManager = GetComponent<GameFlowController>();
            }

            if (loopManager == null)
            {
                loopManager = UnityEngine.Object.FindFirstObjectByType<GameFlowController>();
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
                loopManager = UnityEngine.Object.FindFirstObjectByType<GameFlowController>();
            }

            return loopManager != null && GameFlowDevQueries.CanAcceptInBattleDebugQuickMode();
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
            // 释放 \ 键：关闭菜单
            if (!Input.GetKey(KeyCode.Backslash))
            {
                ResetToIdle(closeMenu: true);
                return;
            }

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
            var arch = NineGrid.Core.NineGridArchitecture.Interface
                       ?? NineGrid.Core.NineGridArchitecture.Current;
            switch (digit)
            {
                case '1':
                    if (arch != null)
                    {
                        arch.SendCommand(new ApplyInBattleTimeScaleX1Command());
                    }
                    else
                    {
                        loopManager.ApplyInBattleDebugQuickModeTimeScaleX1();
                    }

                    RefreshMenuNotice();
                    break;
                case '2':
                    if (arch != null)
                    {
                        arch.SendCommand(new ApplyInBattleTimeScaleX2Command());
                    }
                    else
                    {
                        loopManager.ApplyInBattleDebugQuickModeTimeScaleX2();
                    }

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
