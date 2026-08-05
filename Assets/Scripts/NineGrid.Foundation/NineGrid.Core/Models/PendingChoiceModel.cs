using System.Collections.Generic;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Core
{
    public sealed class PendingChoiceModel : AbstractModel
    {
        private readonly List<RewardEntry> mRewardOptions = new List<RewardEntry>();
        private readonly List<RoomKind> mRoomOptions = new List<RoomKind>();
        private readonly List<string> mAttributeSelectedDefIds = new List<string>();

        public BindableProperty<PendingChoiceKind> Kind { get; private set; }
        public BindableProperty<string> PoolId { get; private set; }
        public BindableProperty<RoomKind> SelectedRoom { get; private set; }
        public BindableProperty<NavigationKind> NavigationOffer { get; private set; }
        public BindableProperty<NavigationKind> SelectedNavigation { get; private set; }
        public BindableProperty<int> Version { get; private set; }

        /// <summary>商店刷新价（金币）；仅本次进店有效，离开后清零。</summary>
        public BindableProperty<int> ShopRefreshPriceGold { get; private set; }

        public IReadOnlyList<RewardEntry> RewardOptions
        {
            get { return mRewardOptions; }
        }

        public IReadOnlyList<RoomKind> RoomOptions
        {
            get { return mRoomOptions; }
        }

        /// <summary>属性房三选二（#136）：本会话已选 defId（选满后提交 RunModel，未满可放弃）。</summary>
        public IReadOnlyList<string> AttributeSelectedDefIds
        {
            get { return mAttributeSelectedDefIds; }
        }

        public int AttributeSelectedCount
        {
            get { return mAttributeSelectedDefIds.Count; }
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

            if (ShopRefreshPriceGold == null)
            {
                ShopRefreshPriceGold = new BindableProperty<int>(0);
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
            if (!KeepsVisitRefreshPrice(poolId))
            {
                ShopRefreshPriceGold.Value = 0;
            }

            Touch();
        }

        /// <summary>商店货架会话：4 货架 + 本次进店刷新价。</summary>
        public void OfferShop(IReadOnlyList<RewardEntry> shelves, int refreshPriceGold)
        {
            OfferRewards(ShopPoolId, shelves);
            ShopRefreshPriceGold.Value = refreshPriceGold < 0 ? 0 : refreshPriceGold;
            Touch();
        }

        /// <summary>卡店服务会话：3 服务选项 + 本次进店刷新价（#93）。</summary>
        public void OfferTavern(IReadOnlyList<RewardEntry> services, int refreshPriceGold)
        {
            OfferRewards(TavernPoolId, services);
            ShopRefreshPriceGold.Value = refreshPriceGold < 0 ? 0 : refreshPriceGold;
            Touch();
        }

        /// <summary>卡店「道具卡固定」二级选择：候选来自道具卡来源池；保留本次进店刷新价。</summary>
        public void OfferTavernFixItem(IReadOnlyList<RewardEntry> candidates)
        {
            var refresh = ShopRefreshPriceGold.Value;
            OfferRewards(TavernFixItemPoolId, candidates);
            ShopRefreshPriceGold.Value = refresh;
            Touch();
        }

        /// <summary>属性房三选二会话（#136）：3 个加权候选；候选实例选中后移除，不可重复点同实例。</summary>
        public void OfferAttributePick(IReadOnlyList<RewardEntry> candidates)
        {
            OfferRewards(AttributePickPoolId, candidates);
            mAttributeSelectedDefIds.Clear();
            Kind.Value = PendingChoiceKind.AttributePick;
            Touch();
        }

        /// <summary>记录一次候选选择（候选实例由 RemoveRewardOptionAt 去重；同种 defId 可重复选）。</summary>
        public void AddAttributeSelection(string defId)
        {
            if (Kind.Value != PendingChoiceKind.AttributePick
                || string.IsNullOrEmpty(defId)
                || mAttributeSelectedDefIds.Count >= RewardSystem.AttributePickCount)
            {
                return;
            }

            mAttributeSelectedDefIds.Add(defId);
            Touch();
        }

        public bool RemoveRewardOptionAt(int index)
        {
            if (index < 0 || index >= mRewardOptions.Count)
            {
                return false;
            }

            mRewardOptions.RemoveAt(index);
            Touch();
            return true;
        }

        public void ClearRewardChoices()
        {
            mRewardOptions.Clear();
            if (Kind.Value == PendingChoiceKind.Reward
                || Kind.Value == PendingChoiceKind.AttributePick)
            {
                Kind.Value = PendingChoiceKind.None;
            }

            mAttributeSelectedDefIds.Clear();
            PoolId.Value = string.Empty;
            ShopRefreshPriceGold.Value = 0;
            Touch();
        }

        public const string ShopPoolId = "shop.helpCards";
        public const string TavernPoolId = "tavern.services";
        public const string TavernFixItemPoolId = "tavern.fixItem";
        public const string TreasureRewardPoolId = "reward.treasure";
        public const string ItemRewardPoolId = "reward.item";
        /// <summary>属性房三选二会话（#136）。</summary>
        public const string AttributePickPoolId = "attribute.pick";

        public static bool IsShopPool(string poolId)
        {
            return string.Equals(poolId, ShopPoolId, System.StringComparison.Ordinal);
        }

        public static bool IsTavernPool(string poolId)
        {
            return string.Equals(poolId, TavernPoolId, System.StringComparison.Ordinal);
        }

        public static bool IsTavernFixItemPool(string poolId)
        {
            return string.Equals(poolId, TavernFixItemPoolId, System.StringComparison.Ordinal);
        }

        public static bool IsTreasureRewardPool(string poolId)
        {
            return string.Equals(poolId, TreasureRewardPoolId, System.StringComparison.Ordinal);
        }

        public static bool IsItemRewardPool(string poolId)
        {
            return string.Equals(poolId, ItemRewardPoolId, System.StringComparison.Ordinal);
        }

        /// <summary>属性房三选二会话（#136）：只认本池，不属于商店/卡店/特殊房。</summary>
        public static bool IsAttributePickPool(string poolId)
        {
            return string.Equals(poolId, AttributePickPoolId, System.StringComparison.Ordinal);
        }

        /// <summary>特殊奖励房（#94）：免费货架，拿后留房。</summary>
        public static bool IsSpecialRewardPool(string poolId)
        {
            return IsTreasureRewardPool(poolId) || IsItemRewardPool(poolId);
        }

        /// <summary>商店 / 卡店主面：可刷新货架或服务。</summary>
        public static bool IsConsumerRefreshPool(string poolId)
        {
            return IsShopPool(poolId) || IsTavernPool(poolId);
        }

        /// <summary>商店 / 卡店 / 特殊奖励房离开（不发跳过帮助卡金币）。</summary>
        public static bool IsConsumerLeavePool(string poolId)
        {
            return IsShopPool(poolId) || IsTavernPool(poolId) || IsSpecialRewardPool(poolId);
        }

        /// <summary>消费/特殊房场地板（含卡店二级选择）：允许 BoardWalk。</summary>
        public static bool IsConsumerBoardPool(string poolId)
        {
            return IsShopPool(poolId)
                   || IsTavernPool(poolId)
                   || IsTavernFixItemPool(poolId)
                   || IsSpecialRewardPool(poolId);
        }

        private static bool KeepsVisitRefreshPrice(string poolId)
        {
            return IsShopPool(poolId) || IsTavernPool(poolId) || IsTavernFixItemPool(poolId);
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
            ShopRefreshPriceGold.Value = 0;
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
            ShopRefreshPriceGold.Value = 0;
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
            mAttributeSelectedDefIds.Clear();
            Kind.Value = PendingChoiceKind.None;
            PoolId.Value = string.Empty;
            SelectedRoom.Value = RoomKind.None;
            NavigationOffer.Value = NavigationKind.None;
            SelectedNavigation.Value = NavigationKind.None;
            ShopRefreshPriceGold.Value = 0;
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
