using NineGrid.Core.Systems;
using NineGrid.Core.Effects;
using NineGrid.Core.Utilities;
using QFramework;

namespace NineGrid.Core
{
    public sealed class NineGridArchitecture : Architecture<NineGridArchitecture>
    {
        protected override void Init()
        {
            RegisterUtility<IRngUtility>(new DeterministicRngUtility());
            RegisterUtility<ILogUtility>(new InMemoryLogUtility());
            RegisterUtility<IConfigUtility>(new InMemoryConfigUtility());

            RegisterModel(new CardRegistry());
            RegisterModel(new BoardModel());
            RegisterModel(new DeckModel());
            RegisterModel(new PlayerModel());
            RegisterModel(new RelicRunContributionModel());
            RegisterModel(new RunModel());
            RegisterModel(new BattleContextModel());
            RegisterModel(new PendingChoiceModel());

            RegisterSystem<IStatSystem>(new StatSystem());
            RegisterSystem<IBattleScopeSystem>(new BattleScopeSystem());
            RegisterSystem<ITriggerSystem>(new TriggerSystem());
            RegisterSystem<IContentSystem>(new ContentSystem());
            RegisterSystem<IEffectSystem>(new EffectSystem());
            RegisterSystem<IEconomySystem>(new EconomySystem());
            RegisterSystem<IRewardSystem>(new RewardSystem());
            RegisterSystem<IPresentationSyncSystem>(new PresentationSyncSystem());
            RegisterSystem<IActionPipelineSystem>(new ActionPipelineSystem());
            RegisterSystem<IBoardSystem>(new BoardSystem());
            RegisterSystem<IDeckSystem>(new DeckSystem());
            RegisterSystem<IPhaseSystem>(new PhaseSystem());
        }

        public static IArchitecture Current
        {
            get { return Interface; }
        }

        public static void ResetForTests()
        {
            if (mArchitecture != null)
            {
                mArchitecture.Deinit();
            }
        }
    }
}
