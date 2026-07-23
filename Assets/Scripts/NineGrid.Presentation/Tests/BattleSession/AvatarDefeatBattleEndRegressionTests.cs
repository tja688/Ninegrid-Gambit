using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
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
    }
}
