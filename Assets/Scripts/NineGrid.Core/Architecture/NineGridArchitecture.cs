using NineGrid.Core.Systems;
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
            RegisterModel(new RunModel());

            RegisterSystem<IStatSystem>(new StatSystem());
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
