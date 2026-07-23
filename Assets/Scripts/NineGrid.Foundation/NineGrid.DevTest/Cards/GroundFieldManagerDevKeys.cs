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
        [Tooltip("运行时查找场景中的 GroundFieldView；也可手动拖入覆盖。")]
        [SerializeField] private GroundFieldView fieldManager;

        [Tooltip("运行时查找场景中的 FieldBattleView；也可手动拖入覆盖。")]
        [SerializeField] private FieldBattleView battleManager;

        protected override string ModuleId => "ground-field-manager";

        protected override void OnEnable()
        {
            if (fieldManager == null)
            {
                fieldManager = GetComponent<GroundFieldView>();
            }

            if (fieldManager == null)
            {
                fieldManager = UnityEngine.Object.FindFirstObjectByType<GroundFieldView>();
            }

            if (battleManager == null)
            {
                battleManager = UnityEngine.Object.FindFirstObjectByType<FieldBattleView>();
            }

            base.OnEnable();
        }

        protected override void ConfigureBindings(TestKeyRegistrationBuilder builder)
        {
            builder
                .Bind(KeyCode.Keypad1, "导演攻击相邻怪（未击杀走反击锁步）", RunDirectorAttackAdjacent)
                .Bind(KeyCode.Keypad4, "外圈全体顺时针旋转", () => RunRotateOuterRingAsync().Forget())
                .Bind(KeyCode.Keypad5, "随机移除场中1张", () => RunRandomRemoveAsync().Forget())
                .Bind(KeyCode.Keypad6, "即死击杀相邻怪", RunLethalAttackAdjacent)
                .Bind(KeyCode.Keypad7, "移除1张并立即旋转", () => RunRemoveAndRotateAsync().Forget())
                .Bind(KeyCode.Keypad8, "下一次交战即死", ArmNextLethalAttack);
        }

        /// <summary>
        /// #2 硬切：DevTest 也只提交导演攻击意图；未击杀时反击由主线 AttackCounter 锁步播，不再走旧旁路。
        /// </summary>
        private void RunDirectorAttackAdjacent()
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

            Debug.Log($"[GroundFieldManagerDevKeys] 导演攻击意图: slot={slot} uid={attacker.Uid}");
            if (AttackInputHook.TrySubmitAttack == null)
            {
                Debug.LogWarning("[GroundFieldManagerDevKeys] AttackInputHook.TrySubmitAttack 未装配。");
                return;
            }

            AttackInputHook.TrySubmitAttack(slot);
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
            var battle = ResolveBattleManager();
            if (battle == null)
            {
                return;
            }

            battle.ArmNextLethalAttack(true);
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

        private void RunLethalAttackAdjacent()
        {
            var field = ResolveFieldManager();
            var battle = ResolveBattleManager();
            if (field == null || battle == null)
            {
                return;
            }

            if (field.IsBusy)
            {
                Debug.LogWarning("[GroundFieldManagerDevKeys] 场地管理器忙碌，请稍后再试。");
                return;
            }

            battle.ArmNextLethalAttack(true);

            var avatarSlot = GroundSlotTopology.AvatarReservedSlot;
            var neighbors = GroundSlotTopology.GetNeighbors(avatarSlot, GroundSlotRelation.Orthogonal);
            for (var i = 0; i < neighbors.Count; i++)
            {
                var slot = neighbors[i];
                if (!field.TryGetCardAt(slot, out var card) || card.IsFieldDead)
                {
                    continue;
                }

                // #11：DevTest 进攻也走导演意图，不再走已删的旧编排链。
                if (AttackInputHook.TrySubmitAttack == null)
                {
                    Debug.LogWarning("[GroundFieldManagerDevKeys] AttackInputHook.TrySubmitAttack 未装配。");
                    return;
                }

                AttackInputHook.TrySubmitAttack(slot);
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

        private GroundFieldView ResolveFieldManager()
        {
            if (fieldManager != null)
            {
                return fieldManager;
            }

            fieldManager = UnityEngine.Object.FindFirstObjectByType<GroundFieldView>();
            if (fieldManager == null)
            {
                Debug.LogWarning("[GroundFieldManagerDevKeys] 未找到 GroundFieldView。");
            }

            return fieldManager;
        }

        private FieldBattleView ResolveBattleManager()
        {
            if (battleManager != null)
            {
                return battleManager;
            }

            battleManager = UnityEngine.Object.FindFirstObjectByType<FieldBattleView>();
            if (battleManager == null)
            {
                Debug.LogWarning("[GroundFieldManagerDevKeys] 未找到 FieldBattleView。");
            }

            return battleManager;
        }
    }
}

#endif
