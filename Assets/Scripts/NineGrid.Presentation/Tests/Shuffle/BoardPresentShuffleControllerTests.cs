using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.Presentation.Controllers;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.Shuffle
{
    /// <summary>
    /// V5：BoardPresentShuffleController ↔ BoardPresentShuffleHook 接线。
    /// </summary>
    public sealed class BoardPresentShuffleControllerTests
    {
        [SetUp]
        public void SetUp()
        {
            BoardPresentShuffleHook.Flush = null;
        }

        [TearDown]
        public void TearDown()
        {
            BoardPresentShuffleHook.Flush = null;
            var existing = UnityEngine.Object.FindObjectsOfType<BoardPresentShuffleController>();
            for (var i = 0; i < existing.Length; i++)
            {
                if (existing[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(existing[i].gameObject);
                }
            }
        }

        [Test]
        public void BindFlush_ExposesUnifiedHookEntry()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                var calls = 0;
                Func<CancellationToken, UniTask> flush =
                    ct =>
                    {
                        calls++;
                        return UniTask.CompletedTask;
                    };

                var host = new GameObject(nameof(BoardPresentShuffleController) + "_Test");
                var controller = host.AddComponent<BoardPresentShuffleController>();
                controller.BindFlush(flush);

                Assert.AreSame(flush, BoardPresentShuffleHook.Flush);

                var awaiter = BoardPresentShuffleHook.RequestFlush().GetAwaiter();
                Assert.IsTrue(awaiter.IsCompleted);
                Assert.AreEqual(1, calls);
            }
        }

        [Test]
        public void RequestWire_WithoutController_StillBindsFlushDirectly()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                var calls = 0;
                Func<CancellationToken, UniTask> flush =
                    ct =>
                    {
                        calls++;
                        return UniTask.CompletedTask;
                    };

                BoardPresentShuffleHook.WireFlush = null;
                BoardPresentShuffleHook.RequestWire(flush);
                Assert.AreSame(flush, BoardPresentShuffleHook.Flush);

                var awaiter = BoardPresentShuffleHook.RequestFlush().GetAwaiter();
                Assert.IsTrue(awaiter.IsCompleted);
                Assert.AreEqual(1, calls);
            }
        }
    }
}
