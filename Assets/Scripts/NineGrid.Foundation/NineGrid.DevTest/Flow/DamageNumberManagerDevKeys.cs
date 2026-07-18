#if UNITY_EDITOR || DEVELOPMENT_BUILD

using UnityEngine;

namespace NineGrid.DevTest.Flow
{
    [DisallowMultipleComponent]
    public sealed class DamageNumberManagerDevKeys : TestKeyModuleBehaviour
    {
        protected override string ModuleId => "damage-number-manager";

        protected override void OnEnable()
        {
            // 伤害飘字 DevTest 已下线，不再向 TestKeyManager 注册按键。
        }

        protected override void OnDisable()
        {
        }

        protected override void ConfigureBindings(TestKeyRegistrationBuilder builder)
        {
        }
    }
}

#endif
