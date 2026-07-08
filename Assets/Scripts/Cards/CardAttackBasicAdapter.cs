using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 场地级 CardAttackBasic 编排适配器：统一管理四向交战 rig、动态对象绑定与即死变体。
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
            CollectDirectionRigs();
            EnsureAttackerProxy();
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
            rig.BindParticipants(attacker, victimTransform, victimEffects, lethal);

            try
            {
                await rig.PlayAsync(cancellationToken);
            }
            finally
            {
                rig.ResetParticipantMotion(attacker, lethal ? null : victimTransform);
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
            if (attacker == null || field == null)
            {
                return;
            }

            var anchor = field.GetGroundAnchor(GroundSlotTopology.AvatarReservedSlot);
            if (anchor == null)
            {
                return;
            }

            attacker.position = anchor.position;

            var direction = victim.position - attacker.position;
            if (direction.sqrMagnitude > 0.0001f)
            {
                var flat = new Vector3(direction.x, direction.y, 0f);
                attacker.rotation = Quaternion.identity;
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
                if (rig == null || rig.Direction == CardBoardDirection.None)
                {
                    continue;
                }

                _rigByDirection[rig.Direction] = rig;
            }
        }
    }
}
