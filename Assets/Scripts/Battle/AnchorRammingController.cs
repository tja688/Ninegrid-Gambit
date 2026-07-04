using System;
using System.Collections;
using DG.Tweening;
using NinegridGambit.Grapple;
using UnityEngine;
using UnityEngine.Events;

namespace NineGrid.Battle
{
    /// <summary>
    /// 抛锚互撞子状态机：抛锚命中并绷直锁链之后，唤起绞盘让玩家狂点，
    /// 依据“绞劲”把两船越拉越快地拽向对方 → 猛烈对撞 → 弹开回位。
    ///
    /// 流程：
    ///   Attached（等锚命中） → Cranking（狂点拉近，锁链持续绷直缩短）
    ///   → PreImpact（可选：时间缓速 + 镜头放大） → Impact（震屏 + 命中冻结 + 反馈钩子）
    ///   → Bounce（迅速弹开并回到基础点位） → 完成回调。
    ///
    /// 声音 / 特效通过 UnityEvent 钩子预留，直接在 Inspector 挂即可。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnchorRammingController : MonoBehaviour
    {
        public static AnchorRammingController Instance { get; private set; }

        public enum RamPhase { Idle, WaitingAttach, Cranking, PreImpact, Impact, Bounce }

        [Header("引用（留空按名字/类型自动解析）")]
        [SerializeField] Transform player;
        [SerializeField] Transform enemy;
        [Tooltip("玩家基础停留点（对撞后回到这里）。")]
        [SerializeField] Transform playerHome;
        [Tooltip("敌人基础停留点。")]
        [SerializeField] Transform enemyHome;
        [SerializeField] AnchorChainLauncher anchorChain;
        [SerializeField] WinchCrankController winch;
        [SerializeField] CameraJuice cameraJuice;
        [Tooltip("互撞命中时的屏幕震动（可复用的独立效果）。")]
        [SerializeField] ScreenShakeEffect impactShake;

        [Header("拉近 / 双向奔赴")]
        [Tooltip("绞劲很低时的拉近速度（进度/秒）。与 maxApproachRate 一起决定拉近总时长（非固定秒数，随狂点变化）。")]
        public float minApproachRate = 0.02f;
        [Tooltip("狂点顶满时的拉近速度（进度/秒）。越大整段 Approach 越短。")]
        public float maxApproachRate = 1.55f;
        [Tooltip("绞劲→拉近的幂次。>1 时低强度几乎不动，逼近满劲才缓慢启动再猛加速。")]
        [Range(1f, 4f)] public float approachIntensityPower = 2.4f;
        [Tooltip("对撞瞬间两船“中心距离”。越小越脸贴脸。")]
        public float collisionGap = 1.0f;
        [Tooltip("拉近进度(X:0→1) → 位移权重(Y)。只改轨迹形状，不改总时长。时长看 min/maxApproachRate。")]
        public AnimationCurve approachEase = DefaultApproachEaseCurve();
        [Tooltip("拉近过程中船体微抖峰值（世界单位）。保持很小，避免抢轨迹。")]
        public float shipShakeMax = 0.04f;

        [Header("对撞前演出（时间缓速 + 镜头放大 · 可开关对比）")]
        [Tooltip("总开关：觉得效果不好就关掉，只保留干脆的对撞。")]
        public bool enableSlowMoZoom = true;
        [Tooltip("拉近进度到达该值时进入临撞时缓（只卡贴撞前极短一段）。")]
        [Range(0.5f, 0.98f)] public float preImpactApproach = 0.93f;
        [Tooltip("临撞时缓窗口时长（缩放时间秒）。越短越干脆。")]
        public float finalRushDuration = 0.16f;
        [Tooltip("终段冲刺缓动：短窗口内保持冲刺感。")]
        public Ease finalRushEase = Ease.Linear;
        [Tooltip("缓速目标 timeScale。")]
        [Range(0.05f, 1f)] public float slowMoTimeScale = 0.22f;
        [Tooltip("进入缓速的过渡时间（真实秒）。")]
        public float slowMoRampDuration = 0.04f;
        [Tooltip("镜头放大倍率（正交尺寸乘数，<1 为拉近）。")]
        [Range(0.3f, 1f)] public float zoomSizeMultiplier = 0.72f;
        [Tooltip("对撞后镜头拉回时间（真实秒）。")]
        public float zoomOutDuration = 0.22f;

        [Header("对撞反馈")]
        [Tooltip("命中冻结时长（真实秒），0 = 不冻结。")]
        public float hitStopDuration = 0.1f;
        [Tooltip("对撞力度下限，保证再弱也有反馈。")]
        [Range(0f, 1f)] public float minImpactPower = 0.25f;
        [Tooltip("对撞力度的衰减：反映“临撞前一段时间的狂点峰值”。")]
        public float impactPowerDecay = 1.5f;

        [Header("弹开回位")]
        [Tooltip("满力度时，弹开越过基础点位的额外距离。")]
        public float bounceOvershoot = 1.8f;
        [Tooltip("弹开（向外冲）的时长（真实秒）。")]
        public float bounceOutDuration = 0.16f;
        [Tooltip("回落到基础点位的时长（真实秒）。")]
        public float settleDuration = 0.6f;
        public Ease settleEase = Ease.OutBack;

        [Header("其它")]
        [Tooltip("等待抛锚命中（Attached）的超时（真实秒）。")]
        public float attachTimeout = 6f;

        [Header("反馈钩子（挂音效 / 特效）")]
        public UnityEvent onRamBegin;
        public UnityEvent onCrankBegin;
        public UnityEvent onPreImpact;
        public UnityEvent onImpact;
        public UnityEvent onBounce;
        public UnityEvent onRamEnd;

        /// <summary>互撞流程结束（无论成功与否），战斗侧据此回到 Active。</summary>
        public event Action Completed;

        public bool IsRunning => _running;
        public RamPhase Phase => _phase;

        Coroutine _routine;
        Tween _timeTween;
        bool _running;
        RamPhase _phase = RamPhase.Idle;

        // 抛锚链端点追踪
        Vector3 _fireHome;
        Vector3 _fireOffset;
        Vector3 _anchorOffset;
        bool _trackChain;
        bool _prevAllowTestInput;

        void Awake()
        {
            Instance = this;
            EnsureApproachEaseCurve();
        }

        void OnValidate()
        {
            EnsureApproachEaseCurve();
        }

        void EnsureApproachEaseCurve()
        {
            if (approachEase == null || approachEase.length == 0)
                approachEase = DefaultApproachEaseCurve();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 开始互撞流程（假定抛锚已 Fire）。内部会等待命中(Attached)再唤起绞盘。
        /// </summary>
        public bool Begin()
        {
            if (_running) return false;
            ResolveRefs();
            if (player == null || enemy == null)
            {
                Debug.LogWarning("[Ram] 缺少 player / enemy，无法互撞。");
                return false;
            }

            _routine = StartCoroutine(RamRoutine());
            return true;
        }

        /// <summary>被打断时强制复位（时间、镜头、绞盘、船位、锁链）。</summary>
        public void ForceStop()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            KillTimeTween();
            Time.timeScale = 1f;

            if (winch != null) winch.EndCrank(true);
            if (cameraJuice != null) cameraJuice.ResetImmediate();

            if (anchorChain != null)
            {
                anchorChain.allowTestInput = _prevAllowTestInput;
                if (_trackChain && anchorChain.firePoint != null)
                {
                    anchorChain.firePoint.position = _fireHome;
                }
                anchorChain.ResetToIdle();
            }

            if (player != null && playerHome != null) player.position = playerHome.position;
            if (enemy != null && enemyHome != null) enemy.position = enemyHome.position;

            _running = false;
            _phase = RamPhase.Idle;
        }

        IEnumerator RamRoutine()
        {
            _running = true;
            _phase = RamPhase.WaitingAttach;
            onRamBegin?.Invoke();

            Vector3 pHome = playerHome != null ? playerHome.position : player.position;
            Vector3 eHome = enemyHome != null ? enemyHome.position : enemy.position;

            Vector3 mid = (pHome + eHome) * 0.5f;
            Vector3 dir = eHome - pHome;
            dir.z = 0f;
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.left;
            Vector3 pContact = mid - dir * (collisionGap * 0.5f);
            Vector3 eContact = mid + dir * (collisionGap * 0.5f);

            // 等抛锚命中并绷直。
            if (anchorChain != null)
            {
                float timeout = Time.unscaledTime + attachTimeout;
                yield return new WaitUntil(() =>
                    anchorChain == null
                    || anchorChain.CurrentPhase == AnchorChainLauncher.Phase.Attached
                    || Time.unscaledTime >= timeout);
            }

            // 锁链端点跟随两船：抓取当前偏移，后续每帧重贴 + 绷直。
            _trackChain = anchorChain != null
                && anchorChain.firePoint != null
                && anchorChain.anchorTransform != null;
            if (_trackChain)
            {
                _fireHome = anchorChain.firePoint.position;
                _fireOffset = anchorChain.firePoint.position - player.position;
                _anchorOffset = anchorChain.anchorTransform.position - enemy.position;
                _prevAllowTestInput = anchorChain.allowTestInput;
                anchorChain.allowTestInput = false; // 防止 ram 期间空格误发。
            }

            // ---- Cranking：狂点拉近 ----
            _phase = RamPhase.Cranking;
            if (winch != null) winch.BeginCrank();
            onCrankBegin?.Invoke();

            float approach = 0f;
            float impactPower = 0f;
            bool committed = false;
            float committedStart = 0f;
            float rushElapsed = 0f;

            while (approach < 1f)
            {
                float dt = Time.deltaTime;
                float intensity = winch != null ? winch.Intensity01 : 1f;

                if (!committed)
                {
                    // 力度 = 临撞前一段时间的狂点峰值（松手会掉）。
                    impactPower = Mathf.Max(impactPower - impactPowerDecay * Time.unscaledDeltaTime, intensity);

                    // 低绞劲几乎不动；逼近满劲才缓慢启动，再猛加速。
                    float pull = Mathf.Pow(Mathf.Clamp01(intensity), approachIntensityPower);
                    float rate = Mathf.Lerp(minApproachRate, maxApproachRate, pull);
                    approach = Mathf.Min(1f, approach + rate * dt);

                    if (approach >= preImpactApproach)
                    {
                        committed = true;
                        committedStart = approach;
                        rushElapsed = 0f;
                        _phase = RamPhase.PreImpact;
                        onPreImpact?.Invoke();

                        if (enableSlowMoZoom)
                        {
                            EngageSlowMo();
                            if (cameraJuice != null)
                            {
                                float zoomIn = finalRushDuration / Mathf.Max(0.05f, slowMoTimeScale);
                                cameraJuice.BeginZoom(zoomSizeMultiplier, zoomIn);
                            }
                        }
                    }
                }
                else
                {
                    // 终段：慢启动 → 加速撞上（缩放时间下更有速度感）。
                    rushElapsed += dt;
                    float u = Mathf.Clamp01(rushElapsed / Mathf.Max(0.01f, finalRushDuration));
                    float easedU = DOVirtual.EasedValue(0f, 1f, u, finalRushEase);
                    approach = Mathf.Lerp(committedStart, 1f, easedU);
                }

                float eased = EvaluateApproachEase(approach);
                // 轨迹优先：只留极轻微抖，大爆发留给撞击瞬间。
                float jitter = shipShakeMax * Mathf.Lerp(0.25f, 1f, intensity);
                player.position = Vector3.LerpUnclamped(pHome, pContact, eased) + Jitter(jitter, 1.1f, 30f);
                enemy.position = Vector3.LerpUnclamped(eHome, eContact, eased) + Jitter(jitter, 5.7f, 30f);
                UpdateChainTracking();

                yield return null;
            }

            // 精确贴到接触点。
            player.position = pContact;
            enemy.position = eContact;
            UpdateChainTracking();

            // ---- Impact：对撞反馈 ----
            _phase = RamPhase.Impact;
            KillTimeTween();
            onImpact?.Invoke();

            float power = Mathf.Clamp01(Mathf.Max(impactPower, minImpactPower));
            if (impactShake != null) impactShake.Play(power);

            if (hitStopDuration > 0f)
            {
                Time.timeScale = 0f;
                yield return new WaitForSecondsRealtime(hitStopDuration);
            }
            Time.timeScale = 1f;

            if (enableSlowMoZoom && cameraJuice != null)
            {
                cameraJuice.EndZoom(zoomOutDuration);
            }

            // ---- Bounce：迅速弹开 → 回到基础点位 ----
            _phase = RamPhase.Bounce;
            onBounce?.Invoke();

            float over = bounceOvershoot * power;
            Vector3 pOut = pHome + (pHome - pContact).normalized * over;
            Vector3 eOut = eHome + (eHome - eContact).normalized * over;

            yield return MoveShips(pContact, pOut, eContact, eOut, bounceOutDuration, Ease.OutQuad);
            yield return MoveShips(pOut, pHome, eOut, eHome, settleDuration, settleEase);

            // ---- Cleanup ----
            if (anchorChain != null)
            {
                if (_trackChain && anchorChain.firePoint != null)
                {
                    anchorChain.firePoint.position = _fireHome;
                }
                anchorChain.allowTestInput = _prevAllowTestInput;
                anchorChain.ResetToIdle();
            }

            player.position = pHome;
            enemy.position = eHome;

            if (winch != null) winch.EndCrank(true);
            Time.timeScale = 1f;
            KillTimeTween();

            _running = false;
            _phase = RamPhase.Idle;
            _routine = null;
            onRamEnd?.Invoke();
            Completed?.Invoke();
        }

        IEnumerator MoveShips(Vector3 pFrom, Vector3 pTo, Vector3 eFrom, Vector3 eTo, float duration, Ease ease)
        {
            if (duration <= 0f)
            {
                player.position = pTo;
                enemy.position = eTo;
                UpdateChainTracking();
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                float e = DOVirtual.EasedValue(0f, 1f, k, ease);
                player.position = Vector3.LerpUnclamped(pFrom, pTo, e);
                enemy.position = Vector3.LerpUnclamped(eFrom, eTo, e);
                UpdateChainTracking();
                yield return null;
            }

            player.position = pTo;
            enemy.position = eTo;
            UpdateChainTracking();
        }

        void UpdateChainTracking()
        {
            if (!_trackChain || anchorChain == null) return;
            if (anchorChain.firePoint != null) anchorChain.firePoint.position = player.position + _fireOffset;
            if (anchorChain.anchorTransform != null) anchorChain.anchorTransform.position = enemy.position + _anchorOffset;
            anchorChain.HoldTaut();
        }

        void EngageSlowMo()
        {
            KillTimeTween();
            _timeTween = DOTween.To(() => Time.timeScale, v => Time.timeScale = v, slowMoTimeScale, slowMoRampDuration)
                .SetUpdate(true);
        }

        void KillTimeTween()
        {
            if (_timeTween != null && _timeTween.IsActive()) _timeTween.Kill();
            _timeTween = null;
        }

        float EvaluateApproachEase(float progress)
        {
            if (approachEase == null || approachEase.length == 0)
                return Mathf.Clamp01(progress);
            return approachEase.Evaluate(Mathf.Clamp01(progress));
        }

        /// <summary>默认近似 InQuart：前半慢、后半加速。可在 Inspector 曲线窗口手调。</summary>
        static AnimationCurve DefaultApproachEaseCurve()
        {
            var curve = new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 0f),
                new Keyframe(0.5f, 0.0625f, 0.25f, 0.25f),
                new Keyframe(0.75f, 0.3164f, 1.2f, 1.2f),
                new Keyframe(1f, 1f, 4f, 0f));
            return curve;
        }

        static Vector3 Jitter(float amp, float seed, float hz = 40f)
        {
            if (amp <= 0f) return Vector3.zero;
            float t = Time.unscaledTime * hz;
            float x = (Mathf.PerlinNoise(seed, t) - 0.5f) * 2f * amp;
            float y = (Mathf.PerlinNoise(t, seed + 3.1f) - 0.5f) * 2f * amp;
            return new Vector3(x, y, 0f);
        }

        void ResolveRefs()
        {
            if (player == null) player = FindByName("player");
            if (enemy == null) enemy = FindByName("enemy");
            if (playerHome == null) playerHome = FindByName("player stay");
            if (enemyHome == null) enemyHome = FindByName("enemy stay");

            if (anchorChain == null)
                anchorChain = FindFirstObjectByType<AnchorChainLauncher>(FindObjectsInactive.Include);
            if (winch == null)
                winch = FindFirstObjectByType<WinchCrankController>(FindObjectsInactive.Include);
            if (cameraJuice == null)
                cameraJuice = CameraJuice.Resolve();
            if (impactShake == null)
            {
                impactShake = GetComponent<ScreenShakeEffect>()
                    ?? GetComponentInChildren<ScreenShakeEffect>(true);
            }
        }

        static Transform FindByName(string objectName)
        {
            var go = GameObject.Find(objectName);
            return go != null ? go.transform : null;
        }
    }
}
