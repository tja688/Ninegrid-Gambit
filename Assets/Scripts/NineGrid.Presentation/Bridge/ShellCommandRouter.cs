using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Presentation.Bridge
{
    /// <summary>
    /// Shell 选择确认 → <see cref="CommandGateway"/> 真实 Core Command 路由。
    /// </summary>
    public sealed class ShellCommandRouter
    {
        public const string DefaultMonsterDeckId = "deck.wandering_legion";

        public ShellCommandRouter(CommandGateway gateway, IArchitecture architecture)
        {
            Gateway = gateway;
            Architecture = architecture;
        }

        public CommandGateway Gateway { get; }
        public IArchitecture Architecture { get; }

        public bool IsProduction => Gateway != null && Architecture != null;

        public CoreViewSnapshot CaptureSnapshot()
        {
            return Architecture != null ? CoreViewSnapshotFactory.Capture(Architecture) : null;
        }

        public CoreCommandDispatchResult SendStartNode()
        {
            if (!IsProduction)
            {
                return null;
            }

            var run = Architecture.GetModel<RunModel>();
            var rewardSystem = Architecture.GetSystem<IRewardSystem>();
            var catalogNodeIndex = run.NodeIndex.Value + 1;
            var options = rewardSystem.BuildNodeDeckOptions(catalogNodeIndex, DefaultMonsterDeckId);
            return Gateway.Send(new StartNodeCommand(options));
        }

        public CoreCommandDispatchResult SendSelectReward(int optionIndex)
        {
            return IsProduction ? Gateway.Send(new SelectRewardCommand(optionIndex)) : null;
        }

        public CoreCommandDispatchResult SendSkipHelpChoice()
        {
            return IsProduction ? Gateway.Send(new SkipHelpChoiceCommand()) : null;
        }

        public CoreCommandDispatchResult SendSelectRoom(int optionIndex)
        {
            return IsProduction ? Gateway.Send(new SelectRoomCommand(optionIndex)) : null;
        }

        public CoreCommandDispatchResult SendEnterRoom()
        {
            return IsProduction ? Gateway.Send(new EnterRoomCommand()) : null;
        }
    }
}
