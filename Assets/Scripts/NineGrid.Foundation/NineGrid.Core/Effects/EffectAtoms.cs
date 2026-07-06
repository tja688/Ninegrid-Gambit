using System;
using System.Collections.Generic;
using System.Reflection;
using NineGrid.Core.Stats;

namespace NineGrid.Core.Effects
{
    public interface IEffectAtom
    {
        void Configure(EffectDslNode config);
    }

    public interface ITrigger : IEffectAtom
    {
        TriggerPoint Point { get; }
        TriggerTiming Timing { get; }
        bool Matches(EffectRuntimeContext context);
    }

    public interface ICondition : IEffectAtom
    {
        bool IsMet(EffectRuntimeContext context);
        IStatCondition CreateStatCondition(EffectBuildContext context);
    }

    public interface ITarget : IEffectAtom
    {
        IReadOnlyList<int> Resolve(EffectRuntimeContext context);
    }

    public interface IAction : IEffectAtom
    {
        IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets);
    }

    public sealed class EffectBuildContext
    {
        public EffectBuildContext(QFramework.IArchitecture architecture, EffectInstance instance)
        {
            Architecture = architecture;
            Instance = instance;
        }

        public QFramework.IArchitecture Architecture { get; private set; }
        public EffectInstance Instance { get; private set; }
    }

    public sealed class EffectAtomRegistry
    {
        private readonly Dictionary<string, Type> mTriggers = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Type> mConditions = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Type> mTargets = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Type> mActions = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, Type> Triggers
        {
            get { return mTriggers; }
        }

        public IReadOnlyDictionary<string, Type> Conditions
        {
            get { return mConditions; }
        }

        public IReadOnlyDictionary<string, Type> Targets
        {
            get { return mTargets; }
        }

        public IReadOnlyDictionary<string, Type> Actions
        {
            get { return mActions; }
        }

        public void DiscoverLoadedAssemblies()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                Discover(assemblies[i]);
            }
        }

        public void Discover(Assembly assembly)
        {
            if (assembly == null)
            {
                return;
            }

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types;
            }

            for (var i = 0; i < types.Length; i++)
            {
                var type = types[i];
                if (type == null || type.IsAbstract || type.IsInterface)
                {
                    continue;
                }

                var attribute = (EffectAtomAttribute)Attribute.GetCustomAttribute(type, typeof(EffectAtomAttribute));
                if (attribute == null || string.IsNullOrEmpty(attribute.Id))
                {
                    continue;
                }

                Register(attribute, type);
            }
        }

        public ITrigger CreateTrigger(EffectDslNode config)
        {
            return Create<ITrigger>(config, mTriggers, "trigger");
        }

        public ICondition CreateCondition(EffectDslNode config)
        {
            return Create<ICondition>(config, mConditions, "condition");
        }

        public ITarget CreateTarget(EffectDslNode config)
        {
            if (config == null || config.IsNull)
            {
                config = new EffectDslNode(new Dictionary<string, object> { { "atom", "Self" } });
            }

            return Create<ITarget>(config, mTargets, "target");
        }

        public IAction CreateAction(EffectDslNode config)
        {
            return Create<IAction>(config, mActions, "action");
        }

        private void Register(EffectAtomAttribute attribute, Type type)
        {
            switch (attribute.Kind)
            {
                case EffectAtomKind.Trigger:
                    RequireAssignable<ITrigger>(type, attribute.Id);
                    mTriggers[attribute.Id] = type;
                    break;
                case EffectAtomKind.Condition:
                    RequireAssignable<ICondition>(type, attribute.Id);
                    mConditions[attribute.Id] = type;
                    break;
                case EffectAtomKind.Target:
                    RequireAssignable<ITarget>(type, attribute.Id);
                    mTargets[attribute.Id] = type;
                    break;
                case EffectAtomKind.Action:
                    RequireAssignable<IAction>(type, attribute.Id);
                    mActions[attribute.Id] = type;
                    break;
            }
        }

        private static T Create<T>(EffectDslNode config, Dictionary<string, Type> map, string label) where T : class, IEffectAtom
        {
            if (config == null || config.IsNull)
            {
                throw new InvalidOperationException("Missing " + label + " atom config.");
            }

            var id = config.Get("atom").AsString(config.Get("type").AsString(string.Empty));
            if (string.IsNullOrEmpty(id))
            {
                throw new InvalidOperationException("Missing " + label + " atom id.");
            }

            Type type;
            if (!map.TryGetValue(id, out type))
            {
                throw new InvalidOperationException("Unknown " + label + " atom: " + id);
            }

            var atom = (T)Activator.CreateInstance(type);
            atom.Configure(config);
            return atom;
        }

        private static void RequireAssignable<T>(Type type, string id)
        {
            if (!typeof(T).IsAssignableFrom(type))
            {
                throw new InvalidOperationException("Effect atom " + id + " must implement " + typeof(T).Name + ".");
            }
        }
    }
}
