using System.Collections.Generic;
using NUnit.Framework;
using NineGrid.Flow;
using UnityEngine;

namespace NineGrid.Cards.Tests
{
    /// <summary>
    /// Bounce 选项在卡面重置后 UID 被复用时，延迟 Teardown 不得误删新节点视图。
    /// </summary>
    public sealed class BounceCrossNodeStaleReleaseRegressionTests
    {
        private GameObject _cardManagerRoot;
        private GameObject _selectorRoot;
        private GameObject _prefab;
        private CardManagerSingleton _cardManager;
        private SelectorManagerSingleton _selector;
        private BounceFanChoicePresenter _bouncePresenter;

        [SetUp]
        public void SetUp()
        {
            _cardManagerRoot = new GameObject("BounceRegressionCardManager");
            _cardManager = _cardManagerRoot.AddComponent<CardManagerSingleton>();

            _prefab = new GameObject("StandardCardPrefab");
            _prefab.AddComponent<StandardCardView>();
            _cardManager.RegisterPrefab(CardManagerSingleton.StandardDefId, _prefab);

            _selectorRoot = new GameObject("BounceRegressionSelector");
            _selector = _selectorRoot.AddComponent<SelectorManagerSingleton>();
            _bouncePresenter = _selectorRoot.AddComponent<BounceFanChoicePresenter>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_selectorRoot != null)
            {
                Object.DestroyImmediate(_selectorRoot);
            }

            if (_cardManagerRoot != null)
            {
                Object.DestroyImmediate(_cardManagerRoot);
            }

            if (_prefab != null)
            {
                Object.DestroyImmediate(_prefab);
            }
        }

        [Test]
        public void Teardown_AfterSurfaceResetAndUidReuse_DoesNotDestroyReplacementViews()
        {
            var options = new List<string> { "help.test_a", "help.test_b", "help.test_c" };
            _selector.BeginBounceChoice(options, (_, _) => { });

            var reusedUids = new List<int>(_cardManager.CardsByUid.Keys);
            Assert.GreaterOrEqual(reusedUids.Count, 3, "Bounce 应生成临时选项视图。");

            // 模拟下一节点 ResetCardPresentationSurface：未 HideChoice 时旧 _entries 仍持有陈旧句柄。
            _cardManager.ReleaseAll("Presentation.ResetCardSurface");

            var replacements = new List<ManagedCard>(reusedUids.Count);
            for (var i = 0; i < reusedUids.Count; i++)
            {
                var uid = reusedUids[i];
                var card = _cardManager.SpawnView(uid, CardManagerSingleton.StandardDefId);
                Assert.IsNotNull(card);
                replacements.Add(card);
            }

            // 延迟退场回调最终仍会 Teardown；陈旧句柄须被实例校验拦住。
            _bouncePresenter.Teardown();

            for (var i = 0; i < replacements.Count; i++)
            {
                var replacement = replacements[i];
                Assert.IsTrue(
                    _cardManager.TryGet(replacement.Uid, out var stillRegistered),
                    $"uid={replacement.Uid} 的新视图不应被 Bounce 陈旧句柄误删。");
                Assert.AreSame(replacement, stillRegistered);
                Assert.IsNotNull(replacement.View);
            }
        }

        [Test]
        public void HideChoice_BeforeSurfaceReset_PreventsStaleEntryRelease()
        {
            var options = new List<string> { "help.test_a", "help.test_b" };
            _selector.BeginBounceChoice(options, (_, _) => { });

            var uidsBeforeReset = new List<int>(_cardManager.CardsByUid.Keys);
            Assert.GreaterOrEqual(uidsBeforeReset.Count, 2);

            _selector.HideChoice();
            Assert.IsFalse(_selector.IsChoiceActive);

            _cardManager.ReleaseAll("Presentation.ResetCardSurface");

            var uid = uidsBeforeReset[0];
            var replacement = _cardManager.SpawnView(uid, CardManagerSingleton.StandardDefId);
            Assert.IsNotNull(replacement);

            _bouncePresenter.Teardown();

            Assert.IsTrue(_cardManager.TryGet(uid, out var stillRegistered));
            Assert.AreSame(replacement, stillRegistered);
        }
    }
}
