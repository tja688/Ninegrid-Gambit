using System.Collections.Generic;
using System.Threading;
using System;
using Cysharp.Threading.Tasks;
using NineGrid.Core;
using NineGrid.Flow.Presentation;
using QFramework;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.Cards
{
    /// <summary>
    /// 场地级 CardAttackBasic 执行器：管理交战 rig，按 BattleBindParams 绑参播放。
    /// 玩家主动开战仍只接四向正交；敌方单向打击（Counter）支持对角，缺对角 rig 时用相对冲刺回退四向。
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

        private static int ResolveAvatarSlot(GroundFieldView field = null)
        {
            var board = NineGridArchitecture.Current?.GetModel<BoardModel>();
            if (board != null && board.AvatarSlot.Value.IsBoardSlot)
            {
                return board.AvatarSlot.Value.Index;
            }

            if (field != null)
            {
                for (var s = GroundSlotTopology.MinSlot; s <= GroundSlotTopology.MaxSlot; s++)
                {
                    if (field.TryGetCardAt(s, out var card)
                        && card != null
                        && card.CoreKind == CardPresentationKind.Avatar)
                    {
                        return s;
                    }
                }
            }

            return GroundSlotTopology.AvatarReservedSlot;
        }

        public bool TryResolveDirectionForVictimSlot(int victimSlot, out CardBoardDirection direction)
        {
            var avatarSlot = ResolveAvatarSlot(GroundFieldGeometryHook.FieldOrNull());
            return TryResolveDirectionForVictimSlot(victimSlot, avatarSlot, out direction);
        }

        public bool TryResolveDirectionForVictimSlot(int victimSlot, int avatarSlot, out CardBoardDirection direction)
        {
            direction = CardBoardDirection.None;
            if (!GroundSlotTopology.IsValidSlot(victimSlot)
                || !GroundSlotTopology.IsValidSlot(avatarSlot)
                || !GroundSlotTopology.AreOrthogonal(victimSlot, avatarSlot))
            {
                return false;
            }

            direction = CardBoardDirectionUtility.ComputeSelfDirection(
                victimSlot,
                avatarSlot);
            if (direction == CardBoardDirection.None)
            {
                return false;
            }

            EnsureRigsCollected();
            return _rigByDirection.Count == 0 || _rigByDirection.ContainsKey(direction);
        }

        /// <summary>
        /// 怪物→Avatar 单向打击 Present：攻击格与 Avatar 正交或对角相邻时，选用反向方向 rig。
        /// 场景仅有四向 rig 时，对角回退到任意可用四向（Counter 绑参使用相对冲刺朝真实 Avatar）。
        /// </summary>
        public bool TryResolveCounterAttackDirectionForAttackerSlot(int attackerSlot, out CardBoardDirection direction)
        {
            var avatarSlot = ResolveAvatarSlot(GroundFieldGeometryHook.FieldOrNull());
            return TryResolveCounterAttackDirectionForAttackerSlot(attackerSlot, avatarSlot, out direction);
        }

        public bool TryResolveCounterAttackDirectionForAttackerSlot(int attackerSlot, int avatarSlot, out CardBoardDirection direction)
        {
            direction = CardBoardDirection.None;
            if (!GroundSlotTopology.IsValidSlot(attackerSlot)
                || !GroundSlotTopology.IsValidSlot(avatarSlot)
                || !GroundSlotTopology.AreAdjacentEight(
                    attackerSlot,
                    avatarSlot))
            {
                return false;
            }

            var avatarToMonster = CardBoardDirectionUtility.ComputeSelfDirection(
                attackerSlot,
                avatarSlot);
            if (avatarToMonster == CardBoardDirection.None)
            {
                return false;
            }

            EnsureRigsCollected();
            if (_rigByDirection.Count == 0)
            {
                var fallbackOpposite = CardBoardDirectionUtility.GetOpposite(avatarToMonster);
                direction = fallbackOpposite != CardBoardDirection.None ? fallbackOpposite : avatarToMonster;
                return true;
            }

            var preferred = CardBoardDirectionUtility.GetOpposite(avatarToMonster);
            if (preferred != CardBoardDirection.None
                && _rigByDirection.ContainsKey(preferred))
            {
                direction = preferred;
                return true;
            }

            return TryPickFallbackCounterRig(avatarToMonster, out direction);
        }

        /// <summary>
        /// 对角无专用 rig 时，优先取对角分量上的正交向，再任意可用四向。
        /// </summary>
        private bool TryPickFallbackCounterRig(
            CardBoardDirection avatarToMonster,
            out CardBoardDirection direction)
        {
            direction = CardBoardDirection.None;
            foreach (var candidate in EnumerateCounterFallbackDirections(avatarToMonster))
            {
                if (_rigByDirection.ContainsKey(candidate))
                {
                    direction = candidate;
                    return true;
                }
            }

            foreach (var pair in _rigByDirection)
            {
                direction = pair.Key;
                return true;
            }

            return false;
        }

        private static IEnumerable<CardBoardDirection> EnumerateCounterFallbackDirections(
            CardBoardDirection avatarToMonster)
        {
            switch (avatarToMonster)
            {
                case CardBoardDirection.UpLeft:
                    yield return CardBoardDirection.DownRight;
                    yield return CardBoardDirection.Down;
                    yield return CardBoardDirection.Right;
                    break;
                case CardBoardDirection.UpRight:
                    yield return CardBoardDirection.DownLeft;
                    yield return CardBoardDirection.Down;
                    yield return CardBoardDirection.Left;
                    break;
                case CardBoardDirection.DownLeft:
                    yield return CardBoardDirection.UpRight;
                    yield return CardBoardDirection.Up;
                    yield return CardBoardDirection.Right;
                    break;
                case CardBoardDirection.DownRight:
                    yield return CardBoardDirection.UpLeft;
                    yield return CardBoardDirection.Up;
                    yield return CardBoardDirection.Left;
                    break;
                default:
                    var opposite = CardBoardDirectionUtility.GetOpposite(avatarToMonster);
                    if (opposite != CardBoardDirection.None)
                    {
                        yield return opposite;
                    }

                    break;
            }
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

            var field = GroundFieldGeometryHook.FieldOrNull();
            if (field == null || !field.TryGetSlotOf(victim.Uid, out var victimSlot))
            {
                Debug.LogWarning("[CardAttackBasicAdapter] 受击卡不在场地中，跳过交战。");
                return;
            }

            if (!TryResolveAttacker(field, out var attackerCard, out var attacker))
            {
                Debug.LogWarning("[CardAttackBasicAdapter] 未找到 Avatar 攻击者，跳过交战。");
                return;
            }

            var attackerSlot = attackerCard != null && field.TryGetSlotOf(attackerCard.Uid, out var aSlot)
                ? aSlot
                : ResolveAvatarSlot(field);

            EnsureRigsCollected();
            if (!TryResolveDirectionForVictimSlot(victimSlot, attackerSlot, out var direction)
                || !_rigByDirection.TryGetValue(direction, out var rig))
            {
                Debug.LogWarning($"[CardAttackBasicAdapter] 格位 {victimSlot}（攻击者在 {attackerSlot}）无可用交战 rig。", this);
                return;
            }

            var victimTransform = victim.Transform;
            victim.TryGetEffectManager(out var victimEffects);

            var attackerSnapshot = BattleFinalStateGuard.Capture(
                attackerCard,
                field,
                attackerSlot);
            var victimSnapshot = BattleFinalStateGuard.Capture(victim, field, victimSlot);

            PrepareAttackerAtSlotAnchor(field, attacker, attackerSlot, victimTransform);
            rig.ResetParticipantMotion(attacker, victimTransform);
            rig.BindParticipants(
                attacker,
                victimTransform,
                victimEffects,
                in bind,
                onLungeBegin: () => BattleCombatAudioCues.Pulse(
                    BattleCombatAudioCues.AttackPrepare,
                    "CardAttackBasicAdapter.BindLungeBegin",
                    victim.DefId),
                onCombatHit);

            await PlayBoundRigAsync(
                rig,
                attacker,
                attackerSnapshot,
                victimSnapshot,
                bind,
                cancellationToken);
        }

        /// <summary>
        /// 嘲讽重定向进攻：蓄力朝向 clickedVictim，出手瞬间脉冲 tauntVictim 并斜线冲刺命中。
        /// </summary>
        public async UniTask PlayTauntRedirectAttackAsync(
            ManagedCard clickedVictim,
            ManagedCard tauntVictim,
            BattleBindParams bind,
            Action onCombatHit,
            CancellationToken cancellationToken = default)
        {
            if (clickedVictim?.Transform == null || tauntVictim?.Transform == null)
            {
                Debug.LogWarning("[CardAttackBasicAdapter] 嘲讽重定向受击卡无效，跳过交战。");
                return;
            }

            if (!string.Equals(bind.RigFamily, BattleEncounterProfileSO.DefaultRigFamily, System.StringComparison.Ordinal))
            {
                Debug.LogWarning(
                    $"[CardAttackBasicAdapter] 暂不支持 RigFamily='{bind.RigFamily}'，回退 {BattleEncounterProfileSO.DefaultRigFamily}。",
                    this);
            }

            var field = GroundFieldGeometryHook.FieldOrNull();
            if (field == null || !field.TryGetSlotOf(clickedVictim.Uid, out var clickedSlot))
            {
                Debug.LogWarning("[CardAttackBasicAdapter] 点选受击卡不在场地中，跳过嘲讽重定向交战。");
                return;
            }

            if (!field.TryGetSlotOf(tauntVictim.Uid, out var tauntSlot))
            {
                Debug.LogWarning("[CardAttackBasicAdapter] 嘲讽受击卡不在场地中，跳过嘲讽重定向交战。");
                return;
            }

            if (!TryResolveAttacker(field, out var attackerCard, out var attacker))
            {
                Debug.LogWarning("[CardAttackBasicAdapter] 未找到 Avatar 攻击者，跳过嘲讽重定向交战。");
                return;
            }

            var attackerSlot = attackerCard != null && field.TryGetSlotOf(attackerCard.Uid, out var aSlot)
                ? aSlot
                : ResolveAvatarSlot(field);

            EnsureRigsCollected();
            if (!TryResolveDirectionForVictimSlot(clickedSlot, attackerSlot, out var direction)
                || !_rigByDirection.TryGetValue(direction, out var rig))
            {
                Debug.LogWarning($"[CardAttackBasicAdapter] 格位 {clickedSlot}（攻击者在 {attackerSlot}）无可用交战 rig（嘲讽重定向）。");
                return;
            }

            var clickedTransform = clickedVictim.Transform;
            var tauntTransform = tauntVictim.Transform;
            tauntVictim.TryGetEffectManager(out var tauntEffects);

            var attackerSnapshot = BattleFinalStateGuard.Capture(
                attackerCard,
                field,
                attackerSlot);
            var victimSnapshot = BattleFinalStateGuard.Capture(tauntVictim, field, tauntSlot);

            PrepareAttackerAtSlotAnchor(field, attacker, attackerSlot, clickedTransform);
            rig.ResetParticipantMotion(attacker, tauntTransform);
            rig.BindParticipantsTauntRedirect(
                attacker,
                clickedTransform,
                tauntTransform,
                tauntEffects,
                in bind,
                onLungeBegin: () =>
                {
                    BattleCombatAudioCues.Pulse(
                        BattleCombatAudioCues.AttackPrepare,
                        "CardAttackBasicAdapter.BindLungeBegin",
                        tauntVictim.DefId);
                    tauntVictim.PlayEffectTriggerPulse();
                },
                onCombatHit);

            await PlayBoundRigAsync(
                rig,
                attacker,
                attackerSnapshot,
                victimSnapshot,
                bind,
                cancellationToken);
        }

        /// <summary>
        /// 通用效果打击 Present（ADR-0050）：场上任意打击者 → 任意受击者（含 Avatar）。
        /// 强制相对冲刺/相对击退（rig 方向仅用于挑选烘焙时间线），命中帧回调 <paramref name="onStrikeHit"/>
        /// 与受击闪白同帧；不绑死亡回调——受击者退场由移除/击杀呈现另行接手。
        /// </summary>
        /// <summary>
        /// 效果打击 rig 播放；返回是否真的播出（false = 参与者/场地/rig 缺失被跳过，
        /// 调用方须降级为普通冲刷，不得当作已表演）。
        /// </summary>
        public async UniTask<bool> PlayEffectStrikeAsync(
            ManagedCard striker,
            ManagedCard victim,
            BattleBindParams bind,
            Action onStrikeHit,
            CancellationToken cancellationToken = default)
        {
            if (striker?.Transform == null || victim?.Transform == null)
            {
                Debug.LogWarning("[CardAttackBasicAdapter] 效果打击参与者无效，跳过。");
                return false;
            }

            var field = GroundFieldGeometryHook.FieldOrNull();
            if (field == null || !field.TryGetSlotOf(striker.Uid, out var strikerSlot))
            {
                Debug.LogWarning("[CardAttackBasicAdapter] 效果打击者不在场地中，跳过。");
                return false;
            }

            EnsureRigsCollected();
            var victimSlot = 0;
            field.TryGetSlotOf(victim.Uid, out victimSlot);
            if (!TryResolveEffectStrikeRig(strikerSlot, victimSlot, out var rig))
            {
                Debug.LogWarning("[CardAttackBasicAdapter] 无可用效果打击 rig，跳过。", this);
                return false;
            }

            // 任意几何对：必须相对冲刺 + 相对击退，否则会播错烘焙轴向。
            if (!bind.UseRelativeAttackerMotion || !bind.UseRelativeVictimKnockback)
            {
                bind = WithForcedRelativeMotion(in bind);
            }

            var strikerTransform = striker.Transform;
            var victimTransform = victim.Transform;
            victim.TryGetEffectManager(out var victimEffects);

            var strikerSnapshot = BattleFinalStateGuard.Capture(striker, field, strikerSlot);
            var victimSnapshot = BattleFinalStateGuard.Capture(victim, field);

            PrepareAttackerAtSlotAnchor(field, strikerTransform, strikerSlot, victimTransform);
            rig.ResetParticipantMotion(strikerTransform, victimTransform);
            rig.BindParticipants(
                strikerTransform,
                victimTransform,
                victimEffects,
                in bind,
                onLungeBegin: () => BattleCombatAudioCues.Pulse(
                    BattleCombatAudioCues.AttackPrepare,
                    "CardAttackBasicAdapter.EffectStrikeLungeBegin",
                    striker.DefId),
                onStrikeHit);

            await PlayBoundRigAsync(
                rig,
                strikerTransform,
                strikerSnapshot,
                victimSnapshot,
                bind,
                cancellationToken);
            return true;
        }

        /// <summary>
        /// 效果打击 rig 选择：优先受击者相对打击者的方向；对角/无格位回退分量正交向，再任意可用 rig。
        /// 相对冲刺模式下 rig 方向只决定烘焙时间线形状，几何由真实双方位置重绑。
        /// </summary>
        private bool TryResolveEffectStrikeRig(int strikerSlot, int victimSlot, out CardAttackBasicDirectionRig rig)
        {
            rig = null;
            var direction = CardBoardDirection.None;
            if (GroundSlotTopology.IsValidSlot(strikerSlot) && GroundSlotTopology.IsValidSlot(victimSlot))
            {
                direction = CardBoardDirectionUtility.ComputeSelfDirection(victimSlot, strikerSlot);
            }

            if (direction != CardBoardDirection.None
                && _rigByDirection.TryGetValue(direction, out rig))
            {
                return true;
            }

            if (direction != CardBoardDirection.None
                && TryPickFallbackCounterRig(CardBoardDirectionUtility.GetOpposite(direction), out var fallback)
                && _rigByDirection.TryGetValue(fallback, out rig))
            {
                return true;
            }

            foreach (var pair in _rigByDirection)
            {
                rig = pair.Value;
                return true;
            }

            return false;
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

            var field = GroundFieldGeometryHook.FieldOrNull();
            if (field == null || !field.TryGetSlotOf(attacker.Uid, out var attackerSlot))
            {
                Debug.LogWarning("[CardAttackBasicAdapter] 反击攻击者不在场地中，跳过交战。");
                return;
            }

            var avatarSlot = ResolveAvatarSlot(field);
            EnsureRigsCollected();
            if (!TryResolveCounterAttackDirectionForAttackerSlot(attackerSlot, avatarSlot, out var direction)
                || !_rigByDirection.TryGetValue(direction, out var rig))
            {
                Debug.LogWarning($"[CardAttackBasicAdapter] 格位 {attackerSlot}（Avatar 在 {avatarSlot}）无可用反击 rig。", this);
                return;
            }

            ManagedCard victim = null;
            if (field != null)
            {
                if (field.TryGetCardAt(avatarSlot, out var found)
                    && found != null
                    && found.CoreKind == CardPresentationKind.Avatar
                    && found.Transform != null)
                {
                    victim = found;
                }
                else
                {
                    for (var s = GroundSlotTopology.MinSlot; s <= GroundSlotTopology.MaxSlot; s++)
                    {
                        if (field.TryGetCardAt(s, out var candidate)
                            && candidate != null
                            && candidate.CoreKind == CardPresentationKind.Avatar
                            && candidate.Transform != null)
                        {
                            victim = candidate;
                            avatarSlot = s;
                            break;
                        }
                    }
                }
            }

            if (victim == null || victim.Transform == null)
            {
                Debug.LogWarning("[CardAttackBasicAdapter] 未找到 Avatar 受击者，跳过反击。");
                return;
            }

            // 对角无专用 Timeline 时回退四向 rig；必须相对冲刺，否则会播错烘焙轴向。
            if (GroundSlotTopology.AreDiagonal(attackerSlot, avatarSlot)
                && (!bind.UseRelativeAttackerMotion || !bind.UseRelativeVictimKnockback))
            {
                bind = WithForcedRelativeMotion(in bind);
            }

            var attackerTransform = attacker.Transform;
            var victimTransform = victim.Transform;
            victim.TryGetEffectManager(out var victimEffects);

            var attackerSnapshot = BattleFinalStateGuard.Capture(attacker, field, attackerSlot);
            var victimSnapshot = BattleFinalStateGuard.Capture(
                victim,
                field,
                avatarSlot);

            PrepareAttackerAtSlotAnchor(field, attackerTransform, attackerSlot, victimTransform);
            rig.ResetParticipantMotion(attackerTransform, victimTransform);
            rig.BindParticipants(
                attackerTransform,
                victimTransform,
                victimEffects,
                in bind,
                onLungeBegin: () => BattleCombatAudioCues.Pulse(
                    BattleCombatAudioCues.AttackPrepare,
                    "CardAttackBasicAdapter.BindLungeBegin",
                    attacker.DefId),
                onCombatHit);

            await PlayBoundRigAsync(
                rig,
                attackerTransform,
                attackerSnapshot,
                victimSnapshot,
                bind,
                cancellationToken);
        }

        private static BattleBindParams WithForcedRelativeMotion(in BattleBindParams bind)
        {
            return new BattleBindParams(
                bind.Intent,
                bind.ProfileId,
                bind.RigFamily,
                useRelativeAttackerMotion: true,
                useRelativeVictimKnockback: true,
                bind.VictimKnockbackCoefficient,
                bind.BindDeathCallback,
                bind.HitFlashTimingPolicy,
                bind.HitFlashCallbackDelay,
                bind.DeathCallbackDelay,
                bind.RestoreAttackerToSlot,
                bind.RestoreVictimToSlot,
                bind.RequireFinalStateGuard);
        }

        private async UniTask PlayBoundRigAsync(
            CardAttackBasicDirectionRig rig,
            Transform attacker,
            BattleFinalStateGuard.ParticipantSnapshot attackerSnapshot,
            BattleFinalStateGuard.ParticipantSnapshot victimSnapshot,
            BattleBindParams bind,
            CancellationToken cancellationToken)
        {
            var field = GroundFieldGeometryHook.FieldOrNull();
            var victimTransform = victimSnapshot.Transform;
            var sortBoost = BeginAttackerSortBoost(attackerSnapshot, victimSnapshot);
            // 蓄力/预备尚未成立：可取消排期；打断时由本出口显式撤销，不订阅实体生命周期。
            var chargeKey = TryScheduleAttackCharge(attackerSnapshot.Card);
            try
            {
                await rig.PlayAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                SkillEffectTrapRelicAudioCues.CancelScheduled(chargeKey);
                throw;
            }
            finally
            {
                EndAttackerSortBoost(sortBoost);

                var victimKilled = bind.IsLethal && rig.LastHitConfirmedKill;
                rig.ResetParticipantMotion(attacker, victimKilled ? null : victimTransform);

                var restoreVictim = bind.RestoreVictimToSlot;
                if (bind.IsLethal && !rig.LastHitConfirmedKill)
                {
                    restoreVictim = true;
                    StopSpuriousDeathEffect(victimSnapshot.Card);
                }

                if (ShouldKeepVictimOffField(victimSnapshot.Card))
                {
                    restoreVictim = false;
                }

                await BattleFinalStateGuard.RestorePairAsync(
                    attackerSnapshot,
                    victimSnapshot,
                    field,
                    bind,
                    CancellationToken.None,
                    restoreVictimOverride: restoreVictim);
            }
        }

        private static NineGrid.Presentation.Systems.AudioScheduleKey TryScheduleAttackCharge(ManagedCard attacker)
        {
            try
            {
                return SkillEffectTrapRelicAudioCues.ScheduleAttackCharge(
                    attacker != null ? attacker.DefId : string.Empty);
            }
            catch (Exception)
            {
                return default;
            }
        }

        private static void StopSpuriousDeathEffect(ManagedCard victim)
        {
            if (victim == null)
            {
                return;
            }

            if (victim.TryGetEffectManager(out var effectManager))
            {
                effectManager.StopCurrent();
            }

            CardEntityLifecycleHook.CardsOrNull()?.RefreshDisplayMode(victim);
        }

        /// <summary>
        /// 命中帧已写 Core 并 Sync 后，0 血/已标死者不得被终态守卫拉回格锚（含反击非 Lethal Profile）。
        /// </summary>
        private static bool ShouldKeepVictimOffField(ManagedCard victim)
        {
            if (victim == null)
            {
                return false;
            }

            if (victim.IsFieldDead)
            {
                return true;
            }

            return victim.View != null && victim.View.Health <= 0;
        }

        private readonly struct AttackerSortBoost
        {
            public AttackerSortBoost(SortingGroup group, int originalOrder)
            {
                Group = group;
                OriginalOrder = originalOrder;
            }

            public SortingGroup Group { get; }

            public int OriginalOrder { get; }
        }

        /// <summary>
        /// 交战期间攻击方必须压在受击方之上渲染。
        /// Avatar 常驻高一层（-9 vs -10），怪物反击冲脸时若不抬层会整张被 Avatar 卡挡住 → 「隐身打人」。
        /// </summary>
        private static AttackerSortBoost BeginAttackerSortBoost(
            in BattleFinalStateGuard.ParticipantSnapshot attackerSnapshot,
            in BattleFinalStateGuard.ParticipantSnapshot victimSnapshot)
        {
            var attackerView = attackerSnapshot.Card?.View;
            var victimView = victimSnapshot.Card?.View;
            if (attackerView == null || victimView == null)
            {
                return default;
            }

            var attackerGroup = attackerView.GetComponent<SortingGroup>();
            var victimGroup = victimView.GetComponent<SortingGroup>();
            if (attackerGroup == null || victimGroup == null
                || attackerGroup.sortingOrder > victimGroup.sortingOrder)
            {
                return default;
            }

            var originalOrder = attackerGroup.sortingOrder;
            attackerGroup.sortingOrder = victimGroup.sortingOrder + 1;
            CardPresentationProbe.VisChange(
                attackerSnapshot.Card.Uid,
                "Combat.SortBoost",
                sortOrder: attackerGroup.sortingOrder);
            return new AttackerSortBoost(attackerGroup, originalOrder);
        }

        private static void EndAttackerSortBoost(in AttackerSortBoost boost)
        {
            if (boost.Group == null)
            {
                return;
            }

            boost.Group.sortingOrder = boost.OriginalOrder;
        }

        /// <summary>
        /// 优先取场地当前 Avatar 占格的 Avatar 卡；否则用 Inspector defaultAttacker / AttackerProxy 兜底。
        /// </summary>
        private bool TryResolveAttacker(
            GroundFieldView field,
            out ManagedCard attackerCard,
            out Transform attacker)
        {
            attackerCard = null;
            attacker = null;

            var avatarSlot = ResolveAvatarSlot(field);
            if (field != null
                && field.TryGetCardAt(avatarSlot, out attackerCard)
                && attackerCard != null
                && attackerCard.CoreKind == CardPresentationKind.Avatar
                && attackerCard.Transform != null)
            {
                attacker = attackerCard.Transform;
                return true;
            }

            if (field != null)
            {
                for (var s = GroundSlotTopology.MinSlot; s <= GroundSlotTopology.MaxSlot; s++)
                {
                    if (field.TryGetCardAt(s, out var candidate)
                        && candidate != null
                        && candidate.CoreKind == CardPresentationKind.Avatar
                        && candidate.Transform != null)
                    {
                        attackerCard = candidate;
                        attacker = candidate.Transform;
                        return true;
                    }
                }
            }

            if (defaultAttacker != null)
            {
                attacker = defaultAttacker;
                return true;
            }

            EnsureAttackerProxy();
            if (_attackerProxy != null && field != null)
            {
                var anchor = field.GetGroundAnchor(avatarSlot);
                if (anchor != null)
                {
                    _attackerProxy.position = anchor.position;
                }
            }

            attacker = _attackerProxy;
            return attacker != null;
        }

        private static void PrepareAttackerAtAvatarAnchor(
            GroundFieldView field,
            Transform attacker,
            Transform victim)
        {
            var avatarSlot = ResolveAvatarSlot(field);
            PrepareAttackerAtSlotAnchor(
                field,
                attacker,
                avatarSlot,
                victim);
        }

        private static void PrepareAttackerAtSlotAnchor(
            GroundFieldView field,
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
