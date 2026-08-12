using System;
using System.Collections.Generic;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Core
{
    /// <summary>
    /// 卡面投影账本（ADR-0045）：记录每张卡各通道「最后一次经结算指令投影到卡面」的绝对值。
    /// <para>
    /// 账本只从事件日志学习（与表现层 CardFaceStatHandler 同一消费语义），因此账本值＝
    /// 「本批播完后卡面将显示的值」。对账缝据此 diff 出漏发的卡面提交事件；表现层继续
    /// 只从指令赋值、不回读内核（ADR-0005 不变量保持）。
    /// </para>
    /// <para>某通道为 null＝尚未投影过：首见静默对齐（视为已是当前值），不发事件——
    /// 覆盖存档恢复 / 测试直摆盘面等无生成事件的入场路径。</para>
    /// </summary>
    public sealed class CardFaceLedgerModel : AbstractModel
    {
        public sealed class Entry
        {
            public int? Attack;
            public int? Armor;
            public int? Hp;
            public int? ActionCount;
        }

        private readonly Dictionary<int, Entry> mEntries = new Dictionary<int, Entry>();

        /// <summary>事件日志已扫描进账本的条目数（配合 EventLog 清空一起复位）。</summary>
        public int ScannedEventCount { get; set; }

        protected override void OnInit()
        {
            Reset();
        }

        public Entry GetOrCreate(int uid)
        {
            Entry entry;
            if (!mEntries.TryGetValue(uid, out entry))
            {
                entry = new Entry();
                mEntries[uid] = entry;
            }

            return entry;
        }

        public bool TryGet(int uid, out Entry entry)
        {
            return mEntries.TryGetValue(uid, out entry);
        }

        public void Remove(int uid)
        {
            mEntries.Remove(uid);
        }

        public void Reset()
        {
            mEntries.Clear();
            ScannedEventCount = 0;
        }
    }

    /// <summary>
    /// 卡面投影统一对账缝（ADR-0045）：动作管线每结算完一个动作的自身事件后调用一次。
    /// <para>
    /// 流程：①把新事件按表现层消费语义写进账本 → ②对盘面九格 + Avatar 用结算同源
    /// oracle 重算有效卡面值（攻＝<see cref="CardFaceEventValues.GetFaceAttack"/>，Avatar 攻
    /// 另含下一击规则乘区投影；甲＝CurrentArmor；血＝基础血；行动计数＝节奏倒计时）→
    /// ③与账本 diff，有差才追加绝对值提交事件（BaseStatModified / ActionCountdownChanged，
    /// Cause=<see cref="CauseFaceReconcile"/>）。事件紧邻因果动作、沿用既有 Settled 锚点；
    /// 命中帧血甲仍由交战动作显式发 Impact 事件，本缝只补漏不抢拍。
    /// </para>
    /// <para>由此，任何新遗物 / 技能 / 原子改动数值源都无需再手工接线卡面提交——
    /// 旧 Append*FaceCommit 补扫家族已废除（ADR-0045）。</para>
    /// </summary>
    public static class CardFaceReconciliation
    {
        /// <summary>对账缝发出的提交事件统一 Cause/Message 标记（诊断定位用）。</summary>
        public const string CauseFaceReconcile = "faceReconcile";

        /// <summary>
        /// 动作自身事件入日志后调用（后续触发器 / FollowUp 各自结算时再各对账一次）。
        /// 直接向事件日志追加，不进 result.Events——卡面提交是投影，不参与规则触发。
        /// </summary>
        public static void ReconcileAfterAction(GameActionContext context, EventLog log, string actionName)
        {
            if (context == null || log == null)
            {
                return;
            }

            var ledger = context.GetModel<CardFaceLedgerModel>();
            var board = context.GetModel<BoardModel>();
            var registry = context.GetModel<CardRegistry>();
            if (ledger == null || board == null || registry == null)
            {
                return;
            }

            ScanNewEvents(ledger, log);

            var stats = context.GetSystem<IStatSystem>();
            var avatarUid = board.AvatarUid.Value;
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var uid = board.GetCardUid(SlotId.Board(i));
                CardInstance card;
                // Avatar 单独按投影攻 oracle 对账，禁止在盘面循环里按 GetFaceAttack 重复对账。
                if (uid <= 0 || uid == avatarUid || !registry.TryGet(uid, out card) || card == null)
                {
                    continue;
                }

                ReconcileCard(context, log, ledger, stats, card, actionName, isAvatar: false);
            }

            CardInstance avatar;
            if (avatarUid > 0 && registry.TryGet(avatarUid, out avatar) && avatar != null)
            {
                ReconcileCard(context, log, ledger, stats, avatar, actionName, isAvatar: true);
            }

            // 对账自身追加的提交事件语义与账本写入一致，直接跳过重扫。
            ledger.ScannedEventCount = log.Entries.Count;
        }

        /// <summary>结算与卡面共用的血量口径：基础血（DealDamage 读写同源）。</summary>
        public static int GetFaceHp(CardInstance card)
        {
            return card == null ? 0 : Math.Max(0, (int)Math.Round(card.Stats.GetBase(StatId.Hp)));
        }

        private static void ScanNewEvents(CardFaceLedgerModel ledger, EventLog log)
        {
            var entries = log.Entries;
            for (var i = ledger.ScannedEventCount; i < entries.Count; i++)
            {
                ApplyEventToLedger(ledger, entries[i]);
            }

            ledger.ScannedEventCount = entries.Count;
        }

        /// <summary>
        /// 与表现层 CardFaceStatHandler 同一消费语义：账本值＝卡面播完本批后的显示值。
        /// 改这里必须同步改 CardFaceStatHandler（及回归测试的重放模拟器）。
        /// </summary>
        private static void ApplyEventToLedger(CardFaceLedgerModel ledger, CoreGameEvent gameEvent)
        {
            if (gameEvent == null)
            {
                return;
            }

            var uid = gameEvent.CardUid > 0 ? gameEvent.CardUid : gameEvent.TargetUid;
            if (uid <= 0)
            {
                return;
            }

            switch (gameEvent.Type)
            {
                case CoreEventType.HpChanged:
                case CoreEventType.Healed:
                {
                    // 血类指令同事件顺带提交当前甲（CardFaceStatHandler.ApplyHp 同构）。
                    var entry = ledger.GetOrCreate(uid);
                    entry.Hp = Math.Max(0, gameEvent.RemainingHp);
                    entry.Armor = Math.Max(0, gameEvent.RemainingArmor);
                    break;
                }

                case CoreEventType.ArmorChanged:
                {
                    ledger.GetOrCreate(uid).Armor = Math.Max(0, gameEvent.RemainingArmor);
                    break;
                }

                case CoreEventType.CardDealt:
                case CoreEventType.CardSpawned:
                case CoreEventType.AvatarAppeared:
                {
                    // 生成类绝对值（WithFaceAbsolutes）：攻=ResultValue，血/甲=Remaining*。
                    var entry = ledger.GetOrCreate(uid);
                    entry.Attack = Math.Max(0, gameEvent.ResultValue);
                    entry.Armor = Math.Max(0, gameEvent.RemainingArmor);
                    entry.Hp = Math.Max(0, gameEvent.RemainingHp);
                    break;
                }

                case CoreEventType.CardKilled:
                case CoreEventType.CardRemoved:
                {
                    // 离场即停止追踪；再入场由生成事件重新播种。
                    ledger.Remove(uid);
                    break;
                }

                case CoreEventType.ActionCountdownChanged:
                {
                    ledger.GetOrCreate(uid).ActionCount = Math.Max(0, gameEvent.ResultValue);
                    break;
                }

                case CoreEventType.BaseStatModified:
                {
                    var entry = ledger.GetOrCreate(uid);
                    switch ((StatId)gameEvent.Amount)
                    {
                        case StatId.Attack:
                            entry.Attack = Math.Max(0, gameEvent.ResultValue);
                            break;
                        case StatId.CurrentArmor:
                            entry.Armor = Math.Max(0, gameEvent.ResultValue);
                            break;
                        case StatId.Hp:
                            entry.Hp = Math.Max(0, gameEvent.ResultValue);
                            break;
                        case StatId.MaxHp:
                            // 卡面血量槽取耦合后的当前血绝对值。
                            entry.Hp = Math.Max(0, gameEvent.RemainingHp);
                            break;

                        // StatId.Armor（基础甲）只驱动玩家 HUD 轨，不写卡面（ADR-0005）。
                    }

                    break;
                }
            }
        }

        private static void ReconcileCard(
            GameActionContext context,
            EventLog log,
            CardFaceLedgerModel ledger,
            IStatSystem stats,
            CardInstance card,
            string actionName,
            bool isAvatar)
        {
            var entry = ledger.GetOrCreate(card.Uid);

            // Avatar 攻＝下一次对怪交战伤害投影（DamageMultiplier/FlatDelta，无怪/无规则时退化为有效攻）；
            // 其余卡＝GetFaceAttack（有效攻，怪物含 EnemyAttackDelta），与伤害结算同口径。
            var attack = isAvatar
                ? CardFaceEventValues.GetProjectedPlayerBattleAttack(context, card)
                : CardFaceEventValues.GetFaceAttack(stats, card);
            ReconcileChannel(log, context, card, actionName, StatId.Attack, ref entry.Attack, attack);

            var armor = Math.Max(0, StatArmorUtility.GetCurrentArmor(card));
            ReconcileChannel(log, context, card, actionName, StatId.CurrentArmor, ref entry.Armor, armor);

            var hp = GetFaceHp(card);
            ReconcileChannel(log, context, card, actionName, StatId.Hp, ref entry.Hp, hp);

            if (CardRhythmRules.HasActiveRhythm(card))
            {
                var remaining = Math.Max(0, card.Counters.Get(CoreCounterKeys.AttackPatternCountdown));
                if (entry.ActionCount == null)
                {
                    entry.ActionCount = remaining;
                }
                else if (entry.ActionCount.Value != remaining)
                {
                    log.Append(new CoreGameEvent(CoreEventType.ActionCountdownChanged, context.ActionId, actionName)
                        .WithCard(card.Uid)
                        .WithDelta(remaining - entry.ActionCount.Value)
                        .WithResultValue(remaining)
                        .WithMessage(CauseFaceReconcile)
                        .WithSource(string.Empty, CauseFaceReconcile));
                    entry.ActionCount = remaining;
                }
            }
        }

        private static void ReconcileChannel(
            EventLog log,
            GameActionContext context,
            CardInstance card,
            string actionName,
            StatId stat,
            ref int? projected,
            int oracle)
        {
            if (projected == null)
            {
                projected = oracle;
                return;
            }

            if (projected.Value == oracle)
            {
                return;
            }

            log.Append(new CoreGameEvent(CoreEventType.BaseStatModified, context.ActionId, actionName)
                .WithCard(card.Uid)
                .WithTarget(card.Uid)
                .WithAmount((int)stat)
                .WithDelta(oracle - projected.Value)
                .WithResultValue(oracle)
                .WithMessage(CauseFaceReconcile)
                .WithSource(string.Empty, CauseFaceReconcile));
            projected = oracle;
        }
    }
}
