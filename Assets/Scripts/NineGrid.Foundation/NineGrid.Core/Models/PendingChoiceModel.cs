using System.Collections.Generic;
using NineGrid.Core.Content;
using QFramework;

namespace NineGrid.Core
{
    public sealed class PendingChoiceModel : AbstractModel
    {
        private readonly List<RewardEntry> mRewardOptions = new List<RewardEntry>();
        private readonly List<RoomKind> mRoomOptions = new List<RoomKind>();

        public BindableProperty<PendingChoiceKind> Kind { get; private set; }
        public BindableProperty<string> PoolId { get; private set; }
        public BindableProperty<RoomKind> SelectedRoom { get; private set; }
        public BindableProperty<NavigationKind> NavigationOffer { get; private set; }
        public BindableProperty<NavigationKind> SelectedNavigation { get; private set; }
        public BindableProperty<int> Version { get; private set; }

        public IReadOnlyList<RewardEntry> RewardOptions
        {
            get { return mRewardOptions; }
        }

        public IReadOnlyList<RoomKind> RoomOptions
        {
            get { return mRoomOptions; }
        }

        protected override void OnInit()
        {
            if (Kind == null)
            {
                Kind = new BindableProperty<PendingChoiceKind>(PendingChoiceKind.None);
                PoolId = new BindableProperty<string>(string.Empty);
                SelectedRoom = new BindableProperty<RoomKind>(RoomKind.None);
                NavigationOffer = new BindableProperty<NavigationKind>(NavigationKind.None);
                SelectedNavigation = new BindableProperty<NavigationKind>(NavigationKind.None);
                Version = new BindableProperty<int>(0);
            }
        }

        public void OfferRewards(string poolId, IReadOnlyList<RewardEntry> options)
        {
            mRewardOptions.Clear();
            mRoomOptions.Clear();
            if (options != null)
            {
                for (var i = 0; i < options.Count; i++)
                {
                    if (options[i] != null)
                    {
                        mRewardOptions.Add(options[i]);
                    }
                }
            }

            Kind.Value = PendingChoiceKind.Reward;
            PoolId.Value = poolId ?? string.Empty;
            SelectedRoom.Value = RoomKind.None;
            NavigationOffer.Value = NavigationKind.None;
            SelectedNavigation.Value = NavigationKind.None;
            Touch();
        }

        public void ClearRewardChoices()
        {
            mRewardOptions.Clear();
            if (Kind.Value == PendingChoiceKind.Reward)
            {
                Kind.Value = PendingChoiceKind.None;
            }

            PoolId.Value = string.Empty;
            Touch();
        }

        public void OfferRooms(IReadOnlyList<RoomKind> options)
        {
            mRewardOptions.Clear();
            mRoomOptions.Clear();
            if (options != null)
            {
                for (var i = 0; i < options.Count; i++)
                {
                    if (options[i] != RoomKind.None)
                    {
                        mRoomOptions.Add(options[i]);
                    }
                }
            }

            Kind.Value = PendingChoiceKind.Room;
            PoolId.Value = string.Empty;
            SelectedRoom.Value = RoomKind.None;
            NavigationOffer.Value = NavigationKind.None;
            SelectedNavigation.Value = NavigationKind.None;
            Touch();
        }

        public void OfferNavigation(NavigationKind kind)
        {
            mRewardOptions.Clear();
            mRoomOptions.Clear();
            Kind.Value = PendingChoiceKind.Navigation;
            PoolId.Value = string.Empty;
            SelectedRoom.Value = RoomKind.None;
            NavigationOffer.Value = kind;
            SelectedNavigation.Value = NavigationKind.None;
            Touch();
        }

        public void SelectRoom(RoomKind roomKind)
        {
            SelectedRoom.Value = roomKind;
            SelectedNavigation.Value = NavigationKind.None;
            Touch();
        }

        public void SelectNavigation(NavigationKind kind)
        {
            SelectedNavigation.Value = kind;
            SelectedRoom.Value = RoomKind.None;
            Touch();
        }

        public void Clear()
        {
            mRewardOptions.Clear();
            mRoomOptions.Clear();
            Kind.Value = PendingChoiceKind.None;
            PoolId.Value = string.Empty;
            SelectedRoom.Value = RoomKind.None;
            NavigationOffer.Value = NavigationKind.None;
            SelectedNavigation.Value = NavigationKind.None;
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
