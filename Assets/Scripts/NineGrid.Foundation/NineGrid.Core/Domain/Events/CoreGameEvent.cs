using System;

namespace NineGrid.Core
{
    [Serializable]
    public sealed class CoreGameEvent
    {
        public CoreGameEvent(CoreEventType type, int actionId, string actionName)
        {
            Type = type;
            ActionId = actionId;
            ActionName = actionName ?? string.Empty;
            FromSlot = SlotId.None;
            ToSlot = SlotId.None;
            SourceDefId = string.Empty;
            Cause = string.Empty;
            Message = string.Empty;
        }

        public long Sequence { get; private set; }
        public CoreEventType Type { get; private set; }
        public int ActionId { get; private set; }
        public string ActionName { get; private set; }
        public int ActorUid { get; private set; }
        public int TargetUid { get; private set; }
        public int CardUid { get; private set; }
        public SlotId FromSlot { get; private set; }
        public SlotId ToSlot { get; private set; }
        public int Amount { get; private set; }
        public int Delta { get; private set; }
        /// <summary>
        /// 结算后绝对值（赋值用，非增量）。血甲类事件继续用 <see cref="RemainingHp"/> /
        /// <see cref="RemainingArmor"/>；基础数值修改等用本字段。
        /// </summary>
        public int ResultValue { get; private set; }
        public int RemainingHp { get; private set; }
        public int RemainingArmor { get; private set; }
        public int RemovedAttack { get; private set; }
        public int RemovedArmor { get; private set; }
        public string SourceDefId { get; private set; }
        public string Cause { get; private set; }
        public string Message { get; private set; }

        public CoreGameEvent WithActor(int actorUid)
        {
            ActorUid = actorUid;
            return this;
        }

        public CoreGameEvent WithTarget(int targetUid)
        {
            TargetUid = targetUid;
            return this;
        }

        public CoreGameEvent WithCard(int cardUid)
        {
            CardUid = cardUid;
            return this;
        }

        public CoreGameEvent WithSlots(SlotId fromSlot, SlotId toSlot)
        {
            FromSlot = fromSlot;
            ToSlot = toSlot;
            return this;
        }

        public CoreGameEvent WithAmount(int amount)
        {
            Amount = amount;
            return this;
        }

        public CoreGameEvent WithDelta(int delta)
        {
            Delta = delta;
            return this;
        }

        public CoreGameEvent WithResultValue(int resultValue)
        {
            ResultValue = resultValue;
            return this;
        }

        public CoreGameEvent WithRemaining(int hp, int armor)
        {
            RemainingHp = hp;
            RemainingArmor = armor;
            return this;
        }

        public CoreGameEvent WithRemovedStats(int attack, int armor)
        {
            RemovedAttack = attack;
            RemovedArmor = armor;
            return this;
        }

        public CoreGameEvent WithMessage(string message)
        {
            Message = message ?? string.Empty;
            return this;
        }

        public CoreGameEvent WithSource(string sourceDefId, string cause)
        {
            SourceDefId = sourceDefId ?? string.Empty;
            Cause = cause ?? string.Empty;
            return this;
        }

        internal void AssignSequence(long sequence)
        {
            Sequence = sequence;
        }

        public override string ToString()
        {
            return "#" + Sequence + " " + Type + " action=" + ActionId + " card=" + CardUid + " target=" + TargetUid + " source=" + SourceDefId + " cause=" + Cause + " " + Message;
        }
    }
}
