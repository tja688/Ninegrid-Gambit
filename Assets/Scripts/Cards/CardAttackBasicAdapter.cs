using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 场地级 CardAttackBasic 编排适配器：统一管理四向交战 rig、动态对象绑定与即死变体。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardAttackBasicAdapter : MonoBehaviour
    {
        private const float EnemyVictimKnockbackCoefficient = 1f;
        private const float PlayerVictimKnockbackCoefficient = 0.5f;

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

        public async UniTask PlayBasicAttackAsync(
            ManagedCard victim,
            bool lethal,
            CancellationToken cancellationToken = default)
        {
            if (victim?.Transform == null)
            {
                Debug.LogWarning("[CardAttackBasicAdapter] 受击卡无效，跳过交战。");
                return;
            }

            var field = GroundFieldManagerSingleton.Instance;
            if (field == null || !field.TryGetSlotOf(victim.Uid, out var victimSlot))
            {
                Debug.LogWarning("[CardAttackBasicAdapter] 受击卡不在场地中，跳过交战。");
                return;
            }

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

            PrepareAttackerAtAvatarAnchor(field, attacker, victimTransform);
            rig.ResetParticipantMotion(attacker, victimTransform);
            rig.BindParticipants(attacker, victimTransform, victimEffects, lethal,
                victimKnockbackCoefficient: EnemyVictimKnockbackCoefficient);

            await PlayBoundRigAsync(
                rig,
                attacker,
                attackerCard,
                victim,
                victimTransform,
                lethal,
                cancellationToken);
        }

        public async UniTask PlayBasicCounterAttackAsync(
            ManagedCard attacker,
            bool lethal,
            CancellationToken cancellationToken = default)
        {
            if (attacker?.Transform == null)
            {
                Debug.LogWarning("[CardAttackBasicAdapter] 反击攻击者无效，跳过交战。");
                return;
            }

            var field = GroundFieldManagerSingleton.Instance;
            if (field == null || !field.TryGetSlotOf(attacker.Uid, out var attackerSlot))
            {
                Debug.LogWarning("[CardAttackBasicAdapter] 反击攻击者不在场地中，跳过交战。");
                return;
            }

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

            PrepareAttackerAtSlotAnchor(field, attackerTransform, attackerSlot, victimTransform);
            rig.ResetParticipantMotion(attackerTransform, victimTransform);
            rig.BindParticipants(
                attackerTransform,
                victimTransform,
                victimEffects,
                lethal,
                relativeAttackerMotion: true,
                relativeVictimKnockback: true,
                victimKnockbackCoefficient: PlayerVictimKnockbackCoefficient);

            await PlayBoundRigAsync(
                rig,
                attackerTransform,
                attacker,
                victim,
                victimTransform,
                lethal,
                cancellationToken,
                restoreAttackerSlot: attackerSlot,
                restoreVictimSlot: GroundSlotTopology.AvatarReservedSlot);
        }

        private async UniTask PlayBoundRigAsync(
            CardAttackBasicDirectionRig rig,
            Transform attacker,
            ManagedCard attackerCard,
            ManagedCard victim,
            Transform victimTransform,
            bool lethal,
            CancellationToken cancellationToken,
            int? restoreAttackerSlot = null,
            int? restoreVictimSlot = null)
        {
            var field = GroundFieldManagerSingleton.Instance;
            try
            {
                await rig.PlayAsync(cancellationToken);
            }
            finally
            {
                rig.ResetParticipantMotion(attacker, lethal ? null : victimTransform);
                await RestoreCardAtSlotIfNeededAsync(field, attackerCard, restoreAttackerSlot, cancellationToken);
                await RestoreCardAtSlotIfNeededAsync(field, victim, restoreVictimSlot, cancellationToken);

                var cardManager = CardManagerSingleton.Instance;
                if (attackerCard != null)
                {
                    cardManager?.RefreshDisplayMode(attackerCard);
                }

                if (!lethal)
                {
                    cardManager?.RefreshDisplayMode(victim);
                }
            }
        }

        private static async UniTask RestoreCardAtSlotIfNeededAsync(
            GroundFieldManagerSingleton field,
            ManagedCard card,
            int? slot,
            CancellationToken cancellationToken)
        {
            if (!slot.HasValue || card?.Transform == null || field == null)
            {
                return;
            }

            var anchor = field.GetGroundAnchor(slot.Value);
            if (anchor == null)
            {
                return;
            }

            var transform = card.Transform;
            CardDeckTween.KillMotion(transform);

            var distance = Vector3.Distance(transform.position, anchor.position);
            if (distance > 0.02f)
            {
                var completed = false;
                transform
                    .DOMove(anchor.position, 0.12f)
                    .SetEase(Ease.OutCubic)
                    .SetLink(transform.gameObject, LinkBehaviour.KillOnDestroy)
                    .OnComplete(() => completed = true)
                    .OnKill(() => completed = true);

                while (!completed)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
            }
            else
            {
                transform.position = anchor.position;
            }

            transform.rotation = Quaternion.identity;
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

        private void CollectDirectionRigs()
        {
            _rigByDirection.Clear();
            if (directionRigs == null || directionRigs.Count == 0)
            {
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
