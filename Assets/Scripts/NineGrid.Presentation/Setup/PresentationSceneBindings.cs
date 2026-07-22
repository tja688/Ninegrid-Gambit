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
        public InBattleManagerSingleton InBattle { get; }
        public MainGameLoopManagerSingleton MainGameLoop { get; }
        public RelicManagerSingleton RelicManager { get; }
        public SelectorManagerSingleton SelectorManager { get; }
        public DescriptionManagerSingleton DescriptionManager { get; }
        public DamageNumberManagerSingleton DamageNumberManager { get; }
        public GoldGainFxManagerSingleton GoldGainFxManager { get; }
        public CardManagerSingleton CardManager { get; }
        public CardHandManagerSingleton CardHand { get; }
        public CardDeckManagerSingleton CardDeck { get; }
        public GroundFieldManagerSingleton GroundField { get; }
        public FieldBattleManagerSingleton FieldBattle { get; }

        public PresentationSceneBindings(
            InBattleManagerSingleton inBattle,
            MainGameLoopManagerSingleton mainGameLoop,
            RelicManagerSingleton relicManager,
            SelectorManagerSingleton selectorManager,
            DescriptionManagerSingleton descriptionManager,
            DamageNumberManagerSingleton damageNumberManager,
            GoldGainFxManagerSingleton goldGainFxManager,
            CardManagerSingleton cardManager,
            CardHandManagerSingleton cardHand,
            CardDeckManagerSingleton cardDeck,
            GroundFieldManagerSingleton groundField,
            FieldBattleManagerSingleton fieldBattle)
        {
            InBattle = inBattle ?? throw new ArgumentNullException("inBattle");
            MainGameLoop = mainGameLoop;
            RelicManager = relicManager;
            SelectorManager = selectorManager;
            DescriptionManager = descriptionManager;
            DamageNumberManager = damageNumberManager;
            GoldGainFxManager = goldGainFxManager;
            CardManager = cardManager;
            CardHand = cardHand;
            CardDeck = cardDeck;
            GroundField = groundField;
            FieldBattle = fieldBattle;
        }
    }
}
