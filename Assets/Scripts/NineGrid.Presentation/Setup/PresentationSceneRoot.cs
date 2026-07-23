using System;
using NineGrid.Cards;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Controllers;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Setup
{
    /// <summary>
    /// 场景表现组合根：持有宿主 SerializeField，显式接线 Controllers/Hooks，
    /// 并拥有唯一生产 Runtime 的 Install / Tick / Shutdown。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class PresentationSceneRoot : PresentationController
    {
        [Header("Flow Hosts")]
        [SerializeField] private BattleSessionController inBattle;
        [SerializeField] private GameFlowController mainGameLoop;
        [SerializeField] private RelicManagerSingleton relicManager;
        [SerializeField] private SelectorManagerSingleton selectorManager;
        [SerializeField] private DescriptionManagerSingleton descriptionManager;
        [SerializeField] private DamageNumberManagerSingleton damageNumberManager;
        [SerializeField] private GoldGainFxManagerSingleton goldGainFxManager;

        [Header("Card Hosts")]
        [SerializeField] private CardManagerSingleton cardManager;
        [SerializeField] private CardHandManagerSingleton cardHand;
        [SerializeField] private CardDeckManagerSingleton cardDeck;
        [SerializeField] private GroundFieldView groundField;
        [SerializeField] private FieldBattleView fieldBattle;

        private readonly PresentationCompositionRoot mComposition = new PresentationCompositionRoot();
        private PresentationSceneBindings mBindings;
        private bool mInstalled;

        protected override void OnBind()
        {
            WireHosts();
            if (inBattle != null)
            {
                inBattle.BindRuntimeLifecycle(EnsureInstalled, ShutdownRuntime);
            }

            this.RegisterEvent<TeardownPresentationDirectorRequested>(OnTeardownRequested)
                .AddToUnregisterList(this);
        }

        protected override void OnUnbind()
        {
            ShutdownRuntime(IntentClearReason.LayerChange);
            if (inBattle != null)
            {
                inBattle.BindRuntimeLifecycle(null, null);
            }

            mBindings = null;
        }

        private void Update()
        {
            if (!mInstalled)
            {
                return;
            }

            var runtime = this.GetSystem<IPresentationRuntimeSystem>();
            if (runtime != null && runtime.IsStarted)
            {
                runtime.Tick(Time.deltaTime);
            }
        }

        /// <summary>局内开始时幂等安装生产 Runtime；返回导演是否已启动。</summary>
        public bool EnsureInstalled()
        {
            if (!mInstalled)
            {
                mBindings = BuildBindings();
                mComposition.Install(mBindings);
                mInstalled = true;
            }

            var runtime = this.GetSystem<IPresentationRuntimeSystem>();
            return runtime != null && runtime.IsStarted;
        }

        /// <summary>幂等关停；未安装时 no-op。</summary>
        public void ShutdownRuntime(IntentClearReason reason)
        {
            if (!mInstalled)
            {
                return;
            }

            mComposition.Shutdown(reason);
            mInstalled = false;
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
                GameFlowShellSystem.EnsureRegistered().Bind(mainGameLoop);
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

        private PresentationSceneBindings BuildBindings()
        {
            return new PresentationSceneBindings(
                inBattle,
                mainGameLoop,
                relicManager,
                selectorManager,
                descriptionManager,
                damageNumberManager,
                goldGainFxManager,
                cardManager,
                cardHand,
                cardDeck,
                groundField,
                fieldBattle);
        }

        private void OnTeardownRequested(TeardownPresentationDirectorRequested e)
        {
            ShutdownRuntime(e.Reason);
        }
    }
}
