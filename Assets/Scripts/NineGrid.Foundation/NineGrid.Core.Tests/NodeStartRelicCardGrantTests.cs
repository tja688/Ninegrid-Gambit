using System.Collections.Generic;
using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
  /// <summary>
  /// 关卡开始遗物加卡：本关 DrawPile / ItemSlots 契约（不进携带卡包）。
  /// </summary>
  public sealed class NodeStartRelicCardGrantTests
  {
    private const string ThrowingKnifeDefId = "help.throwing_knife";
    private const string SwapCardDefId = "help.swap_card";

    private IArchitecture mArch;
    private IPhaseSystem mPhase;
    private IContentSystem mContent;

    [SetUp]
    public void SetUp()
    {
      NineGridArchitecture.ResetForTests();
      mArch = NineGridArchitecture.Current;
      mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, ContentCatalogBootstrap.Load());
      InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 17UL });
      mPhase = mArch.GetSystem<IPhaseSystem>();
      mContent = mArch.GetSystem<IContentSystem>();
    }

    [TearDown]
    public void TearDown()
    {
      NineGridArchitecture.ResetForTests();
    }

    [Test]
    public void ThrowingKnifeBag_OnNodeStart_AddsToDrawPileOnly()
    {
      mContent.ActivateRelic("relic.throwing_knife_bag");

      Assert.IsTrue(mPhase.StartNode(CreateMinimalNode()).Accepted);

      var deck = mArch.GetModel<DeckModel>();
      Assert.AreEqual(
        2,
        CountDefInList(mArch.GetModel<CardRegistry>(), deck.DrawPileUids, ThrowingKnifeDefId),
        "飞刀袋应在关卡开始时将两张飞刀写入本关抽牌堆");
      Assert.AreEqual(
        0,
        CountDefInList(mArch.GetModel<CardRegistry>(), deck.ItemSlotUids, ThrowingKnifeDefId),
        "关卡开始遗物加卡只写入本关牌堆，不进道具卡格");
    }

    [Test]
    public void SwapButton_OnNodeStart_AddsSwapCardToItemSlots()
    {
      mContent.ActivateRelic("relic.swap_button");

      Assert.IsTrue(mPhase.StartNode(CreateMinimalNode()).Accepted);

      var deck = mArch.GetModel<DeckModel>();
      Assert.AreEqual(
        1,
        CountDefInList(mArch.GetModel<CardRegistry>(), deck.ItemSlotUids, SwapCardDefId),
        "交换按钮应在关卡开始时将交换卡写入道具牌格");
    }

    [Test]
    public void ThrowingKnifeBag_EmitsCardSpawnedWithCauseAndUid()
    {
      mContent.ActivateRelic("relic.throwing_knife_bag");
      Assert.IsTrue(mPhase.StartNode(CreateMinimalNode()).Accepted);

      var entries = mArch.GetSystem<IActionPipelineSystem>().EventLog.Entries;
      var spawnCount = 0;
      for (var i = 0; i < entries.Count; i++)
      {
        var entry = entries[i];
        if (entry.Type != CoreEventType.CardSpawned
            || entry.SourceDefId != ThrowingKnifeDefId
            || entry.Cause != "relic.throwing_knife_bag")
        {
          continue;
        }

        Assert.Greater(entry.CardUid, 0, "node_start 加卡应带 CardUid 供表现捕获");
        spawnCount++;
      }

      Assert.AreEqual(2, spawnCount, "应记录两次 CardSpawned(relic.throwing_knife_bag)");
    }

    private static NodeDeckOptions CreateMinimalNode()
    {
      return new NodeDeckOptions
      {
        PlayerOpeningCount = 0,
        EnemyOpeningCount = 1,
      }.AddEnemyCard(new CardDraft("monster.test", CardKind.Monster) { MaxHp = 5, Attack = 0 });
    }

    private static int CountDefInList(CardRegistry registry, IReadOnlyList<int> uids, string defId)
    {
      var count = 0;
      for (var i = 0; i < uids.Count; i++)
      {
        if (registry.TryGet(uids[i], out var card) && card != null && card.DefId == defId)
        {
          count++;
        }
      }

      return count;
    }
  }
}
