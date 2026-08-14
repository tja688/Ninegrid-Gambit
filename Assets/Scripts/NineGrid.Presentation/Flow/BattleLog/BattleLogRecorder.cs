using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Localization;
using NineGrid.Core.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Flow.BattleLog
{
    /// <summary>
    /// 人读战斗日志的采集端：以游标增量扫描 <c>EventLog</c>，把 Core 的原子事件
    /// 提炼成玩家能看懂的行，写入 <see cref="BattleLogStore"/>。
    ///
    /// 两条提炼规则：
    /// 1. **语义聚合**——一个效果常由多个内核原子（DealDamage / GainArmor / ModifyBaseStat…）
    ///    拼成，日志只认效果容器（<c>SourceDefId</c>）：同源事件收进一个组头下缩进列出，
    ///    而不是把原子一条条平铺。
    /// 2. **同因去重**——DealDamage 会同时发 ArmorChanged / HpChanged / DamageDealt 三条，
    ///    只留信息最全的 DamageDealt；Heal 同理只留 Healed。
    ///
    /// 只读旁路，任何异常都吞掉，不影响游戏路径。
    /// </summary>
    public sealed class BattleLogRecorder : MonoBehaviour
    {
        private static BattleLogRecorder sInstance;

        private readonly List<BattleLogEntry> mPending = new List<BattleLogEntry>(32);
        private IUnRegister mEventUnRegister;
        private bool mRetrySubscriptionPending;
        private int mCursor;

        /// <summary>当前组的来源主键；空串表示「无组，直接平铺」。</summary>
        private string mGroupSourceDefId = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            sInstance = null;
        }

        public static void EnsureInstalled()
        {
            if (sInstance == null)
            {
                var existing = FindFirstObjectByType<BattleLogRecorder>();
                if (existing != null)
                {
                    sInstance = existing;
                }
                else
                {
                    var host = new GameObject(nameof(BattleLogRecorder));
                    sInstance = host.AddComponent<BattleLogRecorder>();
                }
            }

            sInstance.EnsureSubscribed();
        }

        private void Awake()
        {
            if (sInstance != null && sInstance != this)
            {
                Destroy(gameObject);
                return;
            }

            sInstance = this;
            EnsureSubscribed();
        }

        private void Update()
        {
            if (mRetrySubscriptionPending)
            {
                EnsureSubscribed();
            }

            // 兜底追扫：未开表现批次的链路（拒收 / 纯 Core 结算）也不遗漏。
            ScanNewEntries();
        }

        private void OnDestroy()
        {
            mEventUnRegister?.UnRegister();
            mEventUnRegister = null;
            if (sInstance == this)
            {
                sInstance = null;
            }
        }

        private void EnsureSubscribed()
        {
            if (mEventUnRegister != null)
            {
                mRetrySubscriptionPending = false;
                return;
            }

            var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            if (arch == null)
            {
                mRetrySubscriptionPending = true;
                return;
            }

            mRetrySubscriptionPending = false;
            mEventUnRegister = arch.RegisterEvent<Evt_PresentationBatchOpened>(OnBatchOpened);
        }

        private void OnBatchOpened(Evt_PresentationBatchOpened e)
        {
            ScanNewEntries();
        }

        private void ScanNewEntries()
        {
            try
            {
                var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
                var pipeline = arch?.GetSystem<IActionPipelineSystem>();
                if (pipeline?.EventLog == null)
                {
                    return;
                }

                var entries = pipeline.EventLog.Entries;
                if (entries.Count < mCursor)
                {
                    // EventLog 被清空（重开一轮）：日志跟着从头再来。
                    mCursor = 0;
                    mGroupSourceDefId = string.Empty;
                    BattleLogStore.ResetAll();
                    BattleLogNaming.InvalidateCache();
                }

                if (entries.Count == mCursor)
                {
                    return;
                }

                var from = mCursor;
                var to = entries.Count;
                mCursor = to;
                Translate(entries, from, to);
            }
            catch
            {
                // 日志采集失败不阻塞主线。
            }
        }

        private void Translate(IReadOnlyList<CoreGameEvent> entries, int from, int to)
        {
            mPending.Clear();

            for (var i = from; i < to; i++)
            {
                var e = entries[i];
                if (e == null)
                {
                    continue;
                }

                if (e.Type == CoreEventType.NodeStarted)
                {
                    FlushPending();
                    mGroupSourceDefId = string.Empty;
                    BattleLogStore.BeginSection(BuildSectionTitle());
                    continue;
                }

                if (IsSuppressed(entries, i, e))
                {
                    continue;
                }

                if (e.Type == CoreEventType.EffectTriggered)
                {
                    // 效果触发一律另起一组：同一效果的多次触发不会挤在同一个头下。
                    mGroupSourceDefId = string.Empty;
                    OpenGroup(e, e.SourceDefId);
                    continue;
                }

                var line = BuildLine(e);
                if (string.IsNullOrEmpty(line.Text))
                {
                    continue;
                }

                var depth = ResolveDepth(e, line.Grouped);
                mPending.Add(new BattleLogEntry(e.Sequence, line.Kind, depth, line.Text));
            }

            FlushPending();
        }

        private void FlushPending()
        {
            if (mPending.Count == 0)
            {
                return;
            }

            BattleLogStore.AppendRange(mPending);
            mPending.Clear();
        }

        /// <summary>决定该行缩进：属于某个效果组则缩进一级，否则平铺。</summary>
        private int ResolveDepth(CoreGameEvent e, bool groupable)
        {
            if (!groupable)
            {
                mGroupSourceDefId = string.Empty;
                return 0;
            }

            var source = e.SourceDefId ?? string.Empty;
            if (source.Length == 0)
            {
                // 无来源 = 玩家普攻 / 系统结算，不归任何效果。
                mGroupSourceDefId = string.Empty;
                return 0;
            }

            if (!string.Equals(mGroupSourceDefId, source))
            {
                OpenGroup(e, source);
            }

            return 1;
        }

        private void OpenGroup(CoreGameEvent e, string sourceDefId)
        {
            var source = sourceDefId ?? string.Empty;
            if (source.Length == 0)
            {
                mGroupSourceDefId = string.Empty;
                return;
            }

            mGroupSourceDefId = source;
            var owner = e.CardUid != 0 ? e.CardUid : e.ActorUid;
            var name = BattleLogNaming.ResolveSourceName(source, owner);
            var text = BattleLogPalette.Wrap(BattleLogPalette.Source, "【" + name + "】");

            var ownerName = BattleLogNaming.ResolveCardName(owner);
            if (!string.IsNullOrEmpty(ownerName) && ownerName != name)
            {
                text += " " + BattleLogPalette.Wrap(BattleLogPalette.Muted, ownerName);
            }

            mPending.Add(new BattleLogEntry(e.Sequence, BattleLogEntryKind.EffectHeader, 0, text));
        }

        private static string BuildSectionTitle()
        {
            try
            {
                var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
                var run = arch?.GetModel<RunModel>();
                if (run == null)
                {
                    return L10n.Tr("battleLog.section.unknown", "新房间");
                }

                var floor = run.Floor != null ? run.Floor.Value : 0;
                var room = run.Room != null ? run.Room.Value : RoomKind.None;
                return BattleLogNaming.ResolveRoomTitle(floor, room);
            }
            catch
            {
                return L10n.Tr("battleLog.section.unknown", "新房间");
            }
        }

        // ==================== 同因去重 ====================

        /// <summary>
        /// DealDamage 一次结算发 ArmorChanged + HpChanged + DamageDealt，Heal 发 Healed + HpChanged。
        /// 同一 ActionId 内若已有信息更全的那条，附属条目一律不落日志。
        /// </summary>
        private static bool IsSuppressed(IReadOnlyList<CoreGameEvent> entries, int index, CoreGameEvent e)
        {
            switch (e.Type)
            {
                case CoreEventType.ArmorChanged:
                    // 伤害吃甲：DamageDealt 里已带破甲拆分。
                    return e.Delta < 0 && HasSiblingInAction(entries, index, e, CoreEventType.DamageDealt);

                case CoreEventType.HpChanged:
                    // 扣血归 DamageDealt、回血归 Healed。
                    return HasSiblingInAction(entries, index, e, CoreEventType.DamageDealt)
                        || HasSiblingInAction(entries, index, e, CoreEventType.Healed);

                default:
                    return false;
            }
        }

        private static bool HasSiblingInAction(IReadOnlyList<CoreGameEvent> entries, int index, CoreGameEvent e, CoreEventType sibling)
        {
            var actionId = e.ActionId;
            var uid = ResolveSubjectUid(e);

            // 同一动作的事件必然相邻成簇，向后找到动作边界即可。
            for (var i = index + 1; i < entries.Count; i++)
            {
                var other = entries[i];
                if (other == null || other.ActionId != actionId)
                {
                    break;
                }

                if (other.Type == sibling && ResolveSubjectUid(other) == uid)
                {
                    return true;
                }
            }

            for (var i = index - 1; i >= 0; i--)
            {
                var other = entries[i];
                if (other == null || other.ActionId != actionId)
                {
                    break;
                }

                if (other.Type == sibling && ResolveSubjectUid(other) == uid)
                {
                    return true;
                }
            }

            return false;
        }

        private static int ResolveSubjectUid(CoreGameEvent e)
        {
            return e.TargetUid != 0 ? e.TargetUid : e.CardUid;
        }

        // ==================== 行文案 ====================

        private struct Line
        {
            public string Text;
            public BattleLogEntryKind Kind;
            public bool Grouped;
        }

        private static Line BuildLine(CoreGameEvent e)
        {
            switch (e.Type)
            {
                case CoreEventType.DamageDealt:
                    return BuildDamage(e);
                case CoreEventType.Healed:
                    return BuildHeal(e);
                case CoreEventType.HpChanged:
                    return BuildHpChanged(e);
                case CoreEventType.ArmorChanged:
                    return BuildArmor(e);
                case CoreEventType.GoldModified:
                    return BuildGold(e);
                case CoreEventType.BaseStatModified:
                    return BuildBaseStat(e);
                case CoreEventType.CardKilled:
                    return BuildKill(e);
                case CoreEventType.RelicGranted:
                    return BuildRelic(e);
                case CoreEventType.ItemUsed:
                    return BuildItemUsed(e);
                default:
                    return default;
            }
        }

        private static Line BuildDamage(CoreGameEvent e)
        {
            var dealt = e.Delta;
            if (dealt <= 0 && e.Amount <= 0)
            {
                return default;
            }

            var target = BattleLogNaming.ResolveCardName(ResolveSubjectUid(e));
            var head = Subject(e.ActorUid, target);
            var body = head + " " + BattleLogPalette.Wrap(BattleLogPalette.Damage, "-" + dealt);

            var detail = new List<string>(3);
            if (e.ArmorDamage > 0)
            {
                detail.Add("甲 " + e.ArmorDamage);
            }

            if (e.HpDamage > 0)
            {
                detail.Add("血 " + e.HpDamage);
            }

            if (e.Amount > dealt)
            {
                // 减伤 / 免疫吃掉的部分，排伤害数对不对时最关键的一列。
                detail.Add("原始 " + e.Amount);
            }

            detail.Add("余 " + e.RemainingHp + "/" + e.RemainingArmor);
            body += " " + BattleLogPalette.Wrap(BattleLogPalette.Muted, "(" + string.Join(" · ", detail) + ")");

            return new Line { Text = body, Kind = BattleLogEntryKind.Damage, Grouped = true };
        }

        private static Line BuildHeal(CoreGameEvent e)
        {
            if (e.Delta <= 0)
            {
                return default;
            }

            var target = BattleLogNaming.ResolveCardName(ResolveSubjectUid(e));
            var body = target
                + " " + BattleLogPalette.Wrap(BattleLogPalette.Heal, "+" + e.Delta)
                + " " + BattleLogPalette.Wrap(BattleLogPalette.Muted, "(余 " + e.RemainingHp + ")");
            return new Line { Text = body, Kind = BattleLogEntryKind.Heal, Grouped = true };
        }

        private static Line BuildHpChanged(CoreGameEvent e)
        {
            // 走到这里的都是无伤害/治疗配对的直改血（如结算钳制、内容动作）。
            if (e.Delta == 0)
            {
                return default;
            }

            var target = BattleLogNaming.ResolveCardName(ResolveSubjectUid(e));
            var positive = e.Delta > 0;
            var color = positive ? BattleLogPalette.Heal : BattleLogPalette.Damage;
            var body = target
                + " " + BattleLogPalette.Wrap(color, (positive ? "+" : string.Empty) + e.Delta)
                + " " + BattleLogPalette.Wrap(BattleLogPalette.Muted, "(余 " + e.RemainingHp + ")");
            return new Line
            {
                Text = body,
                Kind = positive ? BattleLogEntryKind.Heal : BattleLogEntryKind.Damage,
                Grouped = true
            };
        }

        private static Line BuildArmor(CoreGameEvent e)
        {
            if (e.Delta == 0 || IsNoiseCause(e.Message))
            {
                return default;
            }

            var target = BattleLogNaming.ResolveCardName(ResolveSubjectUid(e));
            var positive = e.Delta > 0;
            var body = target
                + " " + BattleLogPalette.Wrap(BattleLogPalette.Armor, (positive ? "+" : string.Empty) + e.Delta + " 甲")
                + " " + BattleLogPalette.Wrap(BattleLogPalette.Muted, "(共 " + e.RemainingArmor + ")");
            return new Line { Text = body, Kind = BattleLogEntryKind.Armor, Grouped = true };
        }

        private static Line BuildGold(CoreGameEvent e)
        {
            if (e.Delta == 0)
            {
                return default;
            }

            var positive = e.Delta > 0;
            var body = BattleLogPalette.Wrap(BattleLogPalette.Gold, (positive ? "+" : string.Empty) + e.Delta + " 金币")
                + " " + BattleLogPalette.Wrap(BattleLogPalette.Muted, "(共 " + e.Amount + ")");
            return new Line { Text = body, Kind = BattleLogEntryKind.Gold, Grouped = true };
        }

        private static Line BuildBaseStat(CoreGameEvent e)
        {
            if (e.Delta == 0 || IsNoiseCause(e.Message))
            {
                return default;
            }

            var statName = ResolveStatName((StatId)e.Amount);
            if (string.IsNullOrEmpty(statName))
            {
                return default;
            }

            var target = BattleLogNaming.ResolveCardName(ResolveSubjectUid(e));
            var positive = e.Delta > 0;
            var color = (StatId)e.Amount == StatId.Attack ? BattleLogPalette.Attack : BattleLogPalette.BaseStat;
            var body = target + " " + statName
                + " " + BattleLogPalette.Wrap(color, (positive ? "+" : string.Empty) + e.Delta)
                + " " + BattleLogPalette.Wrap(BattleLogPalette.Muted, "(→ " + e.ResultValue + ")");
            return new Line { Text = body, Kind = BattleLogEntryKind.BaseStat, Grouped = true };
        }

        private static Line BuildKill(CoreGameEvent e)
        {
            var target = BattleLogNaming.ResolveCardName(ResolveSubjectUid(e));
            if (string.IsNullOrEmpty(target))
            {
                return default;
            }

            var body = BattleLogPalette.Wrap(BattleLogPalette.Kill, "击杀") + " " + target;
            return new Line { Text = body, Kind = BattleLogEntryKind.Kill, Grouped = true };
        }

        private static Line BuildRelic(CoreGameEvent e)
        {
            var defId = string.IsNullOrEmpty(e.Message) ? e.SourceDefId : e.Message;
            if (string.IsNullOrEmpty(defId))
            {
                return default;
            }

            var body = BattleLogPalette.Wrap(BattleLogPalette.Relic, "获得遗物")
                + " " + BattleLogNaming.ResolveContentName(defId);
            return new Line { Text = body, Kind = BattleLogEntryKind.Relic, Grouped = false };
        }

        private static Line BuildItemUsed(CoreGameEvent e)
        {
            var defId = string.IsNullOrEmpty(e.SourceDefId) ? string.Empty : e.SourceDefId;
            var name = string.IsNullOrEmpty(defId)
                ? BattleLogNaming.ResolveCardName(e.CardUid)
                : BattleLogNaming.ResolveContentName(defId);
            if (string.IsNullOrEmpty(name))
            {
                return default;
            }

            var body = BattleLogPalette.Wrap(BattleLogPalette.Muted, "使用")
                + " " + BattleLogPalette.Wrap(BattleLogPalette.Source, "【" + name + "】");
            return new Line { Text = body, Kind = BattleLogEntryKind.Note, Grouped = false };
        }

        private static string Subject(int actorUid, string targetName)
        {
            if (actorUid <= 0)
            {
                return targetName;
            }

            var actorName = BattleLogNaming.ResolveCardName(actorUid);
            if (string.IsNullOrEmpty(actorName) || actorName == targetName)
            {
                return targetName;
            }

            return actorName + " → " + targetName;
        }

        private static string ResolveStatName(StatId stat)
        {
            switch (stat)
            {
                case StatId.Attack:
                    return L10n.Tr("battleLog.stat.attack", "攻击");
                case StatId.MaxHp:
                    return L10n.Tr("battleLog.stat.maxHp", "生命上限");
                case StatId.Hp:
                    return L10n.Tr("battleLog.stat.hp", "生命");
                case StatId.Armor:
                    return L10n.Tr("battleLog.stat.armor", "护甲上限");
                case StatId.CurrentArmor:
                    return L10n.Tr("battleLog.stat.currentArmor", "护甲");
                case StatId.Recovery:
                    return L10n.Tr("battleLog.stat.recovery", "回复");
                case StatId.InteractionRange:
                    return L10n.Tr("battleLog.stat.range", "互动范围");
                default:
                    return string.Empty;
            }
        }

        /// <summary>对账校正、节点重置这类内部动作不是玩家看的信息。</summary>
        private static bool IsNoiseCause(string cause)
        {
            if (string.IsNullOrEmpty(cause))
            {
                return false;
            }

            return cause == "faceReconcile" || cause == "resetCurrentArmor";
        }
    }
}
