#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace NineGrid.DevTest.Editor
{
    /// <summary>
    /// 非 Play Mode 下点击测试按钮时，自动进入 Play Mode 并在 DevKeys 挂载后执行目标动作。
    /// </summary>
    [InitializeOnLoad]
    internal static class TestKeyRunnerPlayModeLauncher
    {
        private const int MaxDispatchAttempts = 120;

        private static string _pendingLayerId;
        private static KeyCode _pendingKey;
        private static int _dispatchAttempts;

        static TestKeyRunnerPlayModeLauncher()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static bool HasPendingAction =>
            !string.IsNullOrWhiteSpace(_pendingLayerId) && _pendingKey != KeyCode.None;

        public static void RequestRun(string layerId, KeyCode key)
        {
            if (string.IsNullOrWhiteSpace(layerId) || key == KeyCode.None)
            {
                return;
            }

            _pendingLayerId = layerId;
            _pendingKey = key;
            _dispatchAttempts = 0;

            if (EditorApplication.isPlaying)
            {
                EditorApplication.delayCall += TryDispatchPending;
                return;
            }

            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && HasPendingAction)
            {
                _dispatchAttempts = 0;
                EditorApplication.delayCall += TryDispatchPending;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                ClearPending();
            }
        }

        private static void TryDispatchPending()
        {
            if (!HasPendingAction || !EditorApplication.isPlaying)
            {
                return;
            }

            if (TestKeyManager.Instance.TryInvokeRegisteredAction(_pendingLayerId, _pendingKey))
            {
                Debug.Log($"[TestRunner] 已执行测试：{_pendingLayerId} / {_pendingKey}");
                ClearPending();
                return;
            }

            _dispatchAttempts++;
            if (_dispatchAttempts >= MaxDispatchAttempts)
            {
                Debug.LogWarning(
                    $"[TestRunner] 超时未找到可执行回调：{_pendingLayerId} / {_pendingKey}。请确认场景已挂载对应 *DevKeys 组件。");
                ClearPending();
                return;
            }

            EditorApplication.delayCall += TryDispatchPending;
        }

        private static void ClearPending()
        {
            _pendingLayerId = null;
            _pendingKey = KeyCode.None;
            _dispatchAttempts = 0;
        }
    }
}

#endif
