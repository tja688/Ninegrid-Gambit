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

    /// <summary>
    /// Optional trigger contract for one context that consumes multiple logical firings.
    /// </summary>
    public interface ITriggerFireCount
    {
        int FireCount { get; }
    }

    /// <summary>
    /// 倒计时计数器作用域标记（ADR-0035 / #157）：Battle 作用域在离开战斗时真重置为阈值
    /// （authority + projection，教玩家"不跨场"）；Run 作用域跨战斗忠实保留剩余。
    /// 标记仅作者/系统可见——只出现在 DSL 配置里，绝不进入投影键或玩家可见字符串。
    /// </summary>
    public enum CountdownScope
    {
        /// <summary>默认：本战斗生命周期；离战重置为阈值。</summary>
        Battle,

        /// <summary>整趟跑图生命周期；离战保留剩余。</summary>
        Run,
    }

    /// <summary>
    /// 倒计时投影触发契约（ADR-0035）：带 every/threshold 的倒计时触发在 <see cref="ITrigger.Matches"/>
    /// 推进计数器后，由 <see cref="EffectSystem"/> 生成 <see cref="CommitEffectCountdownRemainingAction"/>，
    /// 把剩余次数作为结算指令广播到表现层（View 禁止直读 Core 计数器）。
    /// 模板经 DSL <c>projectKey</c> 声明投影令牌键（完整「装配id.键」）；空 = 不投影。
    /// </summary>
    public interface ICountdownProjectionTrigger : ITrigger
    {
        /// <summary>投影令牌键（如 <c>trap.flame.remove.every</c>）；空 = 本触发不投影。</summary>
        string CountdownProjectionKey { get; }

        /// <summary>本次 Matches 实际用于计数的 Core 计数器键（存放剩余次数）。</summary>
        string CountdownCounterKey { get; }

        /// <summary>本次 Matches 是否推进了倒计时计数器（剩余发生变化）。</summary>
        bool CountdownAdvanced { get; }

        /// <summary>
        /// 作用域标记（Battle 默认 / Run，DSL <c>scope</c> 配置，仅作者/系统可见）。
        /// Battle 作用域由离战重置动作复位为 <see cref="CountdownPeriod"/>；Run 作用域不重置。
        /// </summary>
        CountdownScope Scope { get; }

        /// <summary>倒计时周期（every / threshold）：Battle 离战重置的目标阈值。</summary>
        int CountdownPeriod { get; }

        /// <summary>
        /// 确定性计数器键（离战重置用，不依赖 Matches 解析）：显式 <c>counterKey</c> 或
        /// 按实例 id 推导的自动键（<c>effect.&lt;instanceId&gt;.&lt;suffix&gt;</c>）。
        /// </summary>
        string ResolveCounterKey(string instanceId);
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

                var attributes = (EffectAtomAttribute[])Attribute.GetCustomAttributes(type, typeof(EffectAtomAttribute));
                if (attributes == null || attributes.Length == 0)
                {
                    continue;
                }

                for (var a = 0; a < attributes.Length; a++)
                {
                    var attribute = attributes[a];
                    if (attribute == null || string.IsNullOrEmpty(attribute.Id))
                    {
                        continue;
                    }

                    Register(attribute, type);
                }
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
