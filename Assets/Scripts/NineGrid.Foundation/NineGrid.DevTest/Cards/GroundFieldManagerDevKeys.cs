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
                .Bind(KeyCode.Keypad1, "随机相邻怪反击玩家", () => RunRandomCounterAttackAsync().Forget())
                .Bind(KeyCode.Keypad4, "外圈全体顺时针旋转", () => RunRotateOuterRingAsync().Forget())
                .Bind(KeyCode.Keypad5, "随机移除场中1张", () => RunRandomRemoveAsync().Forget())
                .Bind(KeyCode.Keypad6, "即死击杀相邻怪", () => RunLethalAttackAdjacentAsync().Forget())
                .Bind(KeyCode.Keypad7, "移除1张并立即旋转", () => RunRemoveAndRotateAsync().Forget())
                .Bind(KeyCode.Keypad8, "下一次交战即死", ArmNextLethalAttack);
        }

        private async UniTaskVoid RunRandomCounterAttackAsync()
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

            if (!field.TryGetRandomAvatarOrthogonalMonsterSlot(out var slot, out var attacker))
            {
                Debug.LogWarning("[GroundFieldManagerDevKeys] Avatar 四向相邻格无可用怪物。");
                return;
            }

            Debug.Log($"[GroundFieldManagerDevKeys] 触发怪物反击: slot={slot} uid={attacker.Uid}");
            await field.RequestBasicCounterAttackAtSlotAsync(slot);
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

        private async UniTaskVoid RunLethalAttackAdjacentAsync()
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

            field.ArmNextLethalAttack(true);

            var avatarSlot = GroundSlotTopology.AvatarReservedSlot;
            var neighbors = GroundSlotTopology.GetNeighbors(avatarSlot, GroundSlotRelation.Orthogonal);
            for (var i = 0; i < neighbors.Count; i++)
            {
                var slot = neighbors[i];
                if (!field.TryGetCardAt(slot, out var card) || card.IsFieldDead)
                {
                    continue;
                }

                await field.RequestBasicAttackAtSlotAsync(slot);
                return;
            }

            Debug.LogWarning("[GroundFieldManagerDevKeys] Avatar 四向相邻格无可用交战目标。");
        }

        private async UniTaskVoid RunRemoveAndRotateAsync()
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
            await field.RotateOuterRingClockwiseAsync();
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
