using System.Collections.Generic;
using QFramework;

namespace NineGrid.Core
{
    public sealed class GameActionContext
    {
        public GameActionContext(IArchitecture architecture, EventLog eventLog, int actionId, int depth)
        {
            Architecture = architecture;
            EventLog = eventLog;
            ActionId = actionId;
            Depth = depth;
        }

        public IArchitecture Architecture { get; private set; }
        public EventLog EventLog { get; private set; }
        public int ActionId { get; private set; }
        public int Depth { get; private set; }

        public T GetModel<T>() where T : class, IModel
        {
            return Architecture.GetModel<T>();
        }

        public T GetSystem<T>() where T : class, ISystem
        {
            return Architecture.GetSystem<T>();
        }
    }

    public sealed class GameActionResult
    {
        private readonly List<CoreGameEvent> mEvents = new List<CoreGameEvent>();
        private readonly List<GameAction> mFollowUpActions = new List<GameAction>();

        public IReadOnlyList<CoreGameEvent> Events
        {
            get { return mEvents; }
        }

        public IReadOnlyList<GameAction> FollowUpActions
        {
            get { return mFollowUpActions; }
        }

        public static GameActionResult Empty
        {
            get { return new GameActionResult(); }
        }

        public GameActionResult AddEvent(CoreGameEvent gameEvent)
        {
            if (gameEvent != null)
            {
                mEvents.Add(gameEvent);
            }

            return this;
        }

        public GameActionResult AddFollowUp(GameAction action)
        {
            if (action != null)
            {
                mFollowUpActions.Add(action);
            }

            return this;
        }
    }

    public abstract class GameAction
    {
        private static readonly TriggerPoint[] sBeforeAction = { TriggerPoint.BeforeAction };
        private static readonly TriggerPoint[] sAfterAction = { TriggerPoint.AfterAction };

        public abstract string ActionName { get; }

        public virtual IEnumerable<TriggerPoint> GetPreTriggerPoints(GameActionContext context)
        {
            return sBeforeAction;
        }

        public abstract GameActionResult Apply(GameActionContext context);

        public virtual IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            return sAfterAction;
        }
    }
}
