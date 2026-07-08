#if UNITY_EDITOR || DEVELOPMENT_BUILD

using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.DevTest;
using UnityEngine;

namespace NineGrid.DevTest.Cards
{
    [DisallowMultipleComponent]
    public sealed class GroundFieldManagerDevKeys : TestKeyModuleBehaviour
    {
        [Tooltip("运行时自动查找 GroundFieldManagerSingleton.Instance；也可手动拖入覆盖。")]
        [SerializeField] private GroundFieldManagerSingleton fieldManager;

        protected override string ModuleId => "ground-field-manager";

        protected override void OnEnable()
        {
            if (fieldManager == null)
            {
                fieldManager = GetComponent<GroundFieldManagerSingleton>();
            }

            if (fieldManager == null)
            {
                fieldManager = GroundFieldManagerSingleton.Instance;
            }

            base.OnEnable();
        }

        protected override void ConfigureBindings(TestKeyRegistrationBuilder builder)
        {
            builder
                .Bind(KeyCode.Keypad4, "外圈全体顺时针旋转", () => RunRotateOuterRingAsync().Forget())
                .Bind(KeyCode.Keypad5, "随机移除场中1张", () => RunRandomRemoveAsync().Forget())
                .Bind(KeyCode.Keypad8, "下一次交战即死", ArmNextLethalAttack);
        }

        private async UniTaskVoid RunRotateOuterRingAsync()
        {
            var field = ResolveFieldManager();
            if (field == null)
            {
                return;
            }

            if (field.IsBusy)
            {
                Debug.LogWarning("[GroundFieldManagerDevKeys] 场地管理器忙碌，请稍后再试。");
                return;
            }

            await field.RotateOuterRingClockwiseAsync();
        }

        private void ArmNextLethalAttack()
        {
            var field = ResolveFieldManager();
            if (field == null)
            {
                return;
            }

            field.ArmNextLethalAttack(true);
            Debug.Log("[GroundFieldManagerDevKeys] 已武装下一次交战为即死效果。");
        }

        private async UniTaskVoid RunRandomRemoveAsync()
        {
            var field = ResolveFieldManager();
            if (field == null)
            {
                return;
            }

            if (field.IsBusy)
            {
                Debug.LogWarning("[GroundFieldManagerDevKeys] 场地管理器忙碌，请稍后再试。");
                return;
            }

            if (!field.TryGetRandomOccupiedCard(out var card))
            {
                Debug.LogWarning("[GroundFieldManagerDevKeys] 场上无卡牌可移除。");
                return;
            }

            field.RequestRemoveFromField(card.Uid, animate: true);
            await UniTask.CompletedTask;
        }

        private GroundFieldManagerSingleton ResolveFieldManager()
        {
            if (fieldManager != null)
            {
                return fieldManager;
            }

            fieldManager = GroundFieldManagerSingleton.Instance;
            if (fieldManager == null)
            {
                Debug.LogWarning("[GroundFieldManagerDevKeys] 未找到 GroundFieldManagerSingleton。");
            }

            return fieldManager;
        }
    }
}

#endif
