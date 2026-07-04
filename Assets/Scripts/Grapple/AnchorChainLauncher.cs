using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NinegridGambit.Grapple
{
    /// <summary>
    /// 抛锚 + 锁链 2D 演示效果。
    /// 锚沿抛物线从 <see cref="firePoint"/> 抛向 <see cref="target"/>；
    /// 锁链用 Verlet 质点 + "最大长度"绳索约束模拟：松弛时在空中自然垂坠/弯曲，
    /// 命中后收绳，链条被拉直绷紧。
    /// 链节 sprite 的长轴为竖直方向（局部 +Y），运行时每帧沿绳段方向重新摆放与缩放。
    /// </summary>
    [DisallowMultipleComponent]
    public class AnchorChainLauncher : MonoBehaviour
    {
        public enum Phase { Idle, Flying, Settling, Tightening, Attached }

        [Header("场景引用")]
        [Tooltip("链条起点：船上的绞盘/炮口")]
        public Transform firePoint;
        [Tooltip("锚的可视对象，脚本会驱动它的位置与朝向")]
        public Transform anchorTransform;
        [Tooltip("目标（敌船）")]
        public Transform target;
        [Tooltip("单节锁链模板，运行时会被克隆 segmentCount 份；原件会被隐藏")]
        public SpriteRenderer chainLinkTemplate;

        [Header("链条分段")]
        [Min(2)] public int segmentCount = 10;
        [Tooltip("链节视觉粗细（对应 sprite 的横向缩放）")]
        public float linkThickness = 0.1f;
        [Tooltip("单节 sprite 在缩放为 linkThickness 时、可见链体所占的世界长度")]
        public float naturalLinkLength = 1.0f;
        public int chainSortingOrder = 52;

        [Header("抛物线飞行")]
        [Min(0.05f)] public float flightTime = 0.8f;
        [Tooltip("抛物线额外拱高（越大抛得越高）")]
        public float arcHeight = 3f;
        [Tooltip("命中点相对 target 的偏移")]
        public Vector2 targetOffset = new Vector2(0.3f, 0.2f);
        [Tooltip("锚朝向的微调角度（度）")]
        public float anchorAngleOffset = 0f;

        [Header("链条物理手感")]
        [Tooltip("飞行时绳长 = 直线距离 × slackFactor；越大空中越垂坠")]
        public float slackFactor = 1.6f;
        [Range(0.8f, 1f)] public float damping = 0.98f;
        [Tooltip("链条下垂重力")]
        public float gravity = 14f;
        [Range(1, 80)] public int constraintIterations = 40;

        [Header("命中后收绳绷直")]
        public float settleDelay = 0.15f;
        [Min(0.05f)] public float tightenDuration = 0.7f;
        [Range(0.5f, 1f), Tooltip("绷直后绳长 = 直线距离 × 该比例；1 = 完全拉直")]
        public float tautLengthRatio = 1.0f;

        [Header("演示控制")]
        [Tooltip("是否允许空格等测试键触发；正式流程请走 Fire()")]
        public bool allowTestInput = true;
        public bool autoFireOnStart = false;
        public float autoFireDelay = 1.0f;
        [Tooltip("命中绷直后是否自动复位并再发射（循环演示）")]
        public bool loop = false;
        public float loopInterval = 2.5f;
        public KeyCode fireKey = KeyCode.Space;

        /// <summary>当前阶段，供外部查询。</summary>
        public Phase CurrentPhase => _phase;

        /// <summary>飞行/收绳中为 true，此时 <see cref="Fire"/> 会拒绝。</summary>
        public bool IsBusy =>
            _phase == Phase.Flying || _phase == Phase.Settling || _phase == Phase.Tightening;

        /// <summary>是否可发射（已构建且不在忙碌阶段）。Idle / Attached 均可再发一轮。</summary>
        public bool CanFire => _built && !IsBusy;

        // ---- 运行时内部状态 ----
        Vector2[] _pos;
        Vector2[] _prev;
        Transform[] _links;
        SpriteRenderer[] _linkRenderers;
        SpriteRenderer _anchorRenderer;
        Transform _linkRoot;

        int _points;
        float _restLen;
        float _flightRopeLen;
        float _tautStartLen;
        float _tautRopeLen;
        float _chainZ;

        Phase _phase = Phase.Idle;
        float _timer;
        float _idleTimer;
        bool _autoPending;
        Vector2 _launchStart;
        Vector2 _hitPos;
        bool _built;

        void Start()
        {
            Build();
            SetVisible(false);
            _phase = Phase.Idle;
            _autoPending = autoFireOnStart;
            _idleTimer = autoFireDelay;
        }

        void Build()
        {
            if (chainLinkTemplate == null || firePoint == null || anchorTransform == null || target == null)
            {
                Debug.LogError("[AnchorChainLauncher] 引用未设置完整（firePoint / anchorTransform / target / chainLinkTemplate）。");
                enabled = false;
                return;
            }

            _points = segmentCount + 1;
            _pos = new Vector2[_points];
            _prev = new Vector2[_points];
            _links = new Transform[segmentCount];
            _linkRenderers = new SpriteRenderer[segmentCount];
            _chainZ = anchorTransform.position.z;

            _linkRoot = new GameObject("ChainLinks").transform;
            _linkRoot.SetParent(transform, false);

            for (int i = 0; i < segmentCount; i++)
            {
                var go = Instantiate(chainLinkTemplate.gameObject);
                go.name = "Link_" + i;
                go.transform.SetParent(_linkRoot, false);
                go.SetActive(true);
                var sr = go.GetComponent<SpriteRenderer>();
                sr.sortingOrder = chainSortingOrder;
                _links[i] = go.transform;
                _linkRenderers[i] = sr;
            }

            // 隐藏模板原件，避免场景里多出一节静止的链
            chainLinkTemplate.gameObject.SetActive(false);

            _anchorRenderer = anchorTransform.GetComponent<SpriteRenderer>();
            if (_anchorRenderer != null) _anchorRenderer.sortingOrder = chainSortingOrder + 1;

            _built = true;
        }

        void Update()
        {
            if (!_built) return;

            float dt = Mathf.Min(Time.deltaTime, 0.02f);

            // 测试键：仅空格（或 fireKey），一轮结束后 Idle/Attached 可再按；正统入口是 Fire()
            if (allowTestInput && CanFire && FirePressed()) Fire();

            switch (_phase)
            {
                case Phase.Idle:
                    if (_autoPending)
                    {
                        _idleTimer -= Time.deltaTime;
                        if (_idleTimer <= 0f) { _autoPending = false; Fire(); }
                    }
                    break;

                case Phase.Flying:
                    _timer += Time.deltaTime;
                    float t = _timer / flightTime;
                    anchorTransform.position = new Vector3(Parabola(t).x, Parabola(t).y, _chainZ);
                    if (t >= 1f)
                    {
                        anchorTransform.position = new Vector3(_hitPos.x, _hitPos.y, _chainZ);
                        _phase = Phase.Settling;
                        _timer = 0f;
                    }
                    break;

                case Phase.Settling:
                    _timer += dt;
                    if (_timer >= settleDelay)
                    {
                        _phase = Phase.Tightening;
                        _timer = 0f;
                        _tautStartLen = _flightRopeLen;
                        _tautRopeLen = tautLengthRatio *
                                       Vector2.Distance(firePoint.position, anchorTransform.position);
                    }
                    break;

                case Phase.Tightening:
                    _timer += dt;
                    float k = Mathf.Clamp01(_timer / tightenDuration);
                    float rope = Mathf.Lerp(_tautStartLen, _tautRopeLen, EaseInOut(k));
                    _restLen = rope / segmentCount;
                    if (k >= 1f) _phase = Phase.Attached;
                    break;

                case Phase.Attached:
                    // loop 时自动复位并再发；否则停在 Attached，等下一次 Fire() / 测试键
                    if (loop)
                    {
                        _timer += dt;
                        if (_timer >= loopInterval) ResetDemo();
                    }
                    break;
            }

            if (_phase != Phase.Idle)
            {
                Simulate(dt);
                UpdateVisuals();
            }
        }

        /// <summary>
        /// 正统发射入口（供外部程序 / 玩法逻辑调用）。
        /// Idle 或 Attached 时启动一轮；飞行/收绳中拒绝并返回 false。
        /// </summary>
        public bool Fire()
        {
            if (!_built)
            {
                Build();
            }

            if (!CanFire) return false;

            _autoPending = false;
            _launchStart = firePoint.position;
            _hitPos = (Vector2)target.position + targetOffset;
            _flightRopeLen = slackFactor * Vector2.Distance(_launchStart, _hitPos);
            _restLen = _flightRopeLen / segmentCount;

            // 质点全部聚拢在炮口（沿抛射方向留极小偏移，避免方向退化）
            Vector2 dir = (_hitPos - _launchStart);
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
            for (int i = 0; i < _points; i++)
            {
                _pos[i] = _launchStart + dir * (i * 0.02f);
                _prev[i] = _pos[i];
            }

            anchorTransform.position = new Vector3(_launchStart.x, _launchStart.y, _chainZ);
            SetVisible(true);
            _phase = Phase.Flying;
            _timer = 0f;
            return true;
        }

        /// <summary>强制复位到 Idle（隐藏链条），不自动再发。</summary>
        public void ResetToIdle()
        {
            if (!_built) return;
            _phase = Phase.Idle;
            _timer = 0f;
            _autoPending = false;
            SetVisible(false);
            anchorTransform.position = new Vector3(firePoint.position.x, firePoint.position.y, _chainZ);
        }

        void ResetDemo()
        {
            ResetToIdle();
            // 仅 loop / autoFireOnStart 时排队自动再发
            _autoPending = autoFireOnStart || loop;
            _idleTimer = loop ? loopInterval : autoFireDelay;
        }

        Vector2 Parabola(float t)
        {
            t = Mathf.Clamp01(t);
            Vector2 p = Vector2.Lerp(_launchStart, _hitPos, t);
            p.y += arcHeight * Mathf.Sin(Mathf.PI * t);
            return p;
        }

        static float EaseInOut(float x) => x * x * (3f - 2f * x);

        /// <summary>Verlet 积分 + 最大长度绳索约束（松弛可弯曲、拉满即绷直）。</summary>
        void Simulate(float dt)
        {
            Vector2 g = Vector2.down * gravity;
            float dt2 = dt * dt;

            for (int i = 1; i < _points - 1; i++)
            {
                Vector2 temp = _pos[i];
                Vector2 vel = (_pos[i] - _prev[i]) * damping;
                _pos[i] = _pos[i] + vel + g * dt2;
                _prev[i] = temp;
            }

            Vector2 pinStart = firePoint.position;
            Vector2 pinEnd = anchorTransform.position;

            for (int it = 0; it < constraintIterations; it++)
            {
                _pos[0] = pinStart;
                _pos[_points - 1] = pinEnd;

                for (int s = 0; s < _points - 1; s++)
                {
                    Vector2 a = _pos[s];
                    Vector2 b = _pos[s + 1];
                    Vector2 delta = b - a;
                    float d = delta.magnitude;
                    if (d <= _restLen || d < 1e-6f) continue; // 绳索：仅在超长时收紧

                    float diff = (d - _restLen) / d;
                    bool pinnedA = (s == 0);
                    bool pinnedB = (s + 1 == _points - 1);

                    if (pinnedA && pinnedB) continue;
                    if (pinnedA) { b -= delta * diff; }
                    else if (pinnedB) { a += delta * diff; }
                    else { Vector2 corr = delta * (0.5f * diff); a += corr; b -= corr; }

                    _pos[s] = a;
                    _pos[s + 1] = b;
                }
            }

            _pos[0] = pinStart;
            _pos[_points - 1] = pinEnd;
        }

        void UpdateVisuals()
        {
            for (int i = 0; i < segmentCount; i++)
            {
                Vector2 a = _pos[i];
                Vector2 b = _pos[i + 1];
                Vector2 d = b - a;
                float len = d.magnitude;

                Vector3 mid = new Vector3((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f, _chainZ);
                _links[i].position = mid;

                float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f; // sprite 长轴为 +Y
                _links[i].rotation = Quaternion.Euler(0f, 0f, ang);

                float sy = linkThickness * Mathf.Max(len, 1e-3f) / Mathf.Max(naturalLinkLength, 1e-3f);
                _links[i].localScale = new Vector3(linkThickness, sy, linkThickness);
            }

            // 锚：圆环端指向链条（往回），爪尖朝向前进方向
            Vector2 back = _pos[_points - 2] - _pos[_points - 1];
            if (back.sqrMagnitude > 1e-6f)
            {
                float aa = Mathf.Atan2(back.y, back.x) * Mathf.Rad2Deg - 90f + anchorAngleOffset;
                anchorTransform.rotation = Quaternion.Euler(0f, 0f, aa);
            }
        }

        void SetVisible(bool v)
        {
            if (_linkRenderers != null)
                for (int i = 0; i < _linkRenderers.Length; i++)
                    if (_linkRenderers[i] != null) _linkRenderers[i].enabled = v;
            if (_anchorRenderer != null) _anchorRenderer.enabled = v;
        }

        bool FirePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            // 测试默认空格；其它 KeyCode 走旧 Input 兜底（编辑器里改 fireKey 时）
            if (fireKey == KeyCode.Space)
                return kb.spaceKey.wasPressedThisFrame;
#endif
            return Input.GetKeyDown(fireKey);
        }
    }
}
