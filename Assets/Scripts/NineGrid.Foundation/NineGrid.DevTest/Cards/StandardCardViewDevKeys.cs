#if UNITY_EDITOR || DEVELOPMENT_BUILD

using NineGrid.Cards;
using NineGrid.Cards.Presentation;
using NineGrid.DevTest;
using UnityEngine;

namespace NineGrid.DevTest.Cards
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StandardCardView))]
    public sealed class StandardCardViewDevKeys : TestKeyModuleBehaviour
    {
        protected override string ModuleId => "standard-card";

        protected override string DisplayName => LayerProfile != null
            ? $"{LayerProfile.DisplayName}::{gameObject.name}"
            : $"卡牌测试::{gameObject.name}";

        protected override void ConfigureBindings(TestKeyRegistrationBuilder builder)
        {
            var cardView = GetComponent<StandardCardView>();
            builder
                .Bind(KeyCode.Keypad1, "加护甲", () => Mutate(cardView, armorDelta: 1))
                .Bind(KeyCode.Keypad2, "减护甲", () => Mutate(cardView, armorDelta: -1))
                .Bind(KeyCode.Keypad3, "加攻血", () => Mutate(cardView, attackDelta: 1, hpDelta: 1));
        }

        private static void Mutate(
            StandardCardView cardView,
            int attackDelta = 0,
            int hpDelta = 0,
            int armorDelta = 0)
        {
            if (cardView == null)
            {
                return;
            }

            // Dev 调试走 ApplyPresentation，与正式 Commit 出口同族；禁止旁路公开 Set*。
            cardView.ApplyPresentation(new CardPresentationSnapshot
            {
                Attack = Mathf.Max(0, cardView.Attack + attackDelta),
                Hp = Mathf.Max(0, cardView.Health + hpDelta),
                Armor = Mathf.Max(0, cardView.Armor + armorDelta),
                FaceUp = true,
            });
        }
    }
}

#endif
