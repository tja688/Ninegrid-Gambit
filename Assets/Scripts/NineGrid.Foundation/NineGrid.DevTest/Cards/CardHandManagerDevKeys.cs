#if UNITY_EDITOR || DEVELOPMENT_BUILD

using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.DevTest;
using UnityEngine;

namespace NineGrid.DevTest.Cards
{
    [DisallowMultipleComponent]
    public sealed class CardHandManagerDevKeys : TestKeyModuleBehaviour
    {
        [Tooltip("运行时查找场景中的 CardHandManagerSingleton；也可手动拖入覆盖。")]
        [SerializeField] private CardHandManagerSingleton handManager;

        protected override string ModuleId => "card-hand-manager";

        protected override void OnEnable()
        {
            if (handManager == null)
            {
                handManager = GetComponent<CardHandManagerSingleton>();
            }

            if (handManager == null)
            {
                handManager = UnityEngine.Object.FindFirstObjectByType<CardHandManagerSingleton>();
            }

            base.OnEnable();
        }

        protected override void ConfigureBindings(TestKeyRegistrationBuilder builder)
        {
            builder.Bind(
                KeyCode.Keypad7,
                "随机抓取场地卡入手",
                () => RunPullRandomGroundCardAsync().Forget());
        }

        private async UniTaskVoid RunPullRandomGroundCardAsync()
        {
            var hand = ResolveHandManager();
            if (hand == null)
            {
                return;
            }

            if (!hand.CanAcceptCard)
            {
                Debug.LogWarning("[CardHandManagerDevKeys] 手牌已满或忙碌，无法抓取。");
                return;
            }

            var field = UnityEngine.Object.FindFirstObjectByType<GroundFieldView>();
            if (field == null)
            {
                Debug.LogWarning("[CardHandManagerDevKeys] 未找到 GroundFieldView。");
                return;
            }

            if (field.IsBusy)
            {
                Debug.LogWarning("[CardHandManagerDevKeys] 场地管理器忙碌，请稍后再试。");
                return;
            }

            if (!field.TryGetRandomOccupiedCard(out var card))
            {
                Debug.LogWarning("[CardHandManagerDevKeys] 场上无卡牌可抓取。请先 Keypad1 发牌到场地。");
                return;
            }

            if (!field.TryTakeCardFromField(card.Uid, out var taken) || taken == null)
            {
                Debug.LogWarning("[CardHandManagerDevKeys] 从场地取卡失败。");
                return;
            }

            var success = await hand.PullFromGroundAsync(taken);
            if (!success)
            {
                Debug.LogWarning("[CardHandManagerDevKeys] 入手牌失败，已释放卡牌。");
                var cards = UnityEngine.Object.FindFirstObjectByType<CardManagerSingleton>();
                cards?.Release(taken, "DevTest.HandPickup");
            }
        }

        private CardHandManagerSingleton ResolveHandManager()
        {
            if (handManager != null)
            {
                return handManager;
            }

            handManager = UnityEngine.Object.FindFirstObjectByType<CardHandManagerSingleton>();
            if (handManager == null)
            {
                Debug.LogWarning("[CardHandManagerDevKeys] 未找到 CardHandManagerSingleton。");
            }

            return handManager;
        }
    }
}

#endif
