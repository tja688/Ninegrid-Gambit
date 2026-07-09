using System.Collections.Generic;
using System.Threading;
using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 场地级 CardAttackBasic 执行器：管理四向交战 rig，按 BattleBindParams 绑参播放。
    /// 编排路由由 FieldBattleManager + Catalog 负责，本类不决定变体语义。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardAttackBasicAdapter : MonoBehaviour
    {
        [Header("Rigs")]
        [Tooltip("CardAttackBasic 根节点；留空时 Awake 按子节点名 CardAttackBasic 查找。")]
        [SerializeField] private Transform rigsRoot;

        [Tooltip("四向 rig；留空时 Awake 从 rigsRoot 收集 CardAttackBasicDirectionRig。")]
        [SerializeField] private List<CardAttackBasicDirectionRig> directionRigs = new();

        [Header("Attacker")]
        [Tooltip("攻击者兜底 Transform。正常优先使用场地格5入场的 Avatar 卡；两者皆空时用 AttackerProxy（运行时创建在 Avatar 格位锚点）。")]
        [SerializeField] private Transform defaultAttacker;

        private readonly Dictionary<CardBoardDirection, CardAttackBasicDirectionRig> _rigByDirection = new();
        private Transform _attackerProxy;
        private bool _nextAttackLethal;

        public bool IsNextAttackLethal => _nextAttackLethal;

        private void Awake()
        {
            ResolveRigsRoot();
            EnsureAttackerProxy();
        }

        private void Start()
        {
            CollectDirectionRigs();
        }

        public void ArmNextLethalAttack(bool armed = true)
        {
            _nextAttackLethal = armed;
        }

        public bool ConsumeNextLethalArmed()
        {
            if (!_nextAttackLethal)
            {
                return false;
            }

            _nextAttackLethal = false;
            return true;
        }

        public bool TryResolveDirectionForVictimSlot(int victimSlot, out CardBoardDirection direction)
        {
            direction = CardBoardDirection.None;
            if (!GroundSlotTopology.IsValidSlot(victimSlot)
                || !GroundSlotTopology.AreOrthogonal(victimSlot, GroundSlotTopology.AvatarReservedSlot))
            {
                return false;
            }

            direction = CardBoardDirectionUtility.ComputeSelfDirection(
                victimSlot,
                GroundSlotTopology.AvatarReservedSlot);
            return direction != CardBoardDirection.None && _rigByDirection.ContainsKey(direction);
        }

        /// <summary>
        /// 怪物反击玩家：攻击格与 Avatar 正交相邻时，选用与玩家进攻相反方向的 rig，并互换攻击者/受击者绑定。
        /// </summary>
        public bool TryResolveCounterAttackDirectionForAttackerSlot(int attackerSlot, out CardBoardDirection direction)
        {
            direction = CardBoardDirection.None;
            if (!TryResolveDirectionForVictimSlot(attackerSlot, out var avatarToMonster))
            {
                return false;
            }

            direction = CardBoardDirectionUtility.GetOrthogonalOpposite(avatarToMonster);
            return direction != CardBoardDirection.None && _rigByDirection.ContainsKey(direction);
        }

        /// <summary>
        /// 兼容旧调用：按 lethal 构造默认绑参后播放基础进攻。
        /// </summary>
        public UniTask PlayBasicAttackAsync(
            ManagedCard victim,
            bool lethal,
            CancellationToken cancellationToken = default)
        {
            var intent = BattleIntentUtility.FromFlags(counter: false, lethal);
            return PlayBasicAttackAsync(victim, BattleBindParams.CreateSafeFallback(intent), cancellationToken);
        }

        public async UniTask PlayBasicAttackAsync(
            ManagedCard victim,
            BattleBindParams bind,
            CancellationToken cancellationToken = default)
        {
            await PlayBasicAttackAsync(victim, bind, onCombatHit: null, cancellationToken);
        }

        public async UniTask PlayBasicAttackAsync(
            ManagedCard victim,
            BattleBindParams bind,
            Action onCombatHit,
            CancellationToken cancellationToken = default)
        {
            if (victim?.Transform == null)
            {
                Debug.LogWarning("[CardAttackBasicAdapter] 受击卡无效，跳过交战。");
                return;
            }

            if (!string.Equals(bind.RigFamily, BattleEncounterProfileSO.DefaultRigFamily, System.StringComparison.Ordinal))
            {
                Debug.LogWarning(
                    $"[CardAttackBasicAdapter] 暂不支持 RigFamily='{bind.RigFamily}'，回退 {BattleEncounterProfileSO.DefaultRigFamily}。",
                    this);
            }

            var field = GroundFieldManagerSingleton.Instance;
            if (field == null || !field.TryGetSlotOf(victim.Uid, out var victimSlot))
            {
                Debug.LogWarning("[CardAttackBasicAdapter] 受击卡不在场地中，跳过交战。");
                return;
            }

            EnsureRigsCollected();
            if (!TryResolveDirectionForVictimSlot(victimSlot, out var direction)
                || !_rigByDirection.TryGetValue(direction, out var rig))
            {
                Debug.LogWarning($"[CardAttackBasicAdapter] 格位 {victimSlot} 无可用交战 rig。", this);
                return;
            }

            if (!TryResolveAttacker(field, out var attackerCard, out var attacker))
            {
                Debug.LogWarning("[CardAttackBasicAdapter] 未找到 Avatar 攻击者，跳过交战。");
                return;
            }

            var victimTransform = victim.Transform;
            victim.TryGetEffectManager(out var victimEffects);

            var attackerSnapshot = BattleFinalStateGuard.Capture(
                attackerCard,
                field,
                GroundSlotTopology.AvatarReservedSlot);
            var victimSnapshot = BattleFinalStateGuard.Capture(victim, field, victimSlot);

            PrepareAttackerAtAvatarAnchor(field, attacker, victimTransform);
            rig.ResetParticipantMotion(attacker, victimTransform);
            rig.BindParticipants(attacker, victimTransform, victimEffects, in bind, onCombatHit);

            await PlayBoundRigAsync(
                rig,
                attacker,
                attackerSnapshot,
                victimSnapshot,
                bind,
                cancellationToken);
        }

        /// <summary>
        /// 兼容旧调用：按 lethal 构造默认绑参后播放反击。
        /// </summary>
        public UniTask PlayBasicCounterAttackAsync(
            ManagedCard attacker,
            bool lethal,
            CancellationToken cancellationToken = default)
        {
            var intent = BattleIntentUtility.FromFlags(counter: true, lethal);
            return PlayBasicCounterAttackAsync(attacker, BattleBindParams.CreateSafeFallback(intent), cancellationToken);
        }

        public async UniTask PlayBasicCounterAttackAsync(
            ManagedCard attacker,
            BattleBindParams bind,
            CancellationToken cancellationToken = default)
        {
            await PlayBasicCounterAttackAsync(attacker, bind, onCombatHit: null, cancellationToken);
        }

        public async UniTask PlayBasicCounterAttackAsync(
            ManagedCard attacker,
            BattleBindParams bind,
            Action onCombatHit,
            CancellationToken cancellationToken = default)
        {
            if (attacker?.Transform == null)
            {
                Debug.LogWarning("[CardAttackBasicAdapter] 反击攻击者无效，跳过交战。");
                return;
            }

            if (!string.Equals(bind.RigFamily, BattleEncounterProfileSO.DefaultRigFamily, System.StringComparison.Ordinal))
            {
                Debug.LogWarning(
                    $"[CardAttackBasicAdapter] 暂不支持 RigFamily='{bind.RigFamily}'，回退 {BattleEncounterProfileSO.DefaultRigFamily}。",
                    this);
            }

            var field = GroundFieldManagerSingleton.Instance;
            if (field == null || !field.TryGetSlotOf(attacker.Uid, out var attackerSlot))
            {
                Debug.LogWarning("[CardAttackBasicAdapter] 反击攻击者不在场地中，跳过交战。");
                return;
            }

            EnsureRigsCollected();
            if (!TryResolveCounterAttackDirectionForAttackerSlot(attackerSlot, out var direction)
                || !_rigByDirection.TryGetValue(direction, out var rig))
            {
                Debug.LogWarning($"[CardAttackBasicAdapter] 格位 {attackerSlot} 无可用反击 rig。", this);
                return;
            }

            if (!field.TryGetCardAt(GroundSlotTopology.AvatarReservedSlot, out var victim)
                || victim?.Transform == null)
            {
                Debug.LogWarning("[CardAttackBasicAdapter] 未找到 Avatar 受击者，跳过反击。");
                return;
            }

            var attackerTransform = attacker.Transform;
            var victimTransform = victim.Transform;
            victim.TryGetEffectManager(out var victimEffects);

            var attackerSnapshot = BattleFinalStateGuard.Capture(attacker, field, attackerSlot);
            var victimSnapshot = BattleFinalStateGuard.Capture(
                victim,
                field,
                GroundSlotTopology.AvatarReservedSlot);

            PrepareAttackerAtSlotAnchor(field, attackerTransform, attackerSlot, victimTransform);
            rig.ResetParticipantMotion(attackerTransform, victimTransform);
            rig.BindParticipants(attackerTransform, victimTransform, victimEffects, in bind, onCombatHit);

            await PlayBoundRigAsync(
                rig,
                attackerTransform,
                attackerSnapshot,
                victimSnapshot,
                bind,
                cancellationToken);
        }

        private async UniTask PlayBoundRigAsync(
            CardAttackBasicDirectionRig rig,
            Transform attacker,
            BattleFinalStateGuard.ParticipantSnapshot attackerSnapshot,
            BattleFinalStateGuard.ParticipantSnapshot victimSnapshot,
            BattleBindParams bind,
            CancellationToken cancellationToken)
        {
            var field = GroundFieldManagerSingleton.Instance;
            var victimTransform = victimSnapshot.Transform;
            try
            {
                await rig.PlayAsync(cancellationToken);
            }
            finally
            {
                rig.ResetParticipantMotion(attacker, bind.IsLethal ? null : victimTransform);
                await BattleFinalStateGuard.RestorePairAsync(
                    attackerSnapshot,
                    victimSnapshot,
                    field,
                    bind,
                    CancellationToken.None);
            }
        }

        /// <summary>
        /// 优先取场地格5入场的 Avatar 卡；否则用 Inspector defaultAttacker / AttackerProxy 兜底。
        /// </summary>
        private bool TryResolveAttacker(
            GroundFieldManagerSingleton field,
            out ManagedCard attackerCard,
            out Transform attacker)
        {
            attackerCard = null;
            attacker = null;

            if (field != null
                && field.TryGetCardAt(GroundSlotTopology.AvatarReservedSlot, out attackerCard)
                && attackerCard?.Transform != null)
            {
                attacker = attackerCard.Transform;
                return true;
            }

            if (defaultAttacker != null)
            {
                attacker = defaultAttacker;
                return true;
            }

            EnsureAttackerProxy();
            if (_attackerProxy != null && field != null)
            {
                var anchor = field.GetGroundAnchor(GroundSlotTopology.AvatarReservedSlot);
                if (anchor != null)
                {
                    _attackerProxy.position = anchor.position;
                }
            }

            attacker = _attackerProxy;
            return attacker != null;
        }

        private static void PrepareAttackerAtAvatarAnchor(
            GroundFieldManagerSingleton field,
            Transform attacker,
            Transform victim)
        {
            PrepareAttackerAtSlotAnchor(
                field,
                attacker,
                GroundSlotTopology.AvatarReservedSlot,
                victim);
        }

        private static void PrepareAttackerAtSlotAnchor(
            GroundFieldManagerSingleton field,
            Transform attacker,
            int attackerSlot,
            Transform victim)
        {
            if (attacker == null || field == null)
            {
                return;
            }

            var anchor = field.GetGroundAnchor(attackerSlot);
            if (anchor == null)
            {
                return;
            }

            attacker.position = anchor.position;

            if (victim != null)
            {
                var direction = victim.position - attacker.position;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    attacker.rotation = Quaternion.identity;
                }
            }
        }

        private void EnsureAttackerProxy()
        {
            if (defaultAttacker != null || _attackerProxy != null)
            {
                return;
            }

            var existing = transform.Find("AttackerProxy");
            if (existing != null)
            {
                _attackerProxy = existing;
                return;
            }

            var proxyObject = new GameObject("AttackerProxy");
            proxyObject.transform.SetParent(transform, false);
            _attackerProxy = proxyObject.transform;
        }

        private void ResolveRigsRoot()
        {
            if (rigsRoot != null)
            {
                return;
            }

            rigsRoot = transform.Find("CardAttackBasic");
            if (rigsRoot == null)
            {
                rigsRoot = transform;
            }
        }

        private void EnsureRigsCollected()
        {
            if (_rigByDirection.Count > 0)
            {
                return;
            }

            CollectDirectionRigs();
        }

        private void CollectDirectionRigs()
        {
            _rigByDirection.Clear();
            if (directionRigs == null || directionRigs.Count == 0)
            {
                if (rigsRoot == null)
                {
                    ResolveRigsRoot();
                }

                directionRigs = new List<CardAttackBasicDirectionRig>(
                    rigsRoot.GetComponentsInChildren<CardAttackBasicDirectionRig>(true));
            }

            for (var i = 0; i < directionRigs.Count; i++)
            {
                var rig = directionRigs[i];
                if (rig == null)
                {
                    continue;
                }

                var direction = rig.Direction != CardBoardDirection.None
                    ? rig.Direction
                    : CardAttackBasicDirectionRig.ResolveDirectionFromName(rig.name);
                if (direction == CardBoardDirection.None)
                {
                    continue;
                }

                _rigByDirection[direction] = rig;
            }
        }
    }
}
