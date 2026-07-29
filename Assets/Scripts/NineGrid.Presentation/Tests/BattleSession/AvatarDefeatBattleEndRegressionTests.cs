using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Systems;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests.BattleSession
{
    /// <summary>
    /// 回归：Core 已 Defeat 时表现侧必须 RaiseBattleEnded，否则留场可点（IntentIntake notLegal phase=Defeat）。
    /// </summary>
    public sealed class AvatarDefeatBattleEndRegressionTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);

        [Test]
        public void QueuedBoardPresent_EmptyDeltaButAvatarDefeated_StillInvokesDrain()
        {
            var drained = false;
            PostKillBoardPresentationResult seen = default;
            var channel = new QueuedBoardPresentChannel(
                (result, _) =>
                {
                    drained = true;
                    seen = result;
                    return UniTask.CompletedTask;
                });

            channel.Enqueue(new PostKillBoardPresentationResult
            {
                Accepted = true,
                AvatarDefeated = true,
            });
            channel.Begin(batchId: 7);

            Assert.IsTrue(
                drained,
                "AvatarDefeated 即使无盘面 delta 也必须走 drain，否则无法 RaiseBattleEnded");
            Assert.IsTrue(seen.AvatarDefeated);
            Assert.IsTrue(channel.IsComplete);
        }

        [Test]
        public void CombatCounterPresent_AvatarDefeatedEvenIfNotAccepted_StillInvokesPlay()
        {
            var played = false;
            PostKillBoardPresentationResult seen = default;
            var channel = new CombatCounterPresentChannel(
                (slot, uid, result, _) =>
                {
                    played = true;
                    seen = result;
                    return UniTask.CompletedTask;
                });

            channel.Enqueue(
                attackerSlot: 2,
                attackerUid: 99,
                new PostKillBoardPresentationResult
                {
                    Accepted = false,
                    AvatarDefeated = true,
                });
            channel.Begin(batchId: 3);

            Assert.IsTrue(
                played,
                "AvatarDefeated 即使 Accepted=false 也必须走 play，否则 CompositionRoot finally 无法 EnsureBattleEnded");
            Assert.IsTrue(seen.AvatarDefeated);
            Assert.IsTrue(channel.IsComplete);
        }

        [Test]
        public void DrainPostKillBoard_AvatarDefeated_RaisesBattleSessionEnded()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            {
                var session = BattleSessionSystem.EnsureRegistered(arch.Architecture);
                var ended = new List<bool>();
                var unreg = arch.Architecture.RegisterEvent<BattleSessionEndedEvent>(
                    e => ended.Add(e.Victory));

                try
                {
                    session.DrainPostKillBoardAsync(
                        new PostKillBoardPresentationResult
                        {
                            Accepted = true,
                            AvatarDefeated = true,
                        },
                        default).GetAwaiter().GetResult();

                    Assert.AreEqual(1, ended.Count, "AvatarDefeated 盘面 Present 必须结束战斗");
                    Assert.IsFalse(ended[0], "应为战败（victory=false）");
                }
                finally
                {
                    unreg.UnRegister();
                }
            }
        }

        [Test]
        public void RaiseBattleEnded_IsIdempotent()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            {
                var session = BattleSessionSystem.EnsureRegistered(arch.Architecture);
                var ended = new List<bool>();
                var unreg = arch.Architecture.RegisterEvent<BattleSessionEndedEvent>(
                    e => ended.Add(e.Victory));
                try
                {
                    session.RaiseBattleEnded(victory: false);
                    session.RaiseBattleEnded(victory: false);
                    Assert.AreEqual(1, ended.Count);
                    Assert.IsFalse(ended[0]);
                }
                finally
                {
                    unreg.UnRegister();
                }
            }
        }

        [Test]
        public void AttackIntent_LethalCounter_RaisesBattleSessionEnded()
        {
            using (var fx = PresentationArchitectureFixture.CreateStartedGame(seed: 901UL))
            {
                var session = BattleSessionSystem.EnsureRegistered(fx.Architecture);
                Assert.IsTrue(fx.Phase.StartNode(CreateSingleMonsterNode(hp: 99, attack: 99)).Accepted);
                fx.PlaceSoleBoardCardAt(sAdjacentSlot);

                var avatar = fx.Registry.Get(fx.Board.AvatarUid.Value);
                avatar.Stats.SetBase(StatId.MaxHp, 3);
                avatar.Stats.SetBase(StatId.Hp, 3);
                avatar.Stats.SetBase(StatId.Armor, 0);
                avatar.Stats.SetBase(StatId.CurrentArmor, 0);
                avatar.Stats.SetBase(StatId.Attack, 1);

                var ended = new List<bool>();
                var unreg = fx.Architecture.RegisterEvent<BattleSessionEndedEvent>(
                    e => ended.Add(e.Victory));
                try
                {
                    var channels = CreateEnsurePresentChannels(session);
                    var factory = new AttackIntentScriptFactory(
                        fx.Architecture,
                        fx.Dispatcher,
                        channels.Hit,
                        channels.Board,
                        channels.Counter,
                        onHitBatchProjected: (start, slot, uid, result) =>
                            channels.HitTyped.Enqueue(slot, uid, result),
                        onBoardBatchProjected: (start, slot, result) =>
                            channels.Board.Enqueue(result),
                        onCounterBatchProjected: (start, slot, attackerUid, result) =>
                            channels.CounterTyped.Enqueue(slot, attackerUid, result));
                    var director = new PresentationDirector(factory);

                    bool preview;
                    Assert.IsTrue(director.TrySubmitIntent(
                        new InputIntent(InputIntentKinds.Attack, sAdjacentSlot.Index),
                        out preview));

                    PumpUntilIdle(director, maxTicks: 120);

                    Assert.AreEqual(GamePhase.Defeat, fx.Phase.CurrentPhase);
                    Assert.IsFalse(director.IsMainlineBusy, "战败后主线不得忙死");
                    Assert.AreEqual(1, ended.Count, "交战回击致死必须 RaiseBattleEnded");
                    Assert.IsFalse(ended[0]);
                }
                finally
                {
                    unreg.UnRegister();
                }
            }
        }

        [Test]
        public void AttackIntent_LethalEnemyActionStrike_RaisesBattleSessionEnded()
        {
            using (var fx = PresentationArchitectureFixture.CreateStartedGame(seed: 902UL))
            {
                var session = BattleSessionSystem.EnsureRegistered(fx.Architecture);
                Assert.IsTrue(fx.Phase.StartNode(CreateEmptyEnemyNode()).Accepted);

                // 回击 3 点后剩 2 血，敌方行动再打 3 点致死（与交战回击分拍）。
                var avatar = fx.Registry.Get(fx.Board.AvatarUid.Value);
                avatar.Stats.SetBase(StatId.MaxHp, 5);
                avatar.Stats.SetBase(StatId.Hp, 5);
                avatar.Stats.SetBase(StatId.Armor, 0);
                avatar.Stats.SetBase(StatId.CurrentArmor, 0);
                avatar.Stats.SetBase(StatId.Attack, 0);

                SpawnOrthogonalMelee(fx, sAdjacentSlot, hp: 99, attack: 3, countdown: 1);
                var second = SpawnOrthogonalMelee(
                    fx,
                    SlotId.Board(4),
                    hp: 99,
                    attack: 3,
                    countdown: 1);
                Assert.Greater(second, 0);

                var ended = new List<bool>();
                var logAtEnd = 0;
                var unreg = fx.Architecture.RegisterEvent<BattleSessionEndedEvent>(
                    e =>
                    {
                        ended.Add(e.Victory);
                        logAtEnd = fx.Pipeline.EventLog.Entries.Count;
                    });
                try
                {
                    var channels = CreateEnsurePresentChannels(session);
                    var factory = new AttackIntentScriptFactory(
                        fx.Architecture,
                        fx.Dispatcher,
                        channels.Hit,
                        channels.Board,
                        channels.Counter,
                        onHitBatchProjected: (start, slot, uid, result) =>
                            channels.HitTyped.Enqueue(slot, uid, result),
                        onBoardBatchProjected: (start, slot, result) =>
                            channels.Board.Enqueue(result),
                        onCounterBatchProjected: (start, slot, attackerUid, result) =>
                            channels.CounterTyped.Enqueue(slot, attackerUid, result));
                    var director = new PresentationDirector(factory);

                    bool preview;
                    Assert.IsTrue(director.TrySubmitIntent(
                        new InputIntent(InputIntentKinds.Attack, sAdjacentSlot.Index),
                        out preview));

                    PumpUntilIdle(director, maxTicks: 200);

                    Assert.AreEqual(GamePhase.Defeat, fx.Phase.CurrentPhase);
                    Assert.IsFalse(director.IsMainlineBusy, "战败后主线不得忙死");
                    Assert.AreEqual(1, ended.Count, "敌方行动致死必须 RaiseBattleEnded");
                    Assert.IsFalse(ended[0]);
                    Assert.AreEqual(
                        0,
                        CountDamageSince(fx.Pipeline, logAtEnd),
                        "Raise 之后名单剩余不得再造成 DamageDealt");
                }
                finally
                {
                    unreg.UnRegister();
                }
            }
        }

        private static EnsureChannels CreateEnsurePresentChannels(IBattleSessionSystem session)
        {
            var hit = new CombatAttackPresentChannel(
                (slot, uid, result, token) =>
                {
                    session.EnsureBattleEndedIfAvatarDefeated(result, token);
                    return UniTask.CompletedTask;
                },
                () => CancellationToken.None);
            var counter = new CombatCounterPresentChannel(
                (slot, uid, result, token) =>
                {
                    session.EnsureBattleEndedIfAvatarDefeated(result, token);
                    return UniTask.CompletedTask;
                },
                () => CancellationToken.None);
            var board = new QueuedBoardPresentChannel(
                (result, token) => session.DrainPostKillBoardAsync(result, token),
                () => CancellationToken.None);
            return new EnsureChannels
            {
                Hit = hit,
                HitTyped = hit,
                Counter = counter,
                CounterTyped = counter,
                Board = board,
            };
        }

        private static void PumpUntilIdle(PresentationDirector director, int maxTicks)
        {
            var guard = 0;
            while (director.IsMainlineBusy && guard++ < maxTicks)
            {
                director.Tick(0.016f);
            }
        }

        private static int CountDamageSince(IActionPipelineSystem pipeline, int startIndex)
        {
            var count = 0;
            var entries = pipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                if (entries[i].Type == CoreEventType.DamageDealt && entries[i].Amount > 0)
                {
                    count++;
                }
            }

            return count;
        }

        private static int SpawnOrthogonalMelee(
            PresentationArchitectureFixture fx,
            SlotId slot,
            int hp,
            int attack,
            int countdown)
        {
            var draft = new CardDraft("monster.test.volley", CardKind.Monster)
            {
                MaxHp = hp,
                Attack = attack,
                AttackPattern = AttackPattern.OrthogonalMelee,
                ActionFrequency = 3
            };
            var card = draft.Create(fx.Registry);
            card.Counters.Set(CoreCounterKeys.AttackPatternCountdown, countdown);
            fx.Board.PlaceCard(card, slot);
            return card.Uid;
        }

        private static NodeDeckOptions CreateSingleMonsterNode(int hp, int attack)
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.test", CardKind.Monster) { MaxHp = hp, Attack = attack });
        }

        private static NodeDeckOptions CreateEmptyEnemyNode()
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            };
        }

        private sealed class EnsureChannels
        {
            public IPresentChannel Hit;
            public CombatAttackPresentChannel HitTyped;
            public IPresentChannel Counter;
            public CombatCounterPresentChannel CounterTyped;
            public QueuedBoardPresentChannel Board;
        }
    }
}
