#if UNITY_EDITOR || DEVELOPMENT_BUILD

using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Presentation.Commands;
using UnityEngine;

namespace NineGrid.DevTest.Flow
{
    [DisallowMultipleComponent]
    public sealed class MainGameLoopManagerDevKeys : TestKeyModuleBehaviour
    {
        [Tooltip("运行时查找场景中的 MainGameLoopManagerSingleton；也可手动拖入覆盖。")]
        [SerializeField] private MainGameLoopManagerSingleton loopManager;

        protected override string ModuleId => "main-game-loop";

        protected override void OnEnable()
        {
            if (loopManager == null)
            {
                loopManager = GetComponent<MainGameLoopManagerSingleton>();
            }

            if (loopManager == null)
            {
                loopManager = UnityEngine.Object.FindFirstObjectByType<MainGameLoopManagerSingleton>();
            }

            base.OnEnable();
        }

        protected override void ConfigureBindings(TestKeyRegistrationBuilder builder)
        {
            builder
                .Bind(KeyCode.Keypad0, "进入主游戏循环测试", BeginTestLoop);
        }

        private void BeginTestLoop()
        {
            var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            if (arch != null)
            {
                arch.SendCommand(new BeginGameFlowRunCommand(testMode: true));
                return;
            }

            var manager = ResolveManager();
            if (manager == null)
            {
                return;
            }

            manager.BeginRun(testMode: true);
        }

        private MainGameLoopManagerSingleton ResolveManager()
        {
            if (loopManager != null)
            {
                return loopManager;
            }

            loopManager = UnityEngine.Object.FindFirstObjectByType<MainGameLoopManagerSingleton>();
            if (loopManager == null)
            {
                Debug.LogWarning("[MainGameLoopManagerDevKeys] 未找到 MainGameLoopManagerSingleton。");
            }

            return loopManager;
        }
    }
}

#endif
