using System.Collections;
using NineGrid.Core;
using NineGrid.Core.Commands;
using Sirenix.OdinInspector;
using UnityEngine;

namespace NineGrid.Presentation.Debugging.Slices._Throwaway
{
    public enum SliceDeckSize
    {
        [LabelText("15 张")]
        Fifteen = 15,
        [LabelText("20 张")]
        Twenty = 20,
        [LabelText("25 张")]
        TwentyFive = 25,
    }

    /// <summary>
    /// 垂直切片 #1：牌组入场（CardDeckEntry）→ 8 张开局发牌（CardDeal / FillEmptySlots）。
    /// 走真实 StartNodeCommand 管线；演员为 Standard Card prefab，锚点为 CardDeckAnchors + NineGridAnchors。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeckEntryDealSliceDriver : ScenarioDriverBase
    {
        private const string SliceMonsterDefPrefix = "slice.deck_entry.monster";

        [LabelText("牌组规模")]
        public SliceDeckSize deckSize = SliceDeckSize.Twenty;

        [Button("开始测试"), ShowIf("@UnityEngine.Application.isPlaying")]
        public void StartTest()
        {
            Seed();
        }

        protected override NodeDeckOptions BuildDeck()
        {
            int count = (int)deckSize;
            var options = new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = count,
            };

            for (var i = 0; i < count; i++)
            {
                options.AddEnemyCard(new CardDraft($"{SliceMonsterDefPrefix}.{i:D2}", CardKind.Monster)
                {
                    MaxHp = 1,
                    Attack = 0,
                });
            }

            return options;
        }

        protected override IEnumerator RunScenario()
        {
            yield return SendAndWait(new StartNodeCommand(BuildDeck()));
        }
    }
}
