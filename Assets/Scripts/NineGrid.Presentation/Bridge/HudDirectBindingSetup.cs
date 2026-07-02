using NineGrid.Core;
using NineGrid.Presentation.Flow.Shell;
using NineGrid.Presentation.Visuals;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Bridge
{
    /// <summary>
    /// 在 MainScene 解析 HUD 视图引用并装配 <see cref="TableNineHudModelBinder"/>。
    /// </summary>
    internal static class HudDirectBindingSetup
    {
        public static TableNineHudModelBinder Install(
            IArchitecture architecture,
            Transform bootstrapRoot,
            InGameUiFlow inGameUiFlow = null)
        {
            if (architecture == null)
            {
                return null;
            }

            TableNineStatusPanelView statusPanel = ResolveStatusPanel();
            TableNineContentIconStripView relicStrip = ResolveIconStrip("RelicPanel");
            TableNineContentIconStripView skillStrip = ResolveIconStrip("PlayerSkillPanel");

            var hudRoots = CollectHudRoots(statusPanel, relicStrip, skillStrip);
            RunModel run = architecture.GetModel<RunModel>();
            inGameUiFlow?.Configure(hudRoots);
            inGameUiFlow?.ApplyImmediateVisibility(
                InGameHudPhasePolicy.ShouldShowGameplayHud(run?.Phase?.Value ?? GamePhase.None));
            var host = new GameObject("TableNineHudModelBinder");
            if (bootstrapRoot != null)
            {
                host.transform.SetParent(bootstrapRoot, false);
            }

            var binder = host.AddComponent<TableNineHudModelBinder>();
            binder.Configure(architecture, statusPanel, relicStrip, skillStrip, hudRoots);
            return binder;
        }

        private static TableNineStatusPanelView ResolveStatusPanel()
        {
            TableNineStatusPanelView existing = Object.FindObjectOfType<TableNineStatusPanelView>();
            if (existing != null)
            {
                existing.TryWireFromHierarchy();
                return existing;
            }

            GameObject playerInfo = GameObject.Find("PlayerInfo Text");
            if (playerInfo == null)
            {
                return null;
            }

            var panel = playerInfo.GetComponent<TableNineStatusPanelView>();
            if (panel == null)
            {
                panel = playerInfo.AddComponent<TableNineStatusPanelView>();
            }

            panel.TryWireFromHierarchy();
            return panel;
        }

        private static TableNineContentIconStripView ResolveIconStrip(string objectName)
        {
            GameObject panelObject = GameObject.Find(objectName);
            if (panelObject == null)
            {
                return null;
            }

            var strip = panelObject.GetComponent<TableNineContentIconStripView>();
            if (strip == null)
            {
                strip = panelObject.AddComponent<TableNineContentIconStripView>();
            }

            return strip;
        }

        private static GameObject[] CollectHudRoots(
            TableNineStatusPanelView statusPanel,
            TableNineContentIconStripView relicStrip,
            TableNineContentIconStripView skillStrip)
        {
            var roots = new System.Collections.Generic.List<GameObject>(3);
            if (statusPanel != null)
            {
                roots.Add(statusPanel.gameObject);
            }

            if (relicStrip != null)
            {
                roots.Add(relicStrip.gameObject);
            }

            if (skillStrip != null)
            {
                roots.Add(skillStrip.gameObject);
            }

            return roots.ToArray();
        }
    }
}
