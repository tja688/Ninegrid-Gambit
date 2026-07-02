using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Presentation.Visuals;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Bridge
{
    /// <summary>
    /// HUD 被动视图：直连 <see cref="PlayerModel"/> / <see cref="RunModel"/> BindableProperty，不经 Batch 时间线。
    /// 卡面 HP/护甲仍由战斗 Flow + 批末 Reconcile 驱动。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TableNineHudModelBinder : MonoBehaviour
    {
        [SerializeField] private TableNineStatusPanelView statusPanel;
        [SerializeField] private TableNineContentIconStripView relicStrip;
        [SerializeField] private TableNineContentIconStripView skillStrip;

        private IArchitecture mArchitecture;
        private readonly List<IUnRegister> mUnregisters = new();

        public void Configure(
            IArchitecture architecture,
            TableNineStatusPanelView panel,
            TableNineContentIconStripView relicView,
            TableNineContentIconStripView skillView,
            GameObject[] visibilityRoots = null)
        {
            Unbind();
            mArchitecture = architecture;
            statusPanel = panel;
            relicStrip = relicView;
            skillStrip = skillView;
            Bind();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void Bind()
        {
            if (mArchitecture == null)
            {
                return;
            }

            PlayerModel player = mArchitecture.GetModel<PlayerModel>();
            if (player == null)
            {
                return;
            }

            Track(player.Coins.RegisterWithInitValue(OnCoinsChanged));
            Track(player.InteractionCount.RegisterWithInitValue(OnInteractionCountChanged));
            Track(player.Version.RegisterWithInitValue(_ => RefreshContentStrips()));

            RefreshContentStrips();
        }

        private void Unbind()
        {
            for (var i = mUnregisters.Count - 1; i >= 0; i--)
            {
                mUnregisters[i]?.UnRegister();
            }

            mUnregisters.Clear();
            mArchitecture = null;
        }

        private void Track(IUnRegister unregister)
        {
            if (unregister != null)
            {
                mUnregisters.Add(unregister);
            }
        }

        private void OnCoinsChanged(int coins)
        {
            statusPanel?.SetGold(coins);
        }

        private void OnInteractionCountChanged(int count)
        {
            statusPanel?.SetInteractionCount(count);
        }

        private void RefreshContentStrips()
        {
            if (mArchitecture == null)
            {
                return;
            }

            PlayerModel player = mArchitecture.GetModel<PlayerModel>();
            if (player == null)
            {
                return;
            }

            relicStrip?.ApplyDefIds(mArchitecture, player.RelicDefIds);
            skillStrip?.ApplyDefIds(mArchitecture, player.SkillDefIds);
        }
    }
}
