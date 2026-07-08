#if UNITY_EDITOR || DEVELOPMENT_BUILD

using Cysharp.Threading.Tasks;
using NineGrid.Flow;
using UnityEngine;

namespace NineGrid.DevTest.Flow
{
    [DisallowMultipleComponent]
    public sealed class InBattleManagerDevKeys : TestKeyModuleBehaviour
    {
        [Tooltip("运行时自动查找 InBattleManagerSingleton.Instance；也可手动拖入覆盖。")]
        [SerializeField] private InBattleManagerSingleton inBattleManager;

        protected override string ModuleId => "in-battle-manager";

        protected override void OnEnable()
        {
            if (inBattleManager == null)
            {
                inBattleManager = GetComponent<InBattleManagerSingleton>();
            }

            if (inBattleManager == null)
            {
                inBattleManager = InBattleManagerSingleton.Instance;
            }

            base.OnEnable();
        }

        protected override void ConfigureBindings(TestKeyRegistrationBuilder builder)
        {
            builder
                .Bind(KeyCode.Keypad1, "真实局内入场", () => RunRealBattleEntryAsync().Forget())
                .Bind(KeyCode.Keypad2, "探测节点结算", () => TrySettlement());
        }

        private async UniTaskVoid RunRealBattleEntryAsync()
        {
            var manager = ResolveManager();
            if (manager == null)
            {
                return;
            }

            manager.BootstrapRun();
            await manager.StartBattleNodeAsync();
        }

        private void TrySettlement()
        {
            var manager = ResolveManager();
            if (manager == null)
            {
                return;
            }

            if (!manager.TryEnterNodeSettlement())
            {
                Debug.Log("[InBattleManagerDevKeys] 内核尚未确认通关，未进入结算。");
            }
        }

        private InBattleManagerSingleton ResolveManager()
        {
            if (inBattleManager != null)
            {
                return inBattleManager;
            }

            inBattleManager = InBattleManagerSingleton.Instance;
            if (inBattleManager == null)
            {
                Debug.LogWarning("[InBattleManagerDevKeys] 未找到 InBattleManagerSingleton。");
            }

            return inBattleManager;
        }
    }
}

#endif
