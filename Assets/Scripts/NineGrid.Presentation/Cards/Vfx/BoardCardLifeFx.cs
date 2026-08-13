using System.Collections.Generic;
using NineGrid.Cards.Convergence;
using UnityEngine;

namespace NineGrid.Cards.Vfx
{
    /// <summary>
    /// 场地卡"活着"的表现层：独占 L1 BoardFrame，做低频悬浮飘动与冲击波位移。
    /// 纯装饰：不读写 Core 规则状态、不参与 Batch-ack、不改占格权威（L0/L2/L3 一律不碰）。
    /// 交战/受击/换位期间该卡自动回位归零（"战斗与受击回位"）。
    /// </summary>
    public static class BoardCardLifeFx
    {
        /// <summary>全局开关。关闭后所有 L1 偏移立刻归零。</summary>
        public static bool Enabled
        {
            get => sEnabled;
            set
            {
                sEnabled = value;
                if (!value)
                {
                    Reset();
                }
            }
        }

        /// <summary>悬浮飘动幅度倍率（0 = 只保留冲击波，1 = 默认）。</summary>
        public static float DriftScale { get; set; } = 1f;

        /// <summary>冲击波幅度倍率。</summary>
        public static float ImpulseScale { get; set; } = 1f;

        private static bool sEnabled = true;
        private static BoardCardLifeRunner sRunner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            sRunner = null;
            sEnabled = true;
            DriftScale = 1f;
            ImpulseScale = 1f;
        }

