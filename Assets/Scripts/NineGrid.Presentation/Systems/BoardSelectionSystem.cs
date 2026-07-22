using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.Core;
using QFramework;

namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// BoardSelect / 手牌拖放校验门面；实现委托会话 Executor。
    /// </summary>
    public interface IBoardSelectionSystem : ISystem
    {
        UniTask<bool> ValidateHandDragApplyAsync(ManagedCard card, int? targetGroundSlot);

        void AbortBoardSelectIfActive(string reason);

        UniTask OnBoardSelectionCompletedAsync(int itemUid, int[] selectedUids);

        UniTask OnBoardSelectionAbortedAsync(int itemUid, string defId, string reason);
    }

    public sealed class BoardSelectionSystem : AbstractSystem, IBoardSelectionSystem
    {
        protected override void OnInit()
        {
        }

        public UniTask<bool> ValidateHandDragApplyAsync(ManagedCard card, int? targetGroundSlot)
        {
            return ResolveSession().ValidateHandDragApplyAsync(card, targetGroundSlot);
        }

        public void AbortBoardSelectIfActive(string reason)
        {
            ResolveSession().AbortBoardSelectIfActive(reason);
        }

        public UniTask OnBoardSelectionCompletedAsync(int itemUid, int[] selectedUids)
        {
            return ResolveSession().OnBoardSelectionCompletedAsync(itemUid, selectedUids);
        }

        public UniTask OnBoardSelectionAbortedAsync(int itemUid, string defId, string reason)
        {
            return ResolveSession().OnBoardSelectionAbortedAsync(itemUid, defId, reason);
        }

        private static IBattleSessionSystem ResolveSession()
        {
            return BattleSessionSystem.EnsureRegistered();
        }

        public static IBoardSelectionSystem EnsureRegistered(IArchitecture architecture = null)
        {
            architecture ??= NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            var existing = architecture.GetSystem<IBoardSelectionSystem>();
            if (existing != null)
            {
                return existing;
            }

            var created = new BoardSelectionSystem();
            architecture.RegisterSystem<IBoardSelectionSystem>(created);
            return created;
        }
    }
}
