using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Presentation.Bridge;
using QFramework;
using Sirenix.OdinInspector;
using UnityEngine;

namespace NineGrid.Presentation.Debugging.Slices
{
    /// <summary>
    /// 单局战斗闭环 Driver：走真实 <see cref="IRewardSystem.BuildNodeDeckOptions"/> + <see cref="StartNodeCommand"/>。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleSessionDriver : ScenarioDriverBase
    {
        public const string DefaultMonsterDeckId = ShellCommandRouter.DefaultMonsterDeckId;

        [LabelText("节点索引（catalog 1-based）")]
        [SerializeField]
        private int nodeIndex = 1;

        [LabelText("怪物牌组 defId")]
        [SerializeField]
        private string monsterDeckId = DefaultMonsterDeckId;

        [LabelText("测试用小牌组（3 只 catalog 怪）")]
        [SerializeField]
        private bool useMinimalTestDeck = true;

        protected override NodeDeckOptions BuildDeck()
        {
            IArchitecture architecture = Architecture;
            if (architecture == null)
            {
                return NodeDeckOptions.CreateDefaultBattle();
            }

            var rewardSystem = architecture.GetSystem<IRewardSystem>();
            NodeDeckOptions options = rewardSystem.BuildNodeDeckOptions(nodeIndex, monsterDeckId);
            if (useMinimalTestDeck)
            {
                options = BattleSessionDeckOptions.BuildMinimalClearDeck(architecture, options);
            }

            return options;
        }

        [Button("Catalog 节点开战"), ShowIf("@UnityEngine.Application.isPlaying")]
        public void StartCatalogNode()
        {
            Seed();
        }
    }
}
