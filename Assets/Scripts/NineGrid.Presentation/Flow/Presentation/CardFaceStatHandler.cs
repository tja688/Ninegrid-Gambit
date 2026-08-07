using System.Collections.Generic;
using NineGrid.Cards;
using NineGrid.Cards.Presentation;
using NineGrid.Core;
using NineGrid.Flow.Diagnostics;
using UnityEngine;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 卡面数值处理器：只从结算指令绝对值赋值，不回头读 CardRegistry / IStatSystem。
    /// </summary>
    public sealed class CardFaceStatHandler : IBattleBeatHandler
    {
        public bool TryApply(PresentationInstruction instruction)
        {
            if (instruction == null || instruction.Event == null)
            {
                return false;
            }

            switch (instruction.Kind)
            {
                case PresentationInstructionKind.UpdateHp:
                    ApplyHp(instruction.Event);
                    return true;
                case PresentationInstructionKind.UpdateArmor:
                    ApplyArmor(instruction.Event);
                    return true;
                case PresentationInstructionKind.UpdateActionCount:
                    ApplyActionCount(instruction.Event);
                    return true;
                case PresentationInstructionKind.UpdateCountdownRemaining:
                    ApplyCountdownRemaining(instruction.Event);
                    return true;
                case PresentationInstructionKind.ClearCountdownRemaining:
                    ApplyCountdownCleared(instruction.Event);
                    return true;
                case PresentationInstructionKind.ModifyBaseStat:
                    ApplyBaseStat(instruction.Event);
                    return true;
                case PresentationInstructionKind.KillCard:
                    ApplyKill(instruction.Event);
                    return true;
                case PresentationInstructionKind.SpawnCard:
                case PresentationInstructionKind.DealCard:
                case PresentationInstructionKind.ShowAvatar:
                    ApplySpawnFace(instruction.Event);
                    return true;
                case PresentationInstructionKind.OfferReward:
                    ApplyOfferReward(instruction.Event);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>测试与开局引导用：等价于 <see cref="TryApply"/> 且忽略返回值。</summary>
        public void Apply(PresentationInstruction instruction)
        {
            TryApply(instruction);
        }

        private static void ApplyHp(CoreGameEvent gameEvent)
        {
            if (!TryResolveCard(gameEvent, out var card))
            {
                return;
            }

            CommitNumeric(card, attack: null, armor: null, hp: Mathf.Max(0, gameEvent.RemainingHp), actionCount: null);
        }

        private static void ApplyArmor(CoreGameEvent gameEvent)
        {
            if (!TryResolveCard(gameEvent, out var card))
            {
                return;
            }

            CommitNumeric(card, attack: null, armor: Mathf.Max(0, gameEvent.RemainingArmor), hp: null, actionCount: null);
        }

        private static void ApplyActionCount(CoreGameEvent gameEvent)
        {
            if (!TryResolveCard(gameEvent, out var card))
            {
                return;
            }

            CommitNumeric(
                card,
                attack: null,
                armor: null,
                hp: null,
                actionCount: Mathf.Max(0, gameEvent.ResultValue));
        }

        /// <summary>
        /// 效果倒计时剩余提交（ADR-0035）：事件 Message 为完整「装配id.键」投影令牌键，
        /// ResultValue 为剩余次数；写入卡面已提交剩余并重投影局内描述（Instance 模式）。
        /// 剩余只经本 Settled 指令到达，View 不直读 Core 计数器。
        /// </summary>
        private static void ApplyCountdownRemaining(CoreGameEvent gameEvent)
        {
            if (gameEvent == null || string.IsNullOrEmpty(gameEvent.Message))
            {
                return;
            }

            if (!TryResolveCard(gameEvent, out var card))
            {
                return;
            }

            CoreCardPresentationMapper.CommitCountdownRemaining(
                card,
                gameEvent.Message,
                Mathf.Max(0, gameEvent.ResultValue).ToString());
        }

        /// <summary>
        /// 效果倒计时投影清除（ADR-0035 / #157）：效果卸载/离战重置后移除已提交剩余键并重投影，
        /// 回退静态/初始（Instance 模式）。剩余只经本 Settled 指令到达，View 不直读 Core 计数器。
        /// </summary>
        private static void ApplyCountdownCleared(CoreGameEvent gameEvent)
        {
            if (gameEvent == null || string.IsNullOrEmpty(gameEvent.Message))
            {
                return;
            }

            if (!TryResolveCard(gameEvent, out var card))
            {
                return;
            }

            CoreCardPresentationMapper.ClearCountdownRemaining(card, gameEvent.Message);
        }

        private static void ApplyBaseStat(CoreGameEvent gameEvent)
        {
            if (!TryResolveCard(gameEvent, out var card))
            {
                Debug.LogWarning(
                    "[CardFaceStatHandler] ModifyBaseStat 找不到卡面 uid="
                    + (gameEvent != null
                        ? (gameEvent.CardUid > 0 ? gameEvent.CardUid : gameEvent.TargetUid)
                        : 0)
                    + " reason=" + (gameEvent != null ? gameEvent.Message : string.Empty)
                    + " source=" + (gameEvent != null ? gameEvent.SourceDefId : string.Empty));
                return;
            }

            var stat = (StatId)gameEvent.Amount;
            var value = Mathf.Max(0, gameEvent.ResultValue);
            switch (stat)
            {
                case StatId.Attack:
                    CommitNumeric(card, attack: value, armor: null, hp: null, actionCount: null);
                    RecordCardFaceBaseStatCommit(card, gameEvent, stat, value);
                    break;
                case StatId.Armor:
                case StatId.CurrentArmor:
                    CommitNumeric(card, attack: null, armor: value, hp: null, actionCount: null);
                    break;
                case StatId.Hp:
                    CommitNumeric(card, attack: null, armor: null, hp: value, actionCount: null);
                    break;
                case StatId.MaxHp:
                    // ResultValue 是新上限；卡面血量槽显示当前血，取 RemainingHp（耦合后绝对值）。
                    CommitNumeric(
                        card,
                        attack: null,
                        armor: null,
                        hp: Mathf.Max(0, gameEvent.RemainingHp),
                        actionCount: null);
                    break;
            }
        }

        private static void RecordCardFaceBaseStatCommit(
            ManagedCard card,
            CoreGameEvent gameEvent,
            StatId stat,
            int value)
        {
            if (!FlowTraceRecorder.Enabled || gameEvent == null)
            {
                return;
            }

            try
            {
                FlowTraceRecorder.BeginSessionIfNeeded();
                FlowTraceRecorder.Record(
                    FlowTraceCategory.Presentation,
                    FlowTraceNames.CardFaceBaseStatCommit,
                    new Dictionary<string, string>
                    {
                        { "uid", card.Uid.ToString() },
                        { "defId", card.DefId ?? string.Empty },
                        { "stat", stat.ToString() },
                        { "resultValue", value.ToString() },
                        { "delta", gameEvent.Delta.ToString() },
                        { "reason", gameEvent.Message ?? string.Empty },
                        { "sourceDefId", gameEvent.SourceDefId ?? string.Empty },
                        { "hasView", card.View != null ? "1" : "0" },
                    },
                    accepted: true,
                    refBattleOpIndex: BattleTraceRecorder.LastOpIndex);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[CardFaceStatHandler] CardFaceBaseStatCommit: " + ex.Message);
            }
        }

        private static void ApplyKill(CoreGameEvent gameEvent)
        {
            if (!TryResolveCard(gameEvent, out var card))
            {
                return;
            }

            // CardKilled：可见血量取指令携带的剩余血量（通常为 0）；禁止本地硬编码置零旁路。
            CommitNumeric(card, attack: null, armor: null, hp: Mathf.Max(0, gameEvent.RemainingHp), actionCount: null);
        }

        private static void ApplySpawnFace(CoreGameEvent gameEvent)
        {
            if (!TryResolveCard(gameEvent, out var card))
            {
                return;
            }

            // 生成/发牌/亮相：攻=ResultValue，血/甲=Remaining*，与后续增量同一 Commit 出口。
            CommitNumeric(
                card,
                attack: Mathf.Max(0, gameEvent.ResultValue),
                armor: Mathf.Max(0, gameEvent.RemainingArmor),
                hp: Mathf.Max(0, gameEvent.RemainingHp),
                actionCount: null);
        }

        /// <summary>
        /// Bounce 候选项：按 DefId 匹配纯表现卡（负 uid / RemovedMode），提交指令投影绝对值。
        /// 尚无 Bounce 卡时仍 return true（认领指令），待 spawn 后 PresentStandalone 二次提交。
        /// </summary>
        private static void ApplyOfferReward(CoreGameEvent gameEvent)
        {
            if (gameEvent == null
                || !RewardOfferFaceEncoding.TryParse(gameEvent.Message, out _, out var faces))
            {
                return;
            }

            var cards = CardEntityLifecycleHook.CardsOrNull();
            if (cards == null)
            {
                return;
            }

            var claimedUids = new System.Collections.Generic.HashSet<int>();
            for (var i = 0; i < faces.Count; i++)
            {
                var face = faces[i];
                if (face == null || string.IsNullOrEmpty(face.DefId))
                {
                    continue;
                }

                if (!TryFindBounceOptionCard(cards, face.DefId, claimedUids, out var card))
                {
                    continue;
                }

                claimedUids.Add(card.Uid);
                CommitNumeric(
                    card,
                    attack: Mathf.Max(0, face.Attack),
                    armor: Mathf.Max(0, face.Armor),
                    hp: Mathf.Max(0, face.Hp),
                    actionCount: null);
            }
        }

        private static bool TryFindBounceOptionCard(
            CardManagerSingleton cards,
            string defId,
            System.Collections.Generic.HashSet<int> claimedUids,
            out ManagedCard card)
        {
            card = null;
            foreach (var pair in cards.CardsByUid)
            {
                var candidate = pair.Value;
                if (candidate == null
                    || candidate.Uid >= 0
                    || candidate.DisplayMode != CardDisplayMode.RemovedMode
                    || claimedUids.Contains(candidate.Uid)
                    || !string.Equals(candidate.DefId, defId, System.StringComparison.Ordinal))
                {
                    continue;
                }

                card = candidate;
                return true;
            }

            return false;
        }

        private static bool TryResolveCard(CoreGameEvent gameEvent, out ManagedCard card)
        {
            var uid = gameEvent.CardUid > 0
                ? gameEvent.CardUid
                : gameEvent.TargetUid;
            if (uid <= 0)
            {
                card = null;
                return false;
            }

            return CardEntityLifecycleHook.TryGetCard(uid, out card) && card != null;
        }

        private static void CommitNumeric(
            ManagedCard card,
            int? attack,
            int? armor,
            int? hp,
            int? actionCount)
        {
            if (card.View == null)
            {
                return;
            }

            var previous = card.CommittedPresentation;
            var snapshot = previous != null
                ? CloneSnapshot(previous)
                : new CardPresentationSnapshot
                {
                    Kind = card.CoreKind,
                    DefId = card.DefId ?? string.Empty,
                    FaceUp = true,
                };

            if (attack.HasValue)
            {
                snapshot.Attack = attack.Value;
            }

            if (armor.HasValue)
            {
                snapshot.Armor = armor.Value;
            }

            if (hp.HasValue)
            {
                snapshot.Hp = hp.Value;
            }

            if (actionCount.HasValue)
            {
                snapshot.ActionCount = actionCount.Value;
            }

            card.CommitPresentation(snapshot);
        }

        private static CardPresentationSnapshot CloneSnapshot(CardPresentationSnapshot source)
        {
            return new CardPresentationSnapshot
            {
                Kind = source.Kind,
                DefId = source.DefId ?? string.Empty,
                DisplayName = source.DisplayName ?? string.Empty,
                MainIcon = source.MainIcon,
                FaceBackground = source.FaceBackground,
                BackBorder = source.BackBorder,
                BackShirt = source.BackShirt,
                BackLogo = source.BackLogo,
                CardFrame = source.CardFrame,
                Banner = source.Banner,
                Attack = source.Attack,
                Armor = source.Armor,
                Hp = source.Hp,
                ActionCount = source.ActionCount,
                FaceUp = source.FaceUp,
                BasicDescription = source.BasicDescription ?? string.Empty,
                DetailDescription = source.DetailDescription ?? string.Empty,
                FaceIntro = source.FaceIntro ?? string.Empty,
                FrameColor = source.FrameColor,
                CommittedCountdownRemaining = CopyCommittedRemaining(source.CommittedCountdownRemaining),
            };
        }

        private static Dictionary<string, string> CopyCommittedRemaining(
            IReadOnlyDictionary<string, string> source)
        {
            var copy = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            if (source != null)
            {
                foreach (var pair in source)
                {
                    copy[pair.Key] = pair.Value;
                }
            }

            return copy;
        }
    }
}
