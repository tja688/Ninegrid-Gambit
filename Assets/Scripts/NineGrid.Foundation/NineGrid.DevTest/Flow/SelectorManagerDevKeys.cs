#if UNITY_EDITOR || DEVELOPMENT_BUILD

using NineGrid.Flow;
using UnityEngine;

namespace NineGrid.DevTest.Flow
{
    [DisallowMultipleComponent]
    public sealed class SelectorManagerDevKeys : TestKeyModuleBehaviour
    {
        [Tooltip("运行时自动查找 SelectorManagerSingleton.Instance；也可手动拖入覆盖。")]
        [SerializeField] private SelectorManagerSingleton selectorManager;

        protected override string ModuleId => "selector-manager";

        protected override void OnEnable()
        {
            if (selectorManager == null)
            {
                selectorManager = GetComponent<SelectorManagerSingleton>();
            }

            if (selectorManager == null)
            {
                selectorManager = SelectorManagerSingleton.Instance;
            }

            base.OnEnable();
        }

        protected override void ConfigureBindings(TestKeyRegistrationBuilder builder)
        {
            builder.Bind(KeyCode.Keypad9, "弹出Bounce三选一", ShowBounceChoice);
        }

        private void ShowBounceChoice()
        {
            var manager = ResolveManager();
            if (manager == null)
            {
                return;
            }

            if (manager.IsChoiceActive)
            {
                manager.HideChoice();
            }

            manager.BeginBounceChoice(3, (index, defId) =>
            {
                Debug.Log($"[SelectorManagerDevKeys] 已选择 index={index} defId={defId}");
            });
        }

        private SelectorManagerSingleton ResolveManager()
        {
            if (selectorManager != null)
            {
                return selectorManager;
            }

            selectorManager = SelectorManagerSingleton.Instance;
            if (selectorManager == null)
            {
                Debug.LogWarning("[SelectorManagerDevKeys] 未找到 SelectorManagerSingleton。");
            }

            return selectorManager;
        }
    }
}

#endif
