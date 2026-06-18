using System.Collections.Generic;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using QFramework;

namespace NineGrid.Core.Effects
{
    public sealed class EffectRuntimeContext
    {
        public EffectRuntimeContext(IArchitecture architecture, EffectInstance instance, TriggerContext triggerContext)
        {
            Architecture = architecture;
            Instance = instance;
            TriggerContext = triggerContext;
        }

        public IArchitecture Architecture { get; private set; }
        public EffectInstance Instance { get; private set; }
        public TriggerContext TriggerContext { get; private set; }

        public GameActionContext ActionContext
        {
            get { return TriggerContext == null ? null : TriggerContext.ActionContext; }
        }

        public IReadOnlyList<CoreGameEvent> Events
        {
            get { return TriggerContext == null || TriggerContext.Events == null ? EmptyEvents : TriggerContext.Events; }
        }

        public CardRegistry Registry
        {
            get { return Architecture.GetModel<CardRegistry>(); }
        }

        public BoardModel Board
        {
            get { return Architecture.GetModel<BoardModel>(); }
        }

        public DeckModel Deck
        {
            get { return Architecture.GetModel<DeckModel>(); }
        }

        public PlayerModel Player
        {
            get { return Architecture.GetModel<PlayerModel>(); }
        }

        public IRngUtility Rng
        {
            get { return Architecture.GetUtility<IRngUtility>(); }
        }

        public int OwnerUid
        {
            get { return Instance == null || Instance.Owner == null ? 0 : Instance.Owner.OwnerUid; }
        }

        public string SourceDefId
        {
            get { return Instance == null || Instance.Owner == null ? string.Empty : Instance.Owner.SourceDefId; }
        }

        public string EffectId
        {
            get { return Instance == null || Instance.Definition == null ? string.Empty : Instance.Definition.Id; }
        }

        public CardInstance OwnerCard
        {
            get
            {
                CardInstance card;
                return OwnerUid != 0 && Registry.TryGet(OwnerUid, out card) ? card : null;
            }
        }

        public int AvatarUid
        {
            get { return Board.AvatarUid.Value; }
        }

        public CardInstance AvatarCard
        {
            get { return AvatarUid == 0 ? null : Registry.Get(AvatarUid); }
        }

        public static readonly CoreGameEvent[] EmptyEvents = new CoreGameEvent[0];

        public CardInstance GetCard(int uid)
        {
            return uid == 0 ? null : Registry.Get(uid);
        }

        public bool TryGetCard(int uid, out CardInstance card)
        {
            if (uid == 0)
            {
                card = null;
                return false;
            }

            return Registry.TryGet(uid, out card);
        }

        public CoreGameEvent FirstEventOfType(CoreEventType type)
        {
            var events = Events;
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Type == type)
                {
                    return events[i];
                }
            }

            return null;
        }

        public int FirstEventCardUid()
        {
            var events = Events;
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].CardUid != 0)
                {
                    return events[i].CardUid;
                }
            }

            return 0;
        }

        public int FirstEventTargetUid()
        {
            var events = Events;
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].TargetUid != 0)
                {
                    return events[i].TargetUid;
                }
            }

            return 0;
        }
    }
}
