#if UNITY_EDITOR || DEVELOPMENT_BUILD

using NineGrid.Cards;
using UnityEngine;

namespace NineGrid.DevTest.Cards
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StandardCardView))]
    public sealed class StandardCardViewDevKeys : TestKeyModuleBehaviour
    {
        protected override string ModuleId => $"standard-card::{GetInstanceID()}";

        protected override string DisplayName => $"卡牌测试::{gameObject.name}";

        protected override void ConfigureBindings(TestKeyRegistrationBuilder builder)
        {
            var cardView = GetComponent<StandardCardView>();
            builder
                .Bind(KeyCode.Keypad1, "加护甲", () => cardView.AddArmor(1))
                .Bind(KeyCode.Keypad2, "减护甲", () => cardView.AddArmor(-1))
                .Bind(KeyCode.Keypad3, "加攻血", () =>
                {
                    cardView.AddAttack(1);
                    cardView.AddHealth(1);
                });
        }
    }
}

#endif