        /// <summary>
        /// 悬浮飘动是常驻表现，不能等第一次冲击才建 Runner——这里主动起一次。
        /// 场上无卡时 Runner 每帧只做一次空成员同步，代价可忽略。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EnsureRunner();
        }

        /// <summary>
        /// 径向冲击波：以 centerWorld 为心，把场上卡向四周推开，按距离衰减并延迟（波前扩散）。
        /// </summary>
        public static void PlayRadialShock(Vector3 centerWorld, float strength = 1f)
        {
            if (!sEnabled || strength <= 0.001f)
            {
                return;
            }

            EnsureRunner()?.QueueRadialShock(centerWorld, Mathf.Clamp(strength, 0f, 2f));
        }

        /// <summary>单卡定向冲击：dirWorld 为受力方向（无需归一化）。</summary>
        public static void PlayImpulse(int uid, Vector3 dirWorld, float strength = 1f)
        {
            if (!sEnabled || strength <= 0.001f)
            {
                return;
            }

            EnsureRunner()?.QueueImpulse(uid, dirWorld, Mathf.Clamp(strength, 0f, 2f), 0f);
        }

        /// <summary>受击一颤：无明确来源方向时用的随机偏下抖动（效果伤害、陷阱等）。</summary>
        public static void PlayJolt(int uid, float strength = 1f)
        {
            if (!sEnabled || strength <= 0.001f)
            {
                return;
            }

            var dir = new Vector3(Random.Range(-1f, 1f) * 0.8f, -0.7f, 0f);
            EnsureRunner()?.QueueImpulse(uid, dir, Mathf.Clamp(strength, 0f, 2f), 0f);
        }

        /// <summary>清零所有 L1 偏移（切场景 / 关表现 / 终态校正时用）。</summary>
        public static void Reset()
        {
            sRunner?.ResetImmediate();
        }

        private static BoardCardLifeRunner EnsureRunner()
        {
            if (sRunner != null)
            {
                return sRunner;
            }

            if (!Application.isPlaying)
            {
                return null;
            }

            var host = new GameObject("~BoardCardLifeRunner");
            host.hideFlags = HideFlags.DontSave;
            Object.DontDestroyOnLoad(host);
            sRunner = host.AddComponent<BoardCardLifeRunner>();
            return sRunner;
        }
    }

    /// <summary>
    /// 场地卡生命感执行体。每帧末（LateUpdate）按格位取占格卡，写 L1 局部位移/微旋。
    /// 权威闲置位姿：L1 = (0,0,0) + identity；本类是 L1 的唯一写者。
    /// </summary>
    [DefaultExecutionOrder(15000)]
    public sealed class BoardCardLifeRunner : MonoBehaviour
    {
        private const int FirstSlot = 1;
        private const int LastSlot = 9;

        /// <summary>飘动幅度（世界单位）。32 PPU 下 0.075 ≈ 2.4 像素。</summary>
        private const float DriftAmplitudeX = 0.075f;
        private const float DriftAmplitudeY = 0.105f;

        /// <summary>飘动微旋。像素画旋转会糊边，只给"悬浮感"够用的极小角度。</summary>
        private const float DriftRollDegrees = 0.4f;

        /// <summary>悬停时放大飘动，让鼠标下的卡"更活"。</summary>
        private const float HoverDriftMultiplier = 1.45f;

        /// <summary>飘动权重淡入/淡出秒数。淡出更快 = 交战时干脆回位。</summary>
        private const float WeightRiseSeconds = 0.9f;
        private const float WeightFallSeconds = 0.28f;

        /// <summary>每卡飘动基频区间（Hz）。彼此错频才不像整块板在呼吸。</summary>
        private const float DriftHzMin = 0.30f;
        private const float DriftHzMax = 0.52f;

        /// <summary>冲击弹簧（欠阻尼）：k / c，以及"峰值位移 → 初速度"换算。</summary>
        private const float ImpulseStiffness = 85f;
        private const float ImpulseDamping = 11.5f;
        private const float ImpulseVelocityPerPeak = 14.8f;

        private const float RollStiffness = 120f;
        private const float RollDamping = 13.5f;
        private const float RollVelocityPerPeak = 17f;

        /// <summary>冲击波：影响半径、波前速度、中心峰值位移、附带微旋峰值。</summary>
        private const float ShockRadius = 6f;
        private const float ShockWaveSpeed = 26f;
        private const float ShockPeakOffset = 0.3f;
        private const float ShockPeakRollDegrees = 2.2f;
        private const float ShockFalloffExponent = 0.7f;

        /// <summary>落位沉降：卡被别的系统搬完、重新交回 L1 时给一次向下小冲击（发牌 / 跳跃 / 旋转 / 换位通吃）。</summary>
        private const float LandingPeakOffset = 0.09f;
        private const float LandingRollDegrees = 0.8f;

        /// <summary>搬动至少持续这么久才算"一次真的移动"，避免边界抖动触发假落位。</summary>
        private const float LandingMinEngagedSeconds = 0.12f;

        /// <summary>总偏移安全钳位（世界单位），防任何情况下飞出格子。</summary>
        private const float MaxTotalOffset = 0.65f;
        private const float MaxTotalRollDegrees = 6f;

        /// <summary>L0 距锚点超过此值视为"该卡正在被别的系统搬动"，L1 让位归零。</summary>
        private const float AnchorEngagedEpsilon = 0.05f;
        private const float FrameEngagedEpsilon = 0.0015f;

        private const float SettleEpsilon = 0.0004f;

        private sealed class CardLife
        {
            public ManagedCard Card;
            public CardTransformTower Tower;
            public CardVisualDriver Visual;
            public float PhaseX;
            public float PhaseY;
            public float PhaseRoll;
            public float Hz;
            public float Weight;
            public Vector2 Impulse;
            public Vector2 ImpulseVelocity;
            public float Roll;
            public float RollVelocity;
            public float PendingDelay;
            public Vector2 PendingImpulse;
            public float PendingRoll;
            public bool HasPending;
            public float EngagedSeconds;
            public bool Dirty;
            public int SeenTick;
        }

        private readonly Dictionary<int, CardLife> _lives = new();
        private readonly List<int> _stale = new();
        private int _tick = -1;

        internal void QueueRadialShock(Vector3 centerWorld, float strength)
        {
            SyncMembership();

            foreach (var life in _lives.Values)
            {
                if (!TryResolveAnchorWorld(life, out var anchorWorld))
                {
                    continue;
                }

                var delta = anchorWorld - centerWorld;
                delta.z = 0f;
                var distance = delta.magnitude;
                var falloff = Mathf.Pow(Mathf.Clamp01(1f - distance / ShockRadius), ShockFalloffExponent);
                if (falloff <= 0.02f)
                {
                    continue;
                }

                var dir = distance > 0.05f
                    ? (Vector2)(delta / distance)
                    : Random.insideUnitCircle.normalized;
                if (dir.sqrMagnitude <= 0.0001f)
                {
                    dir = Vector2.up;
                }

                var gain = strength * falloff * Mathf.Max(0f, BoardCardLifeFx.ImpulseScale);
                var peak = ShockPeakOffset * gain;
                var roll = ShockPeakRollDegrees * gain * Mathf.Sign(dir.x == 0f ? 1f : dir.x);
                Enqueue(life, dir * peak, roll, distance / ShockWaveSpeed);
            }
        }

        internal void QueueImpulse(int uid, Vector3 dirWorld, float strength, float delay)
        {
            SyncMembership();

            if (!_lives.TryGetValue(uid, out var life))
            {
                return;
            }

            var dir = new Vector2(dirWorld.x, dirWorld.y);
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.up;
            var gain = strength * Mathf.Max(0f, BoardCardLifeFx.ImpulseScale);
            Enqueue(life, dir * (ShockPeakOffset * gain), ShockPeakRollDegrees * gain * 0.6f * (dir.x >= 0f ? 1f : -1f), delay);
        }

        private static void Enqueue(CardLife life, Vector2 impulse, float roll, float delay)
        {
            if (delay <= 0.001f)
            {
                Apply(life, impulse, roll);
                return;
            }

            // 每卡只留一个待发冲击：冲击波本身稀疏，后到者覆盖先到者即可。
            life.PendingImpulse = impulse;
            life.PendingRoll = roll;
            life.PendingDelay = delay;
            life.HasPending = true;
        }

        private static void Apply(CardLife life, Vector2 impulse, float roll)
        {
            life.ImpulseVelocity += impulse * ImpulseVelocityPerPeak;
            life.RollVelocity += roll * RollVelocityPerPeak;
        }

        internal void ResetImmediate()
        {
            foreach (var life in _lives.Values)
            {
                life.Weight = 0f;
                life.Impulse = Vector2.zero;
                life.ImpulseVelocity = Vector2.zero;
                life.Roll = 0f;
                life.RollVelocity = 0f;
                life.HasPending = false;
                life.EngagedSeconds = 0f;
                WriteHome(life);
            }
        }

        private void LateUpdate()
        {
            if (!BoardCardLifeFx.Enabled)
            {
                return;
            }

            SyncMembership();
            if (_lives.Count == 0)
            {
                return;
            }

            var dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            var time = Time.unscaledTime;
            var boardCalm = IsBoardCalm();
            var driftScale = Mathf.Max(0f, BoardCardLifeFx.DriftScale);

            foreach (var life in _lives.Values)
            {
                TickCard(life, dt, time, boardCalm, driftScale);
            }
        }

        private void TickCard(CardLife life, float dt, float time, bool boardCalm, float driftScale)
        {
            if (life.Tower == null || life.Tower.BoardFrame == null)
            {
                return;
            }

            if (IsEngaged(life))
            {
                // 交战/搬动中：不做缓动，直接让位，避免与 L0/L2/L3 的权威运动叠加出双重位移。
                life.EngagedSeconds += dt;
                life.Weight = 0f;
                life.Impulse = Vector2.zero;
                life.ImpulseVelocity = Vector2.zero;
                life.Roll = 0f;
                life.RollVelocity = 0f;
                life.HasPending = false;
                WriteHome(life);
                return;
            }

            if (life.EngagedSeconds > 0f)
            {
                var landed = life.EngagedSeconds >= LandingMinEngagedSeconds;
                life.EngagedSeconds = 0f;
                if (landed && BoardCardLifeFx.Enabled)
                {
                    var gain = Mathf.Max(0f, BoardCardLifeFx.ImpulseScale);
                    Apply(
                        life,
                        new Vector2(0f, -LandingPeakOffset * gain),
                        LandingRollDegrees * gain * (life.PhaseX > Mathf.PI ? 1f : -1f));
                }
            }

            if (life.HasPending)
            {
                life.PendingDelay -= dt;
                if (life.PendingDelay <= 0f)
                {
                    Apply(life, life.PendingImpulse, life.PendingRoll);
                    life.HasPending = false;
                }
            }

            var wantsDrift = boardCalm && driftScale > 0.001f;
            var target = wantsDrift ? 1f : 0f;
            var rate = target > life.Weight ? WeightRiseSeconds : WeightFallSeconds;
            life.Weight = Mathf.MoveTowards(life.Weight, target, dt / Mathf.Max(0.0001f, rate));

            IntegrateImpulse(life, dt);

            var driftGain = life.Weight * driftScale * ResolveHoverMultiplier(life);
            var driftX = 0f;
            var driftY = 0f;
            var driftRoll = 0f;
            if (driftGain > 0.0001f)
            {
                var w = Mathf.PI * 2f * life.Hz;
                // 两组不整倍频率叠加 → 看着无规律，又完全无缝循环。
                driftX = (Mathf.Sin(time * w + life.PhaseX) * 0.65f
                          + Mathf.Sin(time * w * 1.618f + life.PhaseX * 2.3f) * 0.35f) * DriftAmplitudeX;
                driftY = (Mathf.Sin(time * w * 0.83f + life.PhaseY) * 0.6f
                          + Mathf.Sin(time * w * 1.37f + life.PhaseY * 1.7f) * 0.4f) * DriftAmplitudeY;
                driftRoll = Mathf.Sin(time * w * 0.71f + life.PhaseRoll) * DriftRollDegrees;

                driftX *= driftGain;
                driftY *= driftGain;
                driftRoll *= driftGain;
            }

            var offsetX = Mathf.Clamp(driftX + life.Impulse.x, -MaxTotalOffset, MaxTotalOffset);
            var offsetY = Mathf.Clamp(driftY + life.Impulse.y, -MaxTotalOffset, MaxTotalOffset);
            var roll = Mathf.Clamp(driftRoll + life.Roll, -MaxTotalRollDegrees, MaxTotalRollDegrees);

            var settled = Mathf.Abs(offsetX) < SettleEpsilon
                          && Mathf.Abs(offsetY) < SettleEpsilon
                          && Mathf.Abs(roll) < 0.01f;
            if (settled)
            {
                WriteHome(life);
                return;
            }

            WriteOffset(life, offsetX, offsetY, roll);
        }

        private static void IntegrateImpulse(CardLife life, float dt)
        {
            if (life.Impulse.sqrMagnitude > 1e-8f || life.ImpulseVelocity.sqrMagnitude > 1e-8f)
            {
                var accel = -ImpulseStiffness * life.Impulse - ImpulseDamping * life.ImpulseVelocity;
                life.ImpulseVelocity += accel * dt;
                life.Impulse += life.ImpulseVelocity * dt;
                if (life.Impulse.sqrMagnitude < 1e-8f && life.ImpulseVelocity.sqrMagnitude < 1e-6f)
                {
                    life.Impulse = Vector2.zero;
                    life.ImpulseVelocity = Vector2.zero;
                }
            }

            if (Mathf.Abs(life.Roll) > 1e-4f || Mathf.Abs(life.RollVelocity) > 1e-4f)
            {
                var rollAccel = -RollStiffness * life.Roll - RollDamping * life.RollVelocity;
                life.RollVelocity += rollAccel * dt;
                life.Roll += life.RollVelocity * dt;
                if (Mathf.Abs(life.Roll) < 1e-4f && Mathf.Abs(life.RollVelocity) < 1e-3f)
                {
                    life.Roll = 0f;
                    life.RollVelocity = 0f;
                }
            }
        }

        private static float ResolveHoverMultiplier(CardLife life)
        {
            if (life.Visual == null)
            {
                return 1f;
            }

            return life.Visual.CurrentTarget == CardVisualTarget.Base ? 1f : HoverDriftMultiplier;
        }

        private static void WriteOffset(CardLife life, float x, float y, float rollDegrees)
        {
            var frame = life.Tower.BoardFrame;
            var parent = frame.parent;
            var scale = parent != null ? parent.lossyScale : Vector3.one;
            var sx = Mathf.Abs(scale.x) > 0.0001f ? scale.x : 1f;
            var sy = Mathf.Abs(scale.y) > 0.0001f ? scale.y : 1f;

            // 幅度以世界单位定义（除以父级缩放），全盘像素观感一致。
            frame.localPosition = new Vector3(x / sx, y / sy, 0f);
            frame.localRotation = Quaternion.Euler(0f, 0f, rollDegrees);
            life.Dirty = true;
        }

        private static void WriteHome(CardLife life)
        {
            if (!life.Dirty)
            {
                return;
            }

            var frame = life.Tower?.BoardFrame;
            if (frame == null)
            {
                life.Dirty = false;
                return;
            }

            frame.localPosition = Vector3.zero;
            frame.localRotation = Quaternion.identity;
            life.Dirty = false;
        }

        /// <summary>棋盘整体是否处于"可以呼吸"的状态：主线空闲且场地不忙。</summary>
        private static bool IsBoardCalm()
        {
            if (NineGrid.Presentation.PresentationInputGates.MainlineBusy)
            {
                return false;
            }

            var field = GroundFieldGeometryHook.FieldOrNull();
            return field == null || !field.IsFieldBusy;
        }

        private static bool IsEngaged(CardLife life)
        {
            var card = life.Card;
            if (card?.Transform == null || card.DisplayMode != CardDisplayMode.GroundCardMode)
            {
                return true;
            }

            var tower = life.Tower;
            if (tower.SlotFrame != null && tower.SlotFrame.localPosition.sqrMagnitude > FrameEngagedEpsilon)
            {
                return true;
            }

            if (tower.EffectFrame != null && tower.EffectFrame.localPosition.sqrMagnitude > FrameEngagedEpsilon)
            {
                return true;
            }

            if (SlotFrameConvergence.IsSlotConvergenceActive(card)
                || (EffectFrameConvergence.TryGetDriver(card, out var effectDriver) && effectDriver.IsActive))
            {
                return true;
            }

            if (!TryResolveAnchorWorld(life, out var anchorWorld))
            {
                return true;
            }

            var delta = card.Transform.position - anchorWorld;
            delta.z = 0f;
            return delta.sqrMagnitude > AnchorEngagedEpsilon * AnchorEngagedEpsilon;
        }

        private static bool TryResolveAnchorWorld(CardLife life, out Vector3 anchorWorld)
        {
            anchorWorld = default;
            var field = GroundFieldGeometryHook.FieldOrNull();
            if (field == null || life.Card == null)
            {
                return false;
            }

            if (!field.TryGetSlotOf(life.Card.Uid, out var slot))
            {
                return false;
            }

            var anchor = field.GetGroundAnchor(slot);
            if (anchor == null)
            {
                return false;
            }

            anchorWorld = anchor.position;
            return true;
        }

        /// <summary>
        /// 成员同步：以场地占格（表现权威）为集合来源；离场卡先归零 L1 再摘除。
        /// </summary>
        private void SyncMembership()
        {
            if (_tick == Time.frameCount)
            {
                return;
            }

            _tick = Time.frameCount;

            var field = GroundFieldGeometryHook.FieldOrNull();
            if (field != null)
            {
                for (var slot = FirstSlot; slot <= LastSlot; slot++)
                {
                    if (!field.TryGetCardAt(slot, out var card) || card?.Transform == null)
                    {
                        continue;
                    }

                    if (_lives.TryGetValue(card.Uid, out var existing))
                    {
                        existing.Card = card;
                        existing.SeenTick = _tick;
                        continue;
                    }

                    if (!SlotFrameConvergence.TryGetTower(card, out var tower) || tower.BoardFrame == null)
                    {
                        continue;
                    }

                    _lives[card.Uid] = CreateLife(card, tower, _tick);
                }
            }

            _stale.Clear();
            foreach (var pair in _lives)
            {
                if (pair.Value.SeenTick != _tick)
                {
                    _stale.Add(pair.Key);
                }
            }

            for (var i = 0; i < _stale.Count; i++)
            {
                if (_lives.TryGetValue(_stale[i], out var life))
                {
                    WriteHome(life);
                }

                _lives.Remove(_stale[i]);
            }
        }

        private static CardLife CreateLife(ManagedCard card, CardTransformTower tower, int tick)
        {
            // 相位/频率由 uid 派生：同一张卡每局的飘动轨迹稳定，不同卡之间错开。
            var hash = card.Uid * 2654435761u;
            var a = ((hash >> 8) & 0xFFFF) / 65535f;
            var b = ((hash >> 16) & 0xFFFF) / 65535f;
            var c = ((hash >> 3) & 0xFFFF) / 65535f;

            return new CardLife
            {
                Card = card,
                Tower = tower,
                Visual = card.Transform.GetComponent<CardVisualDriver>(),
                PhaseX = a * Mathf.PI * 2f,
                PhaseY = b * Mathf.PI * 2f,
                PhaseRoll = c * Mathf.PI * 2f,
                Hz = Mathf.Lerp(DriftHzMin, DriftHzMax, (a + b) * 0.5f),
                SeenTick = tick,
            };
        }

        private void OnDisable()
        {
            ResetImmediate();
        }
    }
}
