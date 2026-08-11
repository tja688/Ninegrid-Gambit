using System;
using NineGrid.Cards;
using NineGrid.Flow;

namespace NineGrid.Presentation.Setup
{
    /// <summary>
    /// 生产场景宿主绑定快照：组合根 Install 的不可变输入，不持有运行时状态。
    /// </summary>
    public sealed class PresentationSceneBindings
    {
        public BattleSessionController InBattle { get; }
        public GameFlowController MainGameLoop { get; }
        public RelicManagerSingleton RelicManager { get; }
        public SelectorManagerSingleton SelectorManager { get; }
        public DamageNumberManagerSingleton DamageNumberManager { get; }
        public CardManagerSingleton CardManager { get; }
        public CardHandManagerSingleton CardHand { get; }
        public CardDeckManagerSingleton CardDeck { get; }
        public GroundFieldView GroundField { get; }
        public FieldBattleView FieldBattle { get; }

        public PresentationSceneBindings(
            BattleSessionController inBattle,
            GameFlowController mainGameLoop,
            RelicManagerSingleton relicManager,
            SelectorManagerSingleton selectorManager,
            DamageNumberManagerSingleton damageNumberManager,
            CardManagerSingleton cardManager,
            CardHandManagerSingleton cardHand,
            CardDeckManagerSingleton cardDeck,
            GroundFieldView groundField,
            FieldBattleView fieldBattle)
        {
            InBattle = inBattle ?? throw new ArgumentNullException("inBattle");
            MainGameLoop = mainGameLoop;
            RelicManager = relicManager;
            SelectorManager = selectorManager;
            DamageNumberManager = damageNumberManager;
            CardManager = cardManager;
            CardHand = cardHand;
            CardDeck = cardDeck;
            GroundField = groundField;
            FieldBattle = fieldBattle;
        }
    }
}
