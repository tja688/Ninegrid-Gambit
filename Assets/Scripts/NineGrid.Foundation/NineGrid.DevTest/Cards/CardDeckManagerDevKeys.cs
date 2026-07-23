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

        [Tooltip("运行时查找场景中的 CardDeckManagerSingleton；也可手动拖入覆盖。")]
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
                deckManager = UnityEngine.Object.FindFirstObjectByType<CardDeckManagerSingleton>();
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

            var cardManager = UnityEngine.Object.FindFirstObjectByType<CardManagerSingleton>();
            var cards = cardManager.SpawnMany(
                CardManagerSingleton.StandardDefId,
                DefaultEntryCardCount,
                initialMode: CardDisplayMode.CardDeckMode,
                kind: CardPresentationKind.Monster);

            manager.InjectDeck(cards);
            await manager.BeginEntryAsync();

            var avatar = cardManager.Spawn(
                CardManagerSingleton.StandardDefId,
                initialMode: CardDisplayMode.GroundCardMode,
                kind: CardPresentationKind.Avatar);
            var field = UnityEngine.Object.FindFirstObjectByType<GroundFieldView>();
            if (field != null)
            {
                field.RequestRevealAvatarAsync(avatar).Forget();
            }
            else
            {
                Debug.LogWarning("[CardDeckManagerDevKeys] 未找到 GroundFieldView，跳过 Avatar 入场。");
                cardManager.Release(avatar, "DevTest.DeckAvatar");
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

            var cardManager = UnityEngine.Object.FindFirstObjectByType<CardManagerSingleton>();
            if (cardManager == null)
            {
                Debug.LogWarning("[CardDeckManagerDevKeys] 未找到 CardManagerSingleton。");
                return;
            }

            var card = cardManager.Spawn(
                CardManagerSingleton.StandardDefId,
                initialMode: CardDisplayMode.CardDeckMode,
                kind: CardPresentationKind.HelpCard);

            var slotIndex = Random.Range(0, Mathf.Max(1, manager.DeckCount + 1));
            await manager.AddCardAtAsync(slotIndex, card);
        }

        private CardDeckManagerSingleton ResolveDeckManager()
        {
            if (deckManager != null)
            {
                return deckManager;
            }

            deckManager = UnityEngine.Object.FindFirstObjectByType<CardDeckManagerSingleton>();
            if (deckManager == null)
            {
                Debug.LogWarning("[CardDeckManagerDevKeys] 未找到 CardDeckManagerSingleton。");
            }

            return deckManager;
        }
    }
}

#endif
