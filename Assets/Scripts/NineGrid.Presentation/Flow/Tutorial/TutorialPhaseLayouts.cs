using System.Collections.Generic;
using NineGrid.Core;

namespace NineGrid.Flow.Tutorial
{
    /// <summary>五阶段教学：指定格直摆 + 抽牌堆序（index 0 = 下一张补出）。</summary>
    public static class TutorialPhaseLayouts
    {
        public readonly struct Placement
        {
            public Placement(int slot, string defId, CardKind kind)
            {
                Slot = slot;
                DefId = defId;
                Kind = kind;
            }

            public int Slot { get; }
            public string DefId { get; }
            public CardKind Kind { get; }
        }

        public static IReadOnlyList<Placement> GetBoardPlacements(int phase)
        {
            switch (phase)
            {
                case 1:
                    return new[]
                    {
                        new Placement(1, TutorialContentIds.Phase1HintA, CardKind.Trap),
                        new Placement(2, TutorialContentIds.Phase1HintB, CardKind.Trap),
                        new Placement(8, TutorialContentIds.DummyTrapDefId, CardKind.Trap),
                    };
                case 2:
                    return new[]
                    {
                        new Placement(1, TutorialContentIds.Phase2HintA, CardKind.Trap),
                        new Placement(2, TutorialContentIds.Phase2HintB, CardKind.Trap),
                        new Placement(9, TutorialContentIds.DummyTrapDefId, CardKind.Trap),
                    };
                case 3:
                    return new[]
                    {
                        new Placement(1, TutorialContentIds.Phase3HintA, CardKind.Trap),
                        new Placement(2, TutorialContentIds.Phase3HintB, CardKind.Trap),
                        new Placement(6, TutorialContentIds.ActionDummyDefId, CardKind.Trap),
                        new Placement(8, TutorialContentIds.MoveDummyDefId, CardKind.Trap),
                    };
                case 4:
                    return new[]
                    {
                        new Placement(1, TutorialContentIds.Phase4HintA, CardKind.Trap),
                        new Placement(2, TutorialContentIds.Phase4HintB, CardKind.Trap),
                        new Placement(6, TutorialContentIds.KnifeDefId, CardKind.HelpCard),
                        new Placement(8, TutorialContentIds.PotionDefId, CardKind.HelpCard),
                    };
                case 5:
                    return new[]
                    {
                        new Placement(1, TutorialContentIds.Phase5HintA, CardKind.Trap),
                        new Placement(2, TutorialContentIds.Phase5HintB, CardKind.Trap),
                        new Placement(8, TutorialContentIds.SteelSlimeDefId, CardKind.Monster),
                    };
                default:
                    return System.Array.Empty<Placement>();
            }
        }

        public static IReadOnlyList<string> GetDrawPileDefIds(int phase)
        {
            switch (phase)
            {
                case 2:
                    return new[] { TutorialContentIds.DummyTrapDefId };
                case 5:
                    var pile = new List<string>(10);
                    for (var i = 0; i < 5; i++)
                    {
                        pile.Add(TutorialContentIds.KnifeDefId);
                    }

                    for (var i = 0; i < 5; i++)
                    {
                        pile.Add(TutorialContentIds.PotionDefId);
                    }

                    return pile;
                default:
                    return System.Array.Empty<string>();
            }
        }

        public static IReadOnlyList<string> GetHintDefIds(int phase)
        {
            switch (phase)
            {
                case 1: return new[] { TutorialContentIds.Phase1HintA, TutorialContentIds.Phase1HintB };
                case 2: return new[] { TutorialContentIds.Phase2HintA, TutorialContentIds.Phase2HintB };
                case 3: return new[] { TutorialContentIds.Phase3HintA, TutorialContentIds.Phase3HintB };
                case 4: return new[] { TutorialContentIds.Phase4HintA, TutorialContentIds.Phase4HintB };
                case 5: return new[] { TutorialContentIds.Phase5HintA, TutorialContentIds.Phase5HintB };
                default: return System.Array.Empty<string>();
            }
        }
    }
}
