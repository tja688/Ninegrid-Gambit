using System.Collections.Generic;
using NineGrid.Core.Effects;
using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Core.Tests.Simulation
{
    public sealed class HeadlessInvariantIssue
    {
        public HeadlessInvariantIssue(long sequence, string message)
        {
            Sequence = sequence;
            Message = message ?? string.Empty;
        }

        public long Sequence { get; private set; }
        public string Message { get; private set; }
    }

    public sealed class HeadlessInvariantChecker
    {
        private readonly List<HeadlessInvariantIssue> mIssues = new List<HeadlessInvariantIssue>();
        private readonly HashSet<string> mReportedLeakedEffectIds = new HashSet<string>();
        private int mCheckedEventCount;
        private int mExpectedCoins;

        public HeadlessInvariantChecker(int initialCoins)
        {
            mExpectedCoins = initialCoins;
        }

        public IReadOnlyList<HeadlessInvariantIssue> Issues
        {
            get { return mIssues; }
        }

        public int CheckedEventCount
        {
            get { return mCheckedEventCount; }
        }

        public bool HasIssues
        {
            get { return mIssues.Count > 0; }
        }

        public void CheckNewEvents(IArchitecture architecture)
        {
            var entries = architecture.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            for (var i = mCheckedEventCount; i < entries.Count; i++)
            {
                Check(entries[i]);
            }

            mCheckedEventCount = entries.Count;
            CheckRuntimeEffectLeaks(architecture, entries.Count == 0 ? 0 : entries[entries.Count - 1].Sequence);
        }

        private void Check(CoreGameEvent entry)
        {
            switch (entry.Type)
            {
                case CoreEventType.DamageDealt:
                    CheckDamage(entry);
                    break;
                case CoreEventType.HpChanged:
                case CoreEventType.ArmorChanged:
                    CheckRemaining(entry);
                    break;
                case CoreEventType.GoldModified:
                    CheckGold(entry);
                    break;
            }
        }

        private void CheckDamage(CoreGameEvent entry)
        {
            if (entry.Amount < 0)
            {
                Add(entry, "Damage amount is negative.");
            }

            if (entry.Delta < 0)
            {
                Add(entry, "Damage delta is negative.");
            }

            if (entry.Delta > entry.Amount)
            {
                Add(entry, "Damage delta exceeds damage amount.");
            }

            CheckRemaining(entry);
        }

        private void CheckRemaining(CoreGameEvent entry)
        {
            if (entry.RemainingHp < 0)
            {
                Add(entry, "Remaining HP is negative.");
            }

            if (entry.RemainingArmor < 0)
            {
                Add(entry, "Remaining armor is negative.");
            }
        }

        private void CheckGold(CoreGameEvent entry)
        {
            mExpectedCoins += entry.Delta;
            if (entry.Amount != mExpectedCoins)
            {
                Add(entry, "Gold ledger mismatch. expected=" + mExpectedCoins + " actual=" + entry.Amount);
                mExpectedCoins = entry.Amount;
            }
        }

        private void Add(CoreGameEvent entry, string message)
        {
            mIssues.Add(new HeadlessInvariantIssue(entry.Sequence, message));
        }

        private void Add(long sequence, string message)
        {
            mIssues.Add(new HeadlessInvariantIssue(sequence, message));
        }

        private void CheckRuntimeEffectLeaks(IArchitecture architecture, long sequence)
        {
            var registry = architecture.GetModel<CardRegistry>();
            var effectSystem = architecture.GetSystem<IEffectSystem>();
            var instances = effectSystem.Instances;
            for (var i = 0; i < instances.Count; i++)
            {
                var instance = instances[i];
                var ownerUid = instance.Owner == null ? 0 : instance.Owner.OwnerUid;
                if (ownerUid == 0)
                {
                    continue;
                }

                CardInstance owner;
                var ownerExists = registry.TryGet(ownerUid, out owner);
                if (ownerExists && owner.Zone.Value != ZoneId.Removed && owner.Zone.Value != ZoneId.Graveyard)
                {
                    continue;
                }

                if (mReportedLeakedEffectIds.Add(instance.InstanceId))
                {
                    Add(sequence, "Runtime effect leak. owner=" + ownerUid + " instance=" + instance.InstanceId);
                }
            }
        }
    }
}
