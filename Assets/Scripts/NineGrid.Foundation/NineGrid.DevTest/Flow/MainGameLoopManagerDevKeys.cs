#if UNITY_EDITOR || DEVELOPMENT_BUILD

using NineGrid.Flow;
using UnityEngine;

namespace NineGrid.DevTest.Flow
{
    [DisallowMultipleComponent]
    public sealed class MainGameLoopManagerDevKeys : TestKeyModuleBehaviour
    {
        [Tooltip("运行时自动查找 MainGameLoopManagerSingleton.Instance；也可手动拖入覆盖。")]
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
                loopManager = MainGameLoopManagerSingleton.Instance;
            }

            base.OnEnable();
        }

        protected override void ConfigureBindings(TestKeyRegistrationBuilder builder)
        {
            builder
                .Bind(KeyCode.Keypad0, "进入主游戏循环测试", BeginTestLoop)
                .Bind(KeyCode.Backslash, "快速测试入场 (x2+HP99)", BeginQuickTestLoop);
        }

        private void BeginTestLoop()
        {
            var manager = ResolveManager();
            if (manager == null)
            {
                return;
            }

            manager.BeginRun(testMode: true);
        }

        private void BeginQuickTestLoop()
        {
            var manager = ResolveManager();
            if (manager == null)
            {
                return;
            }

            if (manager.State != MainGameLoopManagerSingleton.LoopState.MainMenu)
            {
                Debug.LogWarning("[MainGameLoopManagerDevKeys] 快速测试仅可在主菜单触发。");
                return;
            }

            manager.BeginRun(testMode: true, quickTestMode: true);
        }

        private MainGameLoopManagerSingleton ResolveManager()
        {
            if (loopManager != null)
            {
                return loopManager;
            }

            loopManager = MainGameLoopManagerSingleton.Instance;
            if (loopManager == null)
            {
                Debug.LogWarning("[MainGameLoopManagerDevKeys] 未找到 MainGameLoopManagerSingleton。");
            }

            return loopManager;
        }
    }
}

#endif
