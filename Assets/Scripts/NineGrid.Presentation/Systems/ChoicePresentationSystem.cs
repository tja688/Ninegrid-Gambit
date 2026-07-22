using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Core;
using QFramework;

namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// 奖励 / StatBoost / 残留帮助卡结算 Present 门面；实现委托会话 Executor。
    /// </summary>
    public interface IChoicePresentationSystem : ISystem
    {
        UniTask PresentRewardChoiceFromCoreAsync(bool hoverOnNotice = false);

        UniTask PresentUnusedHelpCardSettlementFromEventLogAsync(
            int startIndex,
            CancellationToken cancellationToken = default);
    }

    public sealed class ChoicePresentationSystem : AbstractSystem, IChoicePresentationSystem
    {
        protected override void OnInit()
        {
        }

        public UniTask PresentRewardChoiceFromCoreAsync(bool hoverOnNotice = false)
        {
            return ResolveSession().PresentRewardChoiceFromCoreAsync(hoverOnNotice);
        }

        public UniTask PresentUnusedHelpCardSettlementFromEventLogAsync(
            int startIndex,
            CancellationToken cancellationToken = default)
        {
            return ResolveSession().PresentUnusedHelpCardSettlementFromEventLogAsync(
                startIndex,
                cancellationToken);
        }

        private static IBattleSessionSystem ResolveSession()
        {
            return BattleSessionSystem.EnsureRegistered();
        }

        public static IChoicePresentationSystem EnsureRegistered(IArchitecture architecture = null)
        {
            architecture ??= NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            var existing = architecture.GetSystem<IChoicePresentationSystem>();
            if (existing != null)
            {
                return existing;
            }

            var created = new ChoicePresentationSystem();
            architecture.RegisterSystem<IChoicePresentationSystem>(created);
            return created;
        }
    }
}
