using NineGrid.Cards;
using NineGrid.Flow;
using NineGrid.Presentation.Controllers;
using UnityEngine;

namespace NineGrid.Presentation.Setup
{
    /// <summary>
    /// 场景表现组合根：持有宿主 SerializeField，显式接线 Controllers/Hooks。
    /// C2：取代 ManagerSingleton.Instance / ResolveManagers 散点查找。
    /// 导演仍由 InBattle 宿主装配（A1 后迁入 PresentationCompositionRoot.Install）。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class PresentationSceneRoot : PresentationController
    {
        [Header("Flow Hosts")]
        [SerializeField] private InBattleManagerSingleton inBattle;
        [SerializeField] private MainGameLoopManagerSingleton mainGameLoop;
        [SerializeField] private RelicManagerSingleton relicManager;
        [SerializeField] private SelectorManagerSingleton selectorManager;
        [SerializeField] private DescriptionManagerSingleton descriptionManager;
        [SerializeField] private DamageNumberManagerSingleton damageNumberManager;
        [SerializeField] private GoldGainFxManagerSingleton goldGainFxManager;

        [Header("Card Hosts")]
        [SerializeField] private CardManagerSingleton cardManager;
        [SerializeField] private CardHandManagerSingleton cardHand;
        [SerializeField] private CardDeckManagerSingleton cardDeck;
        [SerializeField] private GroundFieldManagerSingleton groundField;
        [SerializeField] private FieldBattleManagerSingleton fieldBattle;

        protected override void OnBind()
        {
            WireHosts();
        }

        protected override void OnUnbind()
        {
            // Hook 清理由各宿主 OnDestroy / Controller OnUnbind 负责。
        }

        /// <summary>EditMode / Pipeline 装配后可显式再接线。</summary>
        public void WireHosts()
        {
            if (inBattle != null)
            {
                inBattle.BindSceneHosts(
                    cardManager,
                    cardDeck,
                    groundField,
                    relicManager,
                    cardHand,
                    fieldBattle,
                    mainGameLoop);
            }

            if (mainGameLoop != null)
            {
                mainGameLoop.BindSceneHosts(selectorManager, inBattle);
            }

            if (cardManager != null || cardHand != null || cardDeck != null)
            {
                CardEntityLifecycleHook.RequestWire(cardManager, cardHand, cardDeck);
            }

            if (groundField != null)
            {
                GroundFieldGeometryHook.RequestWire(groundField);
                ExploreInputHook.RequestWire(groundField);
            }

            if (fieldBattle != null)
            {
                FieldBattlePresentationHook.RequestWire(fieldBattle);
                AttackInputHook.RequestWire(fieldBattle);
            }

            if (cardHand != null)
            {
                UseItemInputHook.RequestWire(cardHand);
                PickupInputHook.RequestWire(cardHand);
            }

            GameFlowShellHook.RequestWire();
            RoomChoiceCoreHook.RequestWire();
            RewardChoiceCoreHook.RequestWire();
            RelicHudHook.RequestWire();

            DamageNumberOutputController.EnsureInstalled();
            DescriptionOutputController.EnsureInstalled();
            TriggerPulseOutputController.EnsureInstalled();
            DiagnosticOutputController.EnsureInstalled();
            GoldGainPresentationBinder.EnsureInstalled();

            // 输出宿主由场景 SerializeField 保活（自身 Awake 接线）；组合根校验非空以免漏挂。
            if (descriptionManager == null)
            {
                Debug.LogWarning("[PresentationSceneRoot] descriptionManager 未绑定。");
            }

            if (damageNumberManager == null)
            {
                Debug.LogWarning("[PresentationSceneRoot] damageNumberManager 未绑定。");
            }

            if (goldGainFxManager == null)
            {
                Debug.LogWarning("[PresentationSceneRoot] goldGainFxManager 未绑定。");
            }
        }
    }
}
