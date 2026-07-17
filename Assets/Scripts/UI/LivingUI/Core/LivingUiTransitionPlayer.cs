using System;

namespace NineGrid.LivingUI
{
    public sealed class LivingUiTransitionPlayer
    {
        private LivingUiTransitionPlan _plan;
        private float _elapsed;

        public LivingUiTransitionPlan ActivePlan => _plan;
        public float Elapsed => _elapsed;
        public bool IsPlaying => _plan != null && _elapsed < _plan.Makespan;

        public bool TryBegin(LivingUiTransitionPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (_plan != null && plan.Generation <= _plan.Generation)
            {
                return false;
            }

            _plan = plan;
            _elapsed = 0f;
            return true;
        }

        public void Advance(float deltaTime)
        {
            if (_plan == null || deltaTime <= 0f) return;
            _elapsed = Math.Min(_elapsed + deltaTime, _plan.Makespan);
        }

        public LivingUiCarrierState Sample(int carrierId)
        {
            if (_plan == null) throw new InvalidOperationException("当前没有转场计划。");
            return _plan.GetProgram(carrierId).Sample(_elapsed);
        }
    }
}
