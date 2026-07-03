using NineGrid.Core;
using NineGrid.Presentation.Bridge;
using NineGrid.Presentation.Tests.Support;
using NineGrid.Presentation.Visuals;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    public sealed class TableNineHudModelBinderTests
    {
        [Test]
        public void Binder_CoinsAndInteraction_UpdatesStatusPanelImmediately()
        {
            NineGridArchitecture.ResetForTests();
            var architecture = NineGridArchitecture.Current;
            architecture.GetModel<PlayerModel>().Reset();

            var host = new GameObject("HudBinderTestHost");
            var statusHost = CreateStatusPanelHost();
            statusHost.transform.SetParent(host.transform, false);

            var binderGo = new GameObject("Binder");
            binderGo.transform.SetParent(host.transform, false);
            var binder = binderGo.AddComponent<TableNineHudModelBinder>();
            binder.Configure(architecture, statusHost.GetComponent<TableNineStatusPanelView>(), null, null);

            var goldText = statusHost.transform.Find("GoldText").GetComponentInChildren<TMPro.TextMeshProUGUI>();
            var interactionText = statusHost.transform.Find("InteractionText").GetComponentInChildren<TMPro.TextMeshProUGUI>();

            Assert.AreEqual("0", goldText.text);
            Assert.AreEqual("0", interactionText.text);

            architecture.GetModel<PlayerModel>().AddCoins(12);
            architecture.GetModel<PlayerModel>().AddInteractionCount(3);

            Assert.AreEqual("12", goldText.text);
            Assert.AreEqual("3", interactionText.text);

            Object.DestroyImmediate(host);
        }

        [Test]
        public void InGameHudPhasePolicy_GameplayPhases_ShowHud()
        {
            Assert.IsTrue(InGameHudPhasePolicy.ShouldShowGameplayHud(GamePhase.InteractionLoop));
            Assert.IsTrue(InGameHudPhasePolicy.ShouldShowGameplayHud(GamePhase.ClearCheck));
            Assert.IsFalse(InGameHudPhasePolicy.ShouldShowGameplayHud(GamePhase.RewardItemChoice));
            Assert.IsFalse(InGameHudPhasePolicy.ShouldShowGameplayHud(GamePhase.RoomChoice));
        }

        [Test]
        public void StatusPanelApplyAvatarCombatStats_OnlyAppliesAvatarCombatStats()
        {
            var statusHost = CreateStatusPanelHost();
            var panel = statusHost.GetComponent<TableNineStatusPanelView>();
            var goldText = statusHost.transform.Find("GoldText").GetComponentInChildren<TMPro.TextMeshProUGUI>();

            goldText.text = "99";
            panel.ApplyAvatarCombatStats(OrchestrationTestSnapshots.Minimal());

            Assert.AreEqual("99", goldText.text);

            Object.DestroyImmediate(statusHost);
        }

        private static GameObject CreateStatusPanelHost()
        {
            var statusHost = new GameObject("PlayerInfo Text");
            statusHost.AddComponent<TableNineStatusPanelView>();
            CreateTmpChild(statusHost.transform, "GoldText");
            CreateTmpChild(statusHost.transform, "InteractionText");
            CreateTmpChild(statusHost.transform, "HpText");
            CreateTmpChild(statusHost.transform, "ArmorText");
            statusHost.GetComponent<TableNineStatusPanelView>().TryWireFromHierarchy();
            return statusHost;
        }

        private static void CreateTmpChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.AddComponent<TMPro.TextMeshProUGUI>();
        }
    }
}
