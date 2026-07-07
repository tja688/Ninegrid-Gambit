#if UNITY_EDITOR || DEVELOPMENT_BUILD

using UnityEngine;

namespace NineGrid.DevTest
{
    /// <summary>
    /// 场景中唯一的测试按键轮询器。首次访问时自动创建常驻对象。
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class TestKeyInputPoller : MonoBehaviour
    {
        private static TestKeyInputPoller _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void BootstrapStack()
        {
            var stack = Resources.Load<TestKeyStackConfigSO>("DevTest/TestKeyStack");
            if (stack != null)
            {
                TestKeyManager.Instance.SetStack(stack);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstance()
        {
            if (_instance != null)
            {
                return;
            }

            var host = new GameObject(nameof(TestKeyInputPoller));
            _instance = host.AddComponent<TestKeyInputPoller>();
            DontDestroyOnLoad(host);
        }

        private void Update()
        {
            TestKeyManager.Instance.PollInput();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}

#endif
