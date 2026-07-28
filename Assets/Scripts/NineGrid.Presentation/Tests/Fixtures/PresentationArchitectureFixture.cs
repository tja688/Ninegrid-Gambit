using System;
using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Systems;
using NineGrid.Flow.Presentation;
using QFramework;

namespace NineGrid.Presentation.Tests.Fixtures
{
    /// <summary>
    /// 迁移票复用：重置 Architecture，可选开战，暴露常用 Core 接缝。
    /// </summary>
    public sealed class PresentationArchitectureFixture : IDisposable
    {
        public IArchitecture Architecture { get; private set; }
        public IPhaseSystem Phase { get; private set; }
        public IActionPipelineSystem Pipeline { get; private set; }
        public IPresentationSyncSystem Sync { get; private set; }
        public CoreCommandDispatcher Dispatcher { get; private set; }
        public BoardModel Board { get; private set; }
        public CardRegistry Registry { get; private set; }

        private PresentationArchitectureFixture(IArchitecture architecture, bool startedGame)
        {
            Architecture = architecture;
            if (startedGame)
            {
                Phase = architecture.GetSystem<IPhaseSystem>();
                Pipeline = architecture.GetSystem<IActionPipelineSystem>();
                Sync = architecture.GetSystem<IPresentationSyncSystem>();
                Dispatcher = new CoreCommandDispatcher(architecture);
                Board = architecture.GetModel<BoardModel>();
                Registry = architecture.GetModel<CardRegistry>();
            }
        }

        public static PresentationArchitectureFixture CreateBare()
        {
            NineGridArchitecture.ResetForTests();
            OccupancyForceSyncGuard.ResetForTests();
            return new PresentationArchitectureFixture(NineGridArchitecture.Current, startedGame: false);
        }

        public static PresentationArchitectureFixture CreateStartedGame(ulong seed = 42UL)
        {
            NineGridArchitecture.ResetForTests();
            OccupancyForceSyncGuard.ResetForTests();
            var architecture = NineGridArchitecture.Current;
            InitialGameFactory.Create(architecture, new InitialGameOptions { Seed = seed });
            return new PresentationArchitectureFixture(architecture, startedGame: true);
        }

        /// <summary>
        /// 注入默认 ContentCatalog 后再开战（融合技能等依赖编目）。
        /// </summary>
        public static PresentationArchitectureFixture CreateStartedGameWithCatalog(ulong seed = 42UL)
        {
            NineGridArchitecture.ResetForTests();
            OccupancyForceSyncGuard.ResetForTests();
            var architecture = NineGridArchitecture.Current;
            architecture.GetUtility<NineGrid.Core.Utilities.IConfigUtility>().Set(
                NineGrid.Core.Content.ContentConfigKeys.DefaultCatalog,
                ContentCatalogBootstrap.Load());
            InitialGameFactory.Create(architecture, new InitialGameOptions { Seed = seed });
            return new PresentationArchitectureFixture(architecture, startedGame: true);
        }

        public void PlaceSoleBoardCardAt(SlotId targetSlot)
        {
            CardInstance sole = null;
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                if (slot == Board.AvatarSlot.Value)
                {
                    continue;
                }

                var uid = Board.GetCardUid(slot);
                if (uid == 0)
                {
                    continue;
                }

                if (sole != null)
                {
                    throw new InvalidOperationException(
                        "Expected at most one non-avatar board card for relocate helper.");
                }

                sole = Registry.Get(uid);
            }

            if (sole == null)
            {
                throw new InvalidOperationException("No board card to relocate.");
            }

            if (sole.Slot.Value == targetSlot)
            {
                return;
            }

            Board.ClearSlot(sole.Slot.Value);
            Board.PlaceCard(sole, targetSlot);
        }

        public void Dispose()
        {
            OccupancyForceSyncGuard.ResetForTests();
            NineGridArchitecture.ResetForTests();
            Architecture = null;
            Phase = null;
            Pipeline = null;
            Sync = null;
            Dispatcher = null;
            Board = null;
            Registry = null;
        }
    }
}
