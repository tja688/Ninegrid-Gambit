using System;
using NineGrid.Cards;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Controllers;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.Output
{
    /// <summary>
    /// V9：描述输出经 Hook → Event；遗留 DescriptionHoverSink 退役。
    /// </summary>
    public sealed class DescriptionOutputControllerTests
    {
        [Test]
        public void Controller_RequestShow_SendsDescriptionShowRequestedEvent()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                DescriptionShowRequested? received = null;
                var unreg = arch.Architecture.RegisterEvent<DescriptionShowRequested>(e => received = e);

                var controller = DescriptionOutputController.EnsureInstalled();
                try
                {
                    DescriptionDisplayHook.RequestShow("help.throwing_knife", DescriptionShowRoute.Hover);

                    Assert.IsTrue(received.HasValue);
                    Assert.AreEqual("help.throwing_knife", received.Value.DefId);
                    Assert.AreEqual(DescriptionShowRoute.Hover, received.Value.Route);
                }
                finally
                {
                    unreg.UnRegister();
                    UnityEngine.Object.DestroyImmediate(controller.gameObject);
                }
            }
        }

        [Test]
        public void Controller_RequestShowText_AndClear_SendMatchingEvents()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                DescriptionShowTextRequested? textEvent = null;
                DescriptionClearRequested? clearEvent = null;
                var unregs = new[]
                {
                    arch.Architecture.RegisterEvent<DescriptionShowTextRequested>(e => textEvent = e),
                    arch.Architecture.RegisterEvent<DescriptionClearRequested>(e => clearEvent = e),
                };

                var controller = DescriptionOutputController.EnsureInstalled();
                try
                {
                    DescriptionDisplayHook.RequestShowText("选一张牌", DescriptionShowRoute.BoardSelect);
                    DescriptionDisplayHook.RequestClear(DescriptionShowRoute.BoardSelect);

                    Assert.IsTrue(textEvent.HasValue);
                    Assert.AreEqual("选一张牌", textEvent.Value.Text);
                    Assert.AreEqual(DescriptionShowRoute.BoardSelect, textEvent.Value.Route);
                    Assert.IsTrue(clearEvent.HasValue);
                    Assert.AreEqual(DescriptionShowRoute.BoardSelect, clearEvent.Value.Route);
                }
                finally
                {
                    foreach (var u in unregs)
                    {
                        u.UnRegister();
                    }

                    UnityEngine.Object.DestroyImmediate(controller.gameObject);
                }
            }
        }

        [Test]
        public void DescriptionHoverSink_TypeIsRemoved()
        {
            var cardsAssembly = typeof(CombatHitSink).Assembly;
            Assert.IsNull(
                cardsAssembly.GetType("NineGrid.Cards.DescriptionHoverSink"),
                "DescriptionHoverSink 应已删除，改由 DescriptionDisplayHook + QF Event");
        }
    }
}
