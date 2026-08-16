using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Flow.InRoomBoard;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Flow.RoomIcons
{
    /// <summary>
    /// 场地图标悬停「特色产物」预览（房间.md / ADR-0020 补充）：hover 房间图标时，
    /// 在场地空格位弹出该房开局注入的候选真卡——属性房 3 张属性道具卡、金币/宝箱/恢复房固定卡、
    /// 困难房 2 张怪物卡（序列 3/4 代表）、层主房 Boss 卡。
    /// 落格复用牌店二级候选的 <see cref="InRoomOfferSlotPlanner"/> 动态避让（避开 Avatar、全部图标格，
    /// 并保留 Avatar→被悬停图标的空路）；入场走 <see cref="InRoomShelfAnimation.DropInToSlotAsync"/>。
    /// 纯表现投影：不写 BoardModel、不注册 RoomIconOccupancy、不接点击。
    /// </summary>
    public sealed class RoomIconHoverPreviewPresenter
    {
        public static RoomIconHoverPreviewPresenter Current { get; private set; } = new RoomIconHoverPreviewPresenter();

        public const int DefaultAvatarSlot = 5;

        /// <summary>预览卡偏好落格（与牌店候选同源 #93）。</summary>
        public static readonly int[] PreferredSlots = { 1, 3, 4, 6, 7, 9 };

        private readonly List<ManagedCard> mPreviewCards = new List<ManagedCard>(4);
        private readonly List<int> mPreviewSlots = new List<int>(4);
        private CancellationTokenSource mAnimCts;
        private IArchitecture mArch;

        /// <summary>当前预览真卡（表现权威只读投影，供房内装饰层聚合）。</summary>
        public IReadOnlyList<ManagedCard> PreviewCards => mPreviewCards;

        /// <summary>当前预览卡落格（与 <see cref="PreviewCards"/> 同序）。</summary>
        public IReadOnlyList<int> PreviewSlots => mPreviewSlots;

        public bool IsShowing => mPreviewCards.Count > 0;

        public static void ResetForTests()
        {
            Current?.Hide();
            Current = new RoomIconHoverPreviewPresenter();
        }

        public void Bind(IArchitecture architecture)
        {
            mArch = architecture;
        }

        /// <summary>
        /// 悬停房间图标：重建预览。非房间 / 无注入产物（商店、卡店、奖励房）静默清空。
        /// 重复进入会先撤旧再建新（与悬停文案同频）。
        /// </summary>
        public void Show(string contentId, IArchitecture arch, int hoveredSlot)
        {
            Bind(arch);
            Hide();
            if (arch == null || string.IsNullOrWhiteSpace(contentId))
            {
                return;
            }

            if (!Enum.TryParse(contentId, ignoreCase: true, out RoomKind room) || room == RoomKind.None)
            {
                return;
            }

            var catalog = arch.GetSystem<IContentSystem>()?.Catalog;
            RoomDefinition def;
            if (catalog == null || !catalog.Rewards.TryGetRoom(room, out def) || def == null)
            {
                return;
            }

            var defIds = ResolvePreviewDefIds(def, arch);
            if (defIds.Count == 0)
            {
                return;
            }

            var geometry = arch.GetSystem<IGroundFieldGeometrySystem>();
            var cards = CardEntityLifecycleHook.CardsOrNull()
                        ?? UnityEngine.Object.FindFirstObjectByType<CardManagerSingleton>();
            if (geometry == null || cards == null)
            {
                return;
            }

            var slots = PlanSlots(arch, hoveredSlot, defIds.Count);
            if (slots == null)
            {
                return;
            }

            mAnimCts?.Cancel();
            mAnimCts?.Dispose();
            mAnimCts = new CancellationTokenSource();
            var ct = mAnimCts.Token;
            var animTasks = new List<UniTask>(defIds.Count);

            for (var i = 0; i < defIds.Count; i++)
            {
                var slot = slots[i];
                if (slot <= 0)
                {
                    continue;
                }

                var defId = defIds[i];
                var kind = CoreCardPresentationMapper.ResolvePresentationKindFromDefId(defId);
                if (kind == CardPresentationKind.Unknown)
                {
                    kind = CardPresentationKind.HelpCard;
                }

                var managed = cards.SpawnPresentationOnly(
                    defId,
                    parent: null,
                    CardDisplayMode.GroundCardMode,
                    kind);
                if (managed?.View == null)
                {
                    continue;
                }

                // 预览不接点击：关掉卡面自带地面命中，避免抢占格位认领/误触发购买。
                var groundHit = managed.View.gameObject.GetComponent<GroundCardHitProxy>();
                if (groundHit != null)
                {
                    groundHit.enabled = false;
                }

                managed.View.transform.rotation = Quaternion.identity;
                CoreCardPresentationMapper.ApplyVisualsByDefId(managed, kind);
                mPreviewCards.Add(managed);
                mPreviewSlots.Add(slot);
                animTasks.Add(InRoomShelfAnimation.DropInToSlotAsync(managed, geometry, slot, ct));
            }

            if (animTasks.Count > 0)
            {
                UniTask.WhenAll(animTasks).Forget();
            }
        }

        /// <summary>撤预览：释放全部预览卡并取消入场动画。</summary>
        public void Hide()
        {
            mAnimCts?.Cancel();
            mAnimCts?.Dispose();
            mAnimCts = null;

            var cards = CardEntityLifecycleHook.CardsOrNull()
                        ?? UnityEngine.Object.FindFirstObjectByType<CardManagerSingleton>();
            for (var i = 0; i < mPreviewCards.Count; i++)
            {
                var card = mPreviewCards[i];
                if (card == null)
                {
                    continue;
                }

                cards?.Release(card, "RoomIconHoverPreview.Hide");
            }

            mPreviewCards.Clear();
            mPreviewSlots.Clear();
        }

        /// <summary>
        /// 解析房间「特色产物」预览 defId（去重保序）：
        /// 玩家侧 FixedCard / WeightedPool 全量候选；困难房只取两张怪物卡（序列 3/4 各一张代表）；
        /// 层主房取当前楼层怪物卡组序列 5（层主）。真实抽取在进战后由 RewardSystem 随机决定。
        /// </summary>
        public static List<string> ResolvePreviewDefIds(RoomDefinition room, IArchitecture arch)
        {
            var result = new List<string>();
            if (room == null)
            {
                return result;
            }

            if (room.Kind == RoomKind.Boss)
            {
                AddFirstSequenceMonster(result, arch, 5);
                return result;
            }

            if (room.Kind == RoomKind.Elite)
            {
                // 困难房：表现侧仅展示待加入的两张怪物卡。
                AddFirstSequenceMonster(result, arch, 3);
                AddFirstSequenceMonster(result, arch, 4);
                return result;
            }

            var injects = room.OpeningInjects;
            for (var i = 0; i < injects.Count; i++)
            {
                var inject = injects[i];
                if (inject == null || inject.Side != RoomInjectSide.Player)
                {
                    continue;
                }

                if (inject.SourceKind == RoomInjectSourceKind.FixedCard)
                {
                    AddUnique(result, inject.CardDefId);
                    continue;
                }

                if (inject.SourceKind != RoomInjectSourceKind.WeightedPool)
                {
                    continue;
                }

                var pool = inject.Pool;
                for (var j = 0; j < pool.Count; j++)
                {
                    var option = pool[j];
                    if (option != null)
                    {
                        AddUnique(result, option.CardDefId);
                    }
                }
            }

            return result;
        }

        /// <summary>当前楼层怪物卡组中指定序列的第一张合法候选（与 RewardSystem 同口径过滤）。</summary>
        private static void AddFirstSequenceMonster(List<string> into, IArchitecture arch, int sequence)
        {
            var run = arch?.GetModel<RunModel>();
            var catalog = arch?.GetSystem<IContentSystem>()?.Catalog;
            if (run == null || catalog == null || string.IsNullOrEmpty(run.FloorMonsterDeckId.Value))
            {
                return;
            }

            if (!catalog.MonsterDecks.TryGetValue(run.FloorMonsterDeckId.Value, out var deck) || deck == null)
            {
                return;
            }

            for (var i = 0; i < deck.MonsterDefIds.Count; i++)
            {
                if (!catalog.TryGetCard(deck.MonsterDefIds[i], out var card) || card == null)
                {
                    continue;
                }

                if (!CardCombatRules.IsBoardCombatTarget(card.Kind) || card.IsReserve)
                {
                    continue;
                }

                if (card.Sequence != sequence)
                {
                    continue;
                }

                AddUnique(into, card.DefId);
                return;
            }
        }

        private static void AddUnique(List<string> into, string defId)
        {
            if (string.IsNullOrEmpty(defId))
            {
                return;
            }

            for (var i = 0; i < into.Count; i++)
            {
                if (string.Equals(into[i], defId, StringComparison.Ordinal))
                {
                    return;
                }
            }

            into.Add(defId);
        }

        /// <summary>
        /// 预览落格：偏好格 + 全格兜底，避开 Avatar 与全部场地图标格；leaveSlot 取被悬停图标格，
        /// 保证 Avatar→该图标仍有空路（复用牌店候选动态避让，ADR-0020）。
        /// </summary>
        private int[] PlanSlots(IArchitecture arch, int hoveredSlot, int count)
        {
            var board = arch.GetModel<BoardModel>();
            var avatarSlot = DefaultAvatarSlot;
            if (board != null && board.AvatarSlot.Value.IsBoardSlot)
            {
                avatarSlot = board.AvatarSlot.Value.Index;
            }

            var excluded = new HashSet<int>(RoomIconOccupancy.Current.BySlot.Keys);
            excluded.Add(avatarSlot);
            if (hoveredSlot >= SlotId.MinBoardIndex && hoveredSlot <= SlotId.MaxBoardIndex)
            {
                excluded.Add(hoveredSlot);
            }

            var preferred = new List<int>(count);
            for (var i = 0; i < count; i++)
            {
                preferred.Add(i < PreferredSlots.Length ? PreferredSlots[i] : 0);
            }

            return InRoomOfferSlotPlanner.Plan(
                board,
                count,
                avatarSlot,
                preferred,
                PreferredSlots,
                leaveSlot: hoveredSlot >= SlotId.MinBoardIndex && hoveredSlot <= SlotId.MaxBoardIndex
                    ? hoveredSlot
                    : InRoomOfferSlotPlanner.DefaultLeaveSlot,
                reserveRefresh: false,
                excludedSlots: excluded);
        }
    }
}
