#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Text;
using NineGrid.Flow;
using UnityEngine;

namespace NineGrid.DevTest.Flow
{
    /// <summary>
    /// 主菜单 \ 键快速测试入口：短按随机开局，长按弹出选关菜单。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuickTestEntryInputHandler : MonoBehaviour
    {
        private const float LongPressSeconds = 0.35f;
        private const float DigitConfirmSeconds = 0.8f;
        private const int MaxDigitCount = 2;

        private enum InputState
        {
            Idle,
            PendingShort,
            Picker,
        }

        [Tooltip("运行时自动查找 MainGameLoopManagerSingleton.Instance；也可手动拖入覆盖。")]
        [SerializeField] private MainGameLoopManagerSingleton loopManager;

        private InputState _state = InputState.Idle;
        private float _backslashDownRealtime;
        private bool _longPressTriggered;
        private readonly StringBuilder _digitBuffer = new StringBuilder(MaxDigitCount);
        private float _lastDigitRealtime;

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
                case InputState.PendingShort:
                    PollPendingShort();
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
            _state = InputState.PendingShort;
        }

        private void PollPendingShort()
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
                if (!_longPressTriggered)
                {
                    StartRandomQuickTest();
                }

                ResetToIdle();
            }
        }

        private void PollPicker()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                loopManager.HideQuickTestPickerNotice();
                ResetToIdle();
                return;
            }

            if (TryReadDigitKeyDown(out var digit))
            {
                if (_digitBuffer.Length < MaxDigitCount)
                {
                    _digitBuffer.Append(digit);
                }

                _lastDigitRealtime = Time.unscaledTime;
            }

            if (_digitBuffer.Length == 0)
            {
                return;
            }

            if (_digitBuffer.Length >= MaxDigitCount
                || Time.unscaledTime - _lastDigitRealtime >= DigitConfirmSeconds)
            {
                TryConfirmPickerSelection();
            }
        }

        private void EnterPicker()
        {
            _state = InputState.Picker;
            _digitBuffer.Clear();
            _lastDigitRealtime = Time.unscaledTime;
            loopManager.ShowQuickTestPickerNotice(loopManager.BuildQuickTestPickerMenuText());
        }

        private void TryConfirmPickerSelection()
        {
            if (!int.TryParse(_digitBuffer.ToString(), out var code))
            {
                ShowInvalidSelection("编号无效");
                return;
            }

            if (loopManager.TryBeginQuickTestFromPickerCode(code))
            {
                Debug.Log("[QuickTestEntry] 选关开始 code=" + code);
                ResetToIdle();
                return;
            }

            ShowInvalidSelection(
                "编号 " + code + " 无效（可用 0-" + QuickTestDeckCatalog.MaxPickerCode + "）");
        }

        private void ShowInvalidSelection(string message)
        {
            Debug.LogWarning("[QuickTestEntry] " + message);
            loopManager.ShowQuickTestPickerNotice(
                loopManager.BuildQuickTestPickerMenuText()
                + "\n\n"
                + message);
            _digitBuffer.Clear();
            _lastDigitRealtime = Time.unscaledTime;
        }

        private void StartRandomQuickTest()
        {
            loopManager.BeginRun(testMode: true, quickTestMode: true);
        }

        private void ResetToIdle()
        {
            _state = InputState.Idle;
            _digitBuffer.Clear();
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
