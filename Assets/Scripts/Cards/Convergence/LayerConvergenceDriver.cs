using System;
using UnityEngine;

namespace NineGrid.Cards.Convergence
{
    /// <summary>
    /// 单层 localPosition 五次收敛驱动器。初版用变长帧 Time.deltaTime，sourceTime 为墙钟秒。
    /// 复杂域 Evict 吐真实速度（B）；净土域交接口仍由各 Manager 恒填 0（C）。
    /// 同一卡可挂多个实例（L2 / L3 各一），按 <see cref="DrivenLayer"/> 区分。
    /// </summary>
    [RequireComponent(typeof(CardTransformTower))]
    public sealed class LayerConvergenceDriver : MonoBehaviour, IHandoffEndpoint
    {
        [SerializeField]
        [Tooltip("驱动的塔层。默认 L2 SlotFrame（格位收敛）；L3 特效位移另挂一实例并设为 EffectFrame。")]
        private TowerLayer drivenLayer = TowerLayer.SlotFrame;

        private CardTransformTower _tower;
        private Transform _layer;
        private ConvergenceCurve _curve;
        private Vector3 _targetLocal;
        private Vector3 _heldVelocity;
        private float _elapsed;
        private bool _active;

        public TowerLayer DrivenLayer => drivenLayer;
        public bool IsActive => _active;
        public bool IsComplete => !_active;
        public Vector3 TargetLocal => _targetLocal;
        public float Elapsed => _elapsed;

        public event Action Completed;

        private void Awake()
        {
            ResolveLayer();
        }

        /// <summary>配置驱动层并立刻解析 Transform（AddComponent 后调用）。</summary>
        public void ConfigureDrivenLayer(TowerLayer layer)
        {
            drivenLayer = layer;
            ResolveLayer();
        }

        public static bool TryGet(Transform root, TowerLayer layer, out LayerConvergenceDriver driver)
        {
            driver = null;
            if (root == null)
            {
                return false;
            }

            var drivers = root.GetComponents<LayerConvergenceDriver>();
            for (var i = 0; i < drivers.Length; i++)
            {
                if (drivers[i] != null && drivers[i].DrivenLayer == layer)
                {
                    driver = drivers[i];
                    return true;
                }
            }

            return false;
        }

        public static LayerConvergenceDriver Ensure(Transform root, TowerLayer layer)
        {
            if (root == null)
            {
                return null;
            }

            if (TryGet(root, layer, out var existing))
            {
                return existing;
            }

            var driver = root.gameObject.AddComponent<LayerConvergenceDriver>();
            driver.ConfigureDrivenLayer(layer);
            return driver;
        }

        private void Update()
        {
            if (!_active)
            {
                return;
            }

            Tick(Time.deltaTime);
        }

        /// <summary>
        /// 推进收敛时间。Update 走变长帧；EditMode 测试可直接调用。
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!_active || _layer == null)
            {
                return;
            }

            if (deltaTime < 0f)
            {
                deltaTime = 0f;
            }

            _elapsed += deltaTime;
            _layer.localPosition = _curve.EvaluatePosition(_elapsed);
            _heldVelocity = _curve.EvaluateVelocity(_elapsed);

            if (!_curve.IsComplete(_elapsed))
            {
                return;
            }

            FinishAtTarget();
        }

        /// <summary>
        /// 从当前 local 位姿与速度出发，在 sourceTime 秒后精确落到 targetLocal（末速归零）。
        /// 若已在收敛中，以当前位姿+速度重解（C1 连续）。
        /// </summary>
        public void ConvergeTo(Vector3 targetLocal, float sourceTime)
        {
            ResolveLayer();
            if (_layer == null)
            {
                Debug.LogError("[LayerConvergenceDriver] 塔层未就绪，无法收敛。", this);
                return;
            }

            var p0 = _layer.localPosition;
            var v0 = _active ? _curve.EvaluateVelocity(_elapsed) : _heldVelocity;
            _targetLocal = targetLocal;
            _curve = ConvergenceCurve.Create(p0, v0, targetLocal, sourceTime);
            _elapsed = 0f;
            _active = true;
        }

        /// <summary>中途换目标：等价于 ConvergeTo（内部已带速度重解）。</summary>
        public void Redirect(Vector3 newTargetLocal, float newSourceTime) =>
            ConvergeTo(newTargetLocal, newSourceTime);

        public void Stop()
        {
            if (!_active)
            {
                return;
            }

            if (_layer != null)
            {
                _heldVelocity = _curve.EvaluateVelocity(_elapsed);
            }

            _active = false;
        }

        public HandoffState Evict()
        {
            ResolveLayer();
            var position = _layer != null ? _layer.localPosition : Vector3.zero;
            // B 阶段（复杂域 L2/L3）：吐真实速度，供征用/跨域交接 C1 连续。
            var velocity = SampleVelocity();
            Stop();
            _heldVelocity = Vector3.zero;
            return new HandoffState(position, velocity);
        }

        public void Admit(in HandoffState state)
        {
            ResolveLayer();
            _active = false;
            if (_layer != null)
            {
                _layer.localPosition = state.LocalPosition;
            }

            _heldVelocity = state.LocalVelocity;
            _targetLocal = state.LocalPosition;
        }

        public Vector3 SampleVelocity()
        {
            if (_active)
            {
                return _curve.EvaluateVelocity(_elapsed);
            }

            return _heldVelocity;
        }

        private void FinishAtTarget()
        {
            if (_layer != null)
            {
                _layer.localPosition = _targetLocal;
            }

            _heldVelocity = Vector3.zero;
            _active = false;
            Completed?.Invoke();
        }

        private void ResolveLayer()
        {
            if (_tower == null)
            {
                _tower = GetComponent<CardTransformTower>();
            }

            if (_tower == null)
            {
                return;
            }

            _tower.EnsureTower();
            _layer = _tower.GetLayer(drivenLayer);
        }

#if UNITY_EDITOR
        [ContextMenu("Demo/Converge L2 To Offset (0.45s)")]
        private void DemoConvergeToOffset()
        {
            ConvergeTo(new Vector3(1.5f, 0.8f, 0f), 0.45f);
        }

        [ContextMenu("Demo/Converge L2 Home (0.35s)")]
        private void DemoConvergeHome()
        {
            ConvergeTo(Vector3.zero, 0.35f);
        }
#endif
    }
}
