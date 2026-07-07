#if UNITY_EDITOR || DEVELOPMENT_BUILD

using UnityEngine;

namespace NineGrid.DevTest
{
    /// <summary>
    /// 场景级联栈宿主：Play 开始时将 TestKeyStackConfigSO 注入 TestKeyManager。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9999)]
    public sealed class TestKeyStackHost : MonoBehaviour
    {
        [Tooltip("级联栈 SO。留空时运行时按名称 TestKeyStack 从 Resources/DevTest 加载。")]
        [SerializeField] private TestKeyStackConfigSO stackConfig;

        private void Awake()
        {
            if (stackConfig == null)
            {
                stackConfig = Resources.Load<TestKeyStackConfigSO>("DevTest/TestKeyStack");
            }

            if (stackConfig == null)
            {
                Debug.LogWarning("[TestKeyStackHost] 未配置 TestKeyStackConfigSO，级联栈将为空。");
                return;
            }

            TestKeyManager.Instance.SetStack(stackConfig);
        }
    }
}

#endif
