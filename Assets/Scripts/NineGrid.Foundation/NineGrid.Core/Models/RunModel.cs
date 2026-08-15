using System.Collections.Generic;
using NineGrid.Core.Content;
using QFramework;

namespace NineGrid.Core
{
    public sealed class RunModel : AbstractModel
    {
        public const int NodesPerFloor = 8;
        public const int FinalFloor = 3;

        private readonly List<string> mUsedMonsterDeckIds = new List<string>();
        private readonly List<string> mAttributePickDefIds = new List<string>();

        public BindableProperty<int> Floor { get; private set; }
        public BindableProperty<int> NodeIndex { get; private set; }
        public BindableProperty<ulong> Seed { get; private set; }
        public BindableProperty<RoomKind> Room { get; private set; }
        public BindableProperty<GamePhase> Phase { get; private set; }
        public BindableProperty<int> Version { get; private set; }
        /// <summary>本层绑定的主题怪物卡组 Id（ADR-0022）；空表示尚未抽取。</summary>
        public BindableProperty<string> FloorMonsterDeckId { get; private set; }
        /// <summary>选人界面难度档 id（normal / advanced / hard）；影响怪物数值与环境。</summary>
        public BindableProperty<string> DifficultyId { get; private set; }

        public IReadOnlyList<string> UsedMonsterDeckIds
        {
            get { return mUsedMonsterDeckIds; }
        }

        /// <summary>
        /// 属性房三选二（#136）：本属性房战斗开局注入的选择结果（≤2 张）。
        /// 会话提交后写入，BuildNodeDeckOptions 消费后清空；新 Run 重置。
        /// </summary>
        public IReadOnlyList<string> AttributePickDefIds
        {
            get { return mAttributePickDefIds; }
        }

        public void SetAttributePicks(IReadOnlyList<string> defIds)
        {
            mAttributePickDefIds.Clear();
            if (defIds != null)
            {
                for (var i = 0; i < defIds.Count; i++)
                {
                    if (!string.IsNullOrEmpty(defIds[i]))
                    {
                        mAttributePickDefIds.Add(defIds[i]);
                    }
                }
            }

            Touch();
        }

        public void ClearAttributePicks()
        {
            if (mAttributePickDefIds.Count == 0)
            {
                return;
            }

            mAttributePickDefIds.Clear();
            Touch();
        }

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
                FloorMonsterDeckId = new BindableProperty<string>(string.Empty);
                DifficultyId = new BindableProperty<string>(RunDifficultyIds.Normal);
            }
        }

        public void SetDifficultyId(string difficultyId)
        {
            var normalized = string.IsNullOrEmpty(difficultyId)
                ? RunDifficultyIds.Normal
                : difficultyId;
            if (string.Equals(DifficultyId.Value, normalized, System.StringComparison.Ordinal))
            {
                return;
            }

            DifficultyId.Value = normalized;
            Touch();
        }

        public void Reset(ulong seed)
        {
            Floor.Value = 1;
            NodeIndex.Value = 0;
            Seed.Value = seed;
            Room.Value = RoomKind.None;
            Phase.Value = GamePhase.BuildEnemyPool;
            FloorMonsterDeckId.Value = string.Empty;
            DifficultyId.Value = RunDifficultyIds.Normal;
            mUsedMonsterDeckIds.Clear();
            mAttributePickDefIds.Clear();
            Touch();
        }

        public void SetPhase(GamePhase phase)
        {
            Phase.Value = phase;
            Touch();
        }

        /// <summary>绑定本层主题卡组；同层重复调用保留首次结果。</summary>
        public bool TryBindFloorMonsterDeck(string deckId)
        {
            if (string.IsNullOrEmpty(deckId))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(FloorMonsterDeckId.Value))
            {
                return string.Equals(FloorMonsterDeckId.Value, deckId, System.StringComparison.Ordinal);
            }

            FloorMonsterDeckId.Value = deckId;
            if (!mUsedMonsterDeckIds.Contains(deckId))
            {
                mUsedMonsterDeckIds.Add(deckId);
            }

            Touch();
            return true;
        }

        public bool IsMonsterDeckUsed(string deckId)
        {
            if (string.IsNullOrEmpty(deckId))
            {
                return false;
            }

            for (var i = 0; i < mUsedMonsterDeckIds.Count; i++)
            {
                if (string.Equals(mUsedMonsterDeckIds[i], deckId, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 存档恢复（RunSaveGame）：整体覆写跑图进度与已用主题卡组。
        /// 只供读档路径调用；相位由调用方另行 SetPhase。
        /// </summary>
        public void RestoreProgress(
            int floor,
            int nodeIndex,
            ulong seed,
            RoomKind room,
            string floorMonsterDeckId,
            IReadOnlyList<string> usedMonsterDeckIds,
            string difficultyId = null)
        {
            Floor.Value = floor < 1 ? 1 : floor;
            NodeIndex.Value = nodeIndex < 0 ? 0 : nodeIndex;
            Seed.Value = seed;
            Room.Value = room;
            FloorMonsterDeckId.Value = floorMonsterDeckId ?? string.Empty;
            SetDifficultyId(difficultyId);
            mUsedMonsterDeckIds.Clear();
            if (usedMonsterDeckIds != null)
            {
                for (var i = 0; i < usedMonsterDeckIds.Count; i++)
                {
                    var deckId = usedMonsterDeckIds[i];
                    if (!string.IsNullOrEmpty(deckId) && !mUsedMonsterDeckIds.Contains(deckId))
                    {
                        mUsedMonsterDeckIds.Add(deckId);
                    }
                }
            }

            Touch();
        }

        public bool AdvanceNode()
        {
            var nextNodeIndex = NodeIndex.Value + 1;
            if (nextNodeIndex >= NodesPerFloor)
            {
                if (Floor.Value >= FinalFloor)
                {
                    NodeIndex.Value = NodesPerFloor;
                    Touch();
                    return true;
                }

                Floor.Value++;
                NodeIndex.Value = 0;
                FloorMonsterDeckId.Value = string.Empty;
                Touch();
                return false;
            }

            NodeIndex.Value = nextNodeIndex;
            Touch();
            return false;
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
