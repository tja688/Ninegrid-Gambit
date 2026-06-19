using QFramework;

namespace NineGrid.Core
{
    public sealed class RunModel : AbstractModel
    {
        public BindableProperty<int> Floor { get; private set; }
        public BindableProperty<int> NodeIndex { get; private set; }
        public BindableProperty<ulong> Seed { get; private set; }
        public BindableProperty<RoomKind> Room { get; private set; }
        public BindableProperty<GamePhase> Phase { get; private set; }
        public BindableProperty<int> Version { get; private set; }

        protected override void OnInit()
        {
            if (Floor == null)
            {
                Floor = new BindableProperty<int>(1);
                NodeIndex = new BindableProperty<int>(0);
                Seed = new BindableProperty<ulong>(1UL);
                Room = new BindableProperty<RoomKind>(RoomKind.None);
                Phase = new BindableProperty<GamePhase>(GamePhase.None);
                Version = new BindableProperty<int>(0);
            }
        }

        public void Reset(ulong seed)
        {
            Floor.Value = 1;
            NodeIndex.Value = 0;
            Seed.Value = seed;
            Room.Value = RoomKind.Battle;
            Phase.Value = GamePhase.BuildEnemyPool;
            Touch();
        }

        public void SetPhase(GamePhase phase)
        {
            Phase.Value = phase;
            Touch();
        }

        public void AdvanceNode()
        {
            NodeIndex.Value++;
            Touch();
        }

        private void Touch()
        {
            if (Version != null)
            {
                Version.Value++;
            }
        }
    }
}
