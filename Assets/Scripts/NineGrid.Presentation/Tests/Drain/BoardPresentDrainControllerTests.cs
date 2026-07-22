using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.Presentation.Controllers;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.Drain
{
    /// <summary>
    /// V4：BoardPresentDrainController ↔ BoardPresentDrainHook 接线。
    /// </summary>
    public sealed class BoardPresentDrainControllerTests
    {
        [SetUp]
        public void SetUp()
        {
            BoardPresentDrainHook.Drain = null;
        }

        [TearDown]
        public void TearDown()
        {
            BoardPresentDrainHook.Drain = null;
            var existing = UnityEngine.Object.FindObjectsOfType<BoardPresentDrainController>();
            for (var i = 0; i < existing.Length; i++)
            {
                if (existing[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(existing[i].gameObject);
                }
            }
        }

        [Test]
        public void BindDrain_ExposesUnifiedHookEntry()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                var calls = 0;
                Func<PostKillBoardPresentationResult, CancellationToken, UniTask> drain =
                    (result, ct) =>
                    {
                        calls++;
                        return UniTask.CompletedTask;
                    };

                var host = new GameObject(nameof(BoardPresentDrainController) + "_Test");
                var controller = host.AddComponent<BoardPresentDrainController>();
                controller.BindDrain(drain);

                Assert.AreSame(drain, BoardPresentDrainHook.Drain);

                var awaiter = BoardPresentDrainHook.RequestDrain(default).GetAwaiter();
                Assert.IsTrue(awaiter.IsCompleted);
                Assert.AreEqual(1, calls);
            }
        }

        [Test]
        public void RequestWire_WithoutController_StillBindsDrainDirectly()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                var calls = 0;
                Func<PostKillBoardPresentationResult, CancellationToken, UniTask> drain =
                    (result, ct) =>
                    {
                        calls++;
                        return UniTask.CompletedTask;
                    };

                BoardPresentDrainHook.WireDrain = null;
                BoardPresentDrainHook.RequestWire(drain);
                Assert.AreSame(drain, BoardPresentDrainHook.Drain);

                var awaiter = BoardPresentDrainHook.RequestDrain(default).GetAwaiter();
                Assert.IsTrue(awaiter.IsCompleted);
                Assert.AreEqual(1, calls);
            }
        }
    }
}
