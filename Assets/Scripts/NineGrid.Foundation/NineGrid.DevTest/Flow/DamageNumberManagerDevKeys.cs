#if UNITY_EDITOR || DEVELOPMENT_BUILD

using NineGrid.Flow;
using UnityEngine;

namespace NineGrid.DevTest.Flow
{
    [DisallowMultipleComponent]
    public sealed class DamageNumberManagerDevKeys : TestKeyModuleBehaviour
    {
        [Tooltip("运行时自动查找 DamageNumberManagerSingleton.Instance；也可手动拖入覆盖。")]
        [SerializeField] private DamageNumberManagerSingleton damageNumberManager;

        protected override string ModuleId => "damage-number-manager";

        protected override void OnEnable()
        {
            if (damageNumberManager == null)
            {
                damageNumberManager = GetComponent<DamageNumberManagerSingleton>();
            }

            if (damageNumberManager == null)
            {
                damageNumberManager = DamageNumberManagerSingleton.Instance;
            }

            base.OnEnable();
        }

        protected override void ConfigureBindings(TestKeyRegistrationBuilder builder)
        {
            builder.Bind(KeyCode.Alpha2, "屏幕中心随机伤害数字", () => SpawnRandomAtScreenCenter());
        }

        private void SpawnRandomAtScreenCenter()
        {
            var manager = ResolveManager();
            if (manager == null)
            {
                return;
            }

            manager.SpawnRandomAtScreenCenter();
        }

        private DamageNumberManagerSingleton ResolveManager()
        {
            if (damageNumberManager != null)
            {
                return damageNumberManager;
            }

            damageNumberManager = DamageNumberManagerSingleton.Instance;
            if (damageNumberManager == null)
            {
                Debug.LogWarning("[DamageNumberManagerDevKeys] 未找到 DamageNumberManagerSingleton。");
            }

            return damageNumberManager;
        }
    }
}

#endif
