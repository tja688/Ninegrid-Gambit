using NineGrid.Flow;
using UnityEngine;

namespace NineGrid.DevTest.Flow
{
    /// <summary>
    /// 主菜单 \ 键快速测试入口：长按弹出选关菜单，释放确认。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuickTestEntryInputHandler : MonoBehaviour
    {
        private const float LongPressSeconds = 0.35f;

        private enum InputState
        {
            Idle,
            PendingLongPress,
            Picker,
        }

        [Tooltip("运行时自动查找 MainGameLoopManagerSingleton.Instance；也可手动拖入覆盖。")]
        [SerializeField] private MainGameLoopManagerSingleton loopManager;

        private InputState _state = InputState.Idle;
        private float _backslashDownRealtime;
        private bool _longPressTriggered;
        private string _digitBuffer = string.Empty;

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
                ResetToIdle();
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
                case InputState.Picker:
                    PollPicker();
                    break;
            }
        }

        private bool CanHandleInput()
        {
            if (loopManager == null)
            {
                loopManager = MainGameLoopManagerSingleton.Instance;
            }

            return loopManager != null && loopManager.CanAcceptQuickTestEntry;
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
                EnterPicker();
                return;
            }

            if (!Input.GetKey(KeyCode.Backslash))
            {
                ResetToIdle();
            }
        }

        private void PollPicker()
        {
            // 释放 \ 键：确认选择并关闭
            if (!Input.GetKey(KeyCode.Backslash))
            {
                ConfirmAndClose();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                loopManager.HideQuickTestPickerNotice();
                ResetToIdle();
                return;
            }

            if (TryReadDigitKeyDown(out var digit) && _digitBuffer.Length < 2)
            {
                _digitBuffer += digit;
            }
        }

        private void EnterPicker()
        {
            _state = InputState.Picker;
            _digitBuffer = string.Empty;
            loopManager.ShowQuickTestPickerNotice(loopManager.BuildQuickTestPickerMenuText());
        }

        private void ConfirmAndClose()
        {
            if (_digitBuffer.Length > 0
                && int.TryParse(_digitBuffer, out var code)
                && loopManager.TryBeginQuickTestFromPickerCode(code))
            {
                Debug.Log("[QuickTestEntry] 选关开始 code=" + code);
            }

            loopManager.HideQuickTestPickerNotice();
            ResetToIdle();
        }

        private void ResetToIdle()
        {
            _state = InputState.Idle;
            _digitBuffer = string.Empty;
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
