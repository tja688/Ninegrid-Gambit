using System;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 按注册顺序把意图交给子工厂；未识别 kind 的子工厂应 no-op。
    /// </summary>
    public sealed class RoutingIntentScriptFactory : IIntentScriptFactory
    {
        private readonly IIntentScriptFactory[] mFactories;

        public RoutingIntentScriptFactory(params IIntentScriptFactory[] factories)
        {
            if (factories == null || factories.Length == 0)
            {
                throw new ArgumentException("RoutingIntentScriptFactory requires at least one factory.", "factories");
            }

            mFactories = new IIntentScriptFactory[factories.Length];
            for (var i = 0; i < factories.Length; i++)
            {
                if (factories[i] == null)
                {
                    throw new ArgumentException("RoutingIntentScriptFactory child cannot be null.", "factories");
                }

                mFactories[i] = factories[i];
            }
        }

        public void BuildScript(InputIntent intent, BattleTimeline timeline)
        {
            if (timeline == null)
            {
                throw new ArgumentNullException("timeline");
            }

            for (var i = 0; i < mFactories.Length; i++)
            {
                mFactories[i].BuildScript(intent, timeline);
            }
        }
    }
}
