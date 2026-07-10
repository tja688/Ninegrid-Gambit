#if UNITY_EDITOR || DEVELOPMENT_BUILD

using NineGrid.Flow;
using UnityEngine;

namespace NineGrid.DevTest.Flow
{
    [DisallowMultipleComponent]
    public sealed class GoldGainFxManagerDevKeys : TestKeyModuleBehaviour
    {
        [Tooltip("运行时自动查找 GoldGainFxManagerSingleton.Instance；也可手动拖入覆盖。")]
        [SerializeField] private GoldGainFxManagerSingleton goldGainFxManager;

        protected override string ModuleId => "gold-gain-fx";

        protected override void OnEnable()
        {
            if (goldGainFxManager == null)
            {
                goldGainFxManager = GetComponent<GoldGainFxManagerSingleton>();
            }

            if (goldGainFxManager == null)
            {
                goldGainFxManager = GoldGainFxManagerSingleton.Instance;
            }

            base.OnEnable();
        }

        protected override void ConfigureBindings(TestKeyRegistrationBuilder builder)
        {
            builder.Bind(KeyCode.Keypad9, "屏幕中心飞入10金币", () => SpawnTenAtScreenCenter());
        }

        private void SpawnTenAtScreenCenter()
        {
            var manager = ResolveManager();
            if (manager == null)
            {
                return;
            }

            manager.PlayVisualGainAtScreenCenter(10);
        }

        private GoldGainFxManagerSingleton ResolveManager()
        {
            if (goldGainFxManager != null)
            {
                return goldGainFxManager;
            }

            goldGainFxManager = GoldGainFxManagerSingleton.Instance;
            if (goldGainFxManager == null)
            {
                Debug.LogWarning("[GoldGainFxManagerDevKeys] 未找到 GoldGainFxManagerSingleton。");
            }

            return goldGainFxManager;
        }
    }
}

#endif
