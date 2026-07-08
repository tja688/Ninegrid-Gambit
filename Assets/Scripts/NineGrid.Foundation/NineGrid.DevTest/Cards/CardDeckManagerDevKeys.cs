#if UNITY_EDITOR || DEVELOPMENT_BUILD

using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.DevTest;
using UnityEngine;

namespace NineGrid.DevTest.Cards
{
    [DisallowMultipleComponent]
    public sealed class CardDeckManagerDevKeys : TestKeyModuleBehaviour
    {
        private const int DefaultEntryCardCount = 15;

        [Tooltip("运行时自动查找 CardDeckManagerSingleton.Instance；也可手动拖入覆盖。")]
        [SerializeField] private CardDeckManagerSingleton deckManager;

        protected override string ModuleId => "card-deck-manager";

        protected override void OnEnable()
        {
            if (deckManager == null)
            {
                deckManager = GetComponent<CardDeckManagerSingleton>();
            }

            if (deckManager == null)
            {
                deckManager = CardDeckManagerSingleton.Instance;
            }

            base.OnEnable();
        }

        protected override void ConfigureBindings(TestKeyRegistrationBuilder builder)
        {
            builder
                .Bind(KeyCode.Keypad1, "纯表现入场+开局发牌", () => RunEntryAndOpeningDealAsync().Forget())
                .Bind(KeyCode.Keypad2, "清场+随机发1张", () => RunSingleDealAsync().Forget())
                .Bind(KeyCode.Keypad3, "随机槽位增卡", () => RunRandomAddCardAsync().Forget());
        }

        private async UniTaskVoid RunEntryAndOpeningDealAsync()
        {
            var manager = ResolveDeckManager();
            if (manager == null)
            {
                return;
            }

            var cardManager = CardManagerSingleton.Instance;
            var cards = cardManager.SpawnMany(
                CardManagerSingleton.StandardDefId,
                DefaultEntryCardCount,
                initialMode: CardDisplayMode.CardDeckMode);

            manager.InjectDeck(cards);
            await manager.BeginEntryAsync();

            var avatar = cardManager.Spawn(
                CardManagerSingleton.StandardDefId,
                initialMode: CardDisplayMode.GroundCardMode);
            var field = GroundFieldManagerSingleton.Instance;
            if (field != null)
            {
                field.RequestRevealAvatarAsync(avatar).Forget();
            }
            else
            {
                Debug.LogWarning("[CardDeckManagerDevKeys] 未找到 GroundFieldManagerSingleton，跳过 Avatar 入场。");
                cardManager.Release(avatar);
            }

            manager.DealOpeningRing();
        }

        private async UniTaskVoid RunSingleDealAsync()
        {
            var manager = ResolveDeckManager();
            if (manager == null)
            {
                return;
            }

            manager.ClearGround();

            if (!manager.TryGetFirstDeckSlot(out _) ||
                !manager.TryGetRandomEmptyGroundSlot(out var groundSlot))
            {
                Debug.LogWarning("[CardDeckManagerDevKeys] 无可用卡组牌或 Ground 空槽。");
                return;
            }

            manager.DealFirstCard(groundSlot);
            await UniTask.CompletedTask;
        }

        private async UniTaskVoid RunRandomAddCardAsync()
        {
            var manager = ResolveDeckManager();
            if (manager == null)
            {
                return;
            }

            if (manager.CurrentMode != CardDeckMode.InGame)
            {
                Debug.LogWarning("[CardDeckManagerDevKeys] 增卡测试需先完成入场进入 InGame。");
                return;
            }

            var card = CardManagerSingleton.Instance.Spawn(
                CardManagerSingleton.StandardDefId,
                initialMode: CardDisplayMode.CardDeckMode);

            var slotIndex = Random.Range(0, Mathf.Max(1, manager.DeckCount + 1));
            await manager.AddCardAtAsync(slotIndex, card);
        }

        private CardDeckManagerSingleton ResolveDeckManager()
        {
            if (deckManager != null)
            {
                return deckManager;
            }

            deckManager = CardDeckManagerSingleton.Instance;
            if (deckManager == null)
            {
                Debug.LogWarning("[CardDeckManagerDevKeys] 未找到 CardDeckManagerSingleton。");
            }

            return deckManager;
        }
    }
}

#endif
