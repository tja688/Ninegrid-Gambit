using System.Collections.Generic;
using NineGrid.Cards.Convergence;
using UnityEngine;

namespace NineGrid.Cards.Vfx
{
    /// <summary>
    /// 手牌"活着"的表现层：独占 L1 BoardFrame，做低频悬浮飘动（ADR-0051 补记）。
    /// 纯装饰：不读写 Core 规则状态、不参与 Batch-ack、不改手牌槽权威（L0/L2/L3 一律不碰）。
    /// 飘动常驻非阻塞（修订 2026-08-14）：主线表演 / 场地忙碌期间照常飘动，不受编排门禁暂停——
    /// 唯一特例是鼠标正指向（hover 弹出）的卡，随 hover 让位归零；拖拽 / ripple 搬动同样自动归零，回位后飘动恢复。
    /// 命中判定不受影响：手牌 hover 以槽位布局坐标为基准（CardHandSlotContainer.GetLayoutPosition），
    /// 不看卡的当前世界位置，飘动只改可见位置（约 3–4 像素）。
    /// </summary>
    public static class HandCardLifeFx
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

        /// <summary>悬浮飘动幅度倍率（0 = 关闭飘动，1 = 默认）。</summary>
        public static float DriftScale { get; set; } = 1f;

        private static bool sEnabled = true;
        private static HandCardLifeRunner sRunner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            sRunner = null;
            sEnabled = true;
            DriftScale = 1f;
        }

        /// <summary>
        /// 悬浮飘动是常驻表现，不能等第一次交互才建 Runner——这里主动起一次。
        /// 无手牌时 Runner 每帧只做一次空成员同步，代价可忽略。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EnsureRunner();
        }

        /// <summary>清零所有 L1 偏移（切场景 / 关表现 / 终态校正时用）。</summary>
        public static void Reset()
        {
            sRunner?.ResetImmediate();
        }

        private static HandCardLifeRunner EnsureRunner()
        {
            if (sRunner != null)
            {
                return sRunner;
            }

            if (!Application.isPlaying)
            {
                return null;
            }

            var host = new GameObject("~HandCardLifeRunner");
            host.hideFlags = HideFlags.DontSave;
            Object.DontDestroyOnLoad(host);
            sRunner = host.AddComponent<HandCardLifeRunner>();
            return sRunner;
        }
    }

    /// <summary>
    /// 手牌生命感执行体。每帧末（LateUpdate）按手牌槽位取卡，写 L1 局部位移/微旋。
    /// 权威闲置位姿：L1 = (0,0,0) + identity；本类是"手牌槽内卡"的 L1 唯一写者，
    /// 与场地卡生命层（BoardCardLifeFx，只收占格卡）和手牌拖拽期惯性倾斜（拖拽卡已移出槽）
    /// 三个成员集合互不重叠。
    /// </summary>
    [DefaultExecutionOrder(15000)]
    public sealed class HandCardLifeRunner : MonoBehaviour
    {
        /// <summary>飘动幅度（世界单位）。32 PPU 下 0.12 ≈ 3.8 像素。手牌是玩家注视焦点，略强于场地卡的 2–3 像素。</summary>
        private const float DriftAmplitudeX = 0.09f;
        private const float DriftAmplitudeY = 0.12f;

        /// <summary>飘动微旋。像素画旋转会糊边，只给"悬浮感"够用的极小角度。</summary>
        private const float DriftRollDegrees = 0.4f;

        /// <summary>飘动权重淡入/淡出秒数。淡出更快 = hover 弹出/被搬动时干脆让位。</summary>
        private const float WeightRiseSeconds = 0.9f;
        private const float WeightFallSeconds = 0.28f;

        /// <summary>每卡飘动基频区间（Hz）。彼此错频才不像整排手牌在一起呼吸。</summary>
        private const float DriftHzMin = 0.30f;
        private const float DriftHzMax = 0.52f;

        /// <summary>
        /// L0 距手牌锚点超过此值视为"正在被别的系统搬动 / hover 弹出"，L1 让位归零。
        /// hover 上浮 0.35、ripple 飞行全程都远超此值；静态卡 L0 与锚点严格重合。
        /// </summary>
        private const float AnchorEngagedEpsilon = 0.05f;

        /// <summary>L0 帧间位移低于此值视为"搬动已停"（hover 退出动画被中断后的静止残留）。</summary>
        private const float StillEpsilon = 0.0002f;

        /// <summary>
        /// 离锚且静止超过此秒数判定为"搬动中断残留"，自愈恢复浮动——
        /// 否则 hover 退出动画被 KillMotion 掐断 / base 缓存过期时，卡停在非锚点即永久让位（死卡）。
        /// 0.25s 远长于任何搬动动画末段的速度低谷，不会误判真搬动。
        /// </summary>
        private const float StuckGraceSeconds = 0.25f;

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
            public bool Dirty;
            public int SeenTick;
            public Vector3 LastWorldPos;
            public float StillSeconds;
        }

        private readonly Dictionary<int, CardLife> _lives = new();
        private readonly List<int> _stale = new();
        private int _tick = -1;

        internal void ResetImmediate()
        {
            foreach (var life in _lives.Values)
            {
                life.Weight = 0f;
                WriteHome(life);
            }
        }

        private void LateUpdate()
        {
            if (!HandCardLifeFx.Enabled)
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
            var driftScale = Mathf.Max(0f, HandCardLifeFx.DriftScale);

            foreach (var life in _lives.Values)
            {
                TickCard(life, dt, time, driftScale);
            }
        }

        private void TickCard(CardLife life, float dt, float time, float driftScale)
        {
            if (life.Tower == null || life.Tower.BoardFrame == null)
            {
                return;
            }

            if (IsEngaged(life, dt))
            {
                // hover 弹出 / 置顶显示 / 被 ripple 搬动 / 模式切换：L1 当帧硬归零（不缓动）。
                // 与场地层同一纪律——权威运动接手时装饰让位，避免叠加出双重位移。
                life.Weight = 0f;
                WriteHome(life);
                return;
            }

            // 飘动常驻：不被主线/场地门禁暂停（修订 2026-08-14）。权重只在 DriftScale 归零时回落。
            var wantsDrift = driftScale > 0.001f;
            var target = wantsDrift ? 1f : 0f;
            var rate = target > life.Weight ? WeightRiseSeconds : WeightFallSeconds;
            life.Weight = Mathf.MoveTowards(life.Weight, target, dt / Mathf.Max(0.0001f, rate));

            var driftGain = life.Weight * driftScale;
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

            var settled = Mathf.Abs(driftX) < SettleEpsilon
                          && Mathf.Abs(driftY) < SettleEpsilon
                          && Mathf.Abs(driftRoll) < 0.01f;
            if (settled)
            {
                WriteHome(life);
                return;
            }

            WriteOffset(life, driftX, driftY, driftRoll);
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

        /// <summary>
        /// 逐卡"正被权威运动接手"判定（带静止自愈，2026-08-14 强化）：
        /// 让位只服务"正在动的卡"；卡静止在非锚点（hover 退出动画被掐断、base 缓存过期、ripple 中断）
        /// 视为搬动残留——超过宽限秒数后自愈恢复浮动，避免永久让位成死卡。
        /// 唯一特例：鼠标正指向（hover / selected 置顶）的卡保持让位，不计入自愈。
        /// </summary>
        private bool IsEngaged(CardLife life, float dt)
        {
            var card = life.Card;
            if (card?.Transform == null || card.DisplayMode != CardDisplayMode.HandCardMode)
            {
                return true;
            }

            if (!TryResolveAnchorWorld(life, out var anchorWorld))
            {
                // 锚点解析失败（卡暂离槽容器 / 管理器过渡）：以当前位置为锚，不误判为搬动；
                // 卡离槽由 SyncMembership 摘除，此回退只覆盖极短的过渡窗口。
                life.LastWorldPos = card.Transform.position;
                life.StillSeconds = 0f;
                return false;
            }

            var current = card.Transform.position;
            var delta = current - anchorWorld;
            delta.z = 0f;
            if (delta.sqrMagnitude <= AnchorEngagedEpsilon * AnchorEngagedEpsilon)
            {
                life.LastWorldPos = current;
                life.StillSeconds = 0f;
                return false;
            }

            // 指针占用（hover 上浮 / selected 置顶）：保持让位，不累计静止——用户要求的唯一特例。
            if (life.Visual != null
                && (life.Visual.CurrentTarget == CardVisualTarget.Hover
                    || life.Visual.CurrentTarget == CardVisualTarget.Selected))
            {
                life.LastWorldPos = current;
                life.StillSeconds = 0f;
                return true;
            }

            // L0 仍在动 → 真搬动（ripple / hop / 回位动画），让位。
            if ((current - life.LastWorldPos).sqrMagnitude > StillEpsilon * StillEpsilon)
            {
                life.LastWorldPos = current;
                life.StillSeconds = 0f;
                return true;
            }

            // 离锚但静止：累计静止时长，超过宽限视为搬动中断残留，自愈恢复浮动。
            life.StillSeconds += dt;
            if (life.StillSeconds < StuckGraceSeconds)
            {
                return true;
            }

            life.StillSeconds = StuckGraceSeconds;
            life.LastWorldPos = current;
            return false;
        }

        private static bool TryResolveAnchorWorld(CardLife life, out Vector3 anchorWorld)
        {
            anchorWorld = default;
            var hand = CardEntityLifecycleHook.HandOrNull();
            if (hand == null || life.Card == null)
            {
                return false;
            }

            return hand.TryGetHandLayoutWorldPosition(life.Card, out anchorWorld);
        }

        /// <summary>
        /// 成员同步：以手牌槽容器（表现权威）为集合来源。
        /// 离槽卡只摘除不写回——拖拽中的卡已被移出手牌槽、L1 由拖拽惯性倾斜/拾起挤压独占，
        /// 写回会与拖拽 L1 写者互踩；残余偏移在卡重新入槽后由首帧 WriteHome（Dirty 置位）清掉。
        /// </summary>
        private void SyncMembership()
        {
            if (_tick == Time.frameCount)
            {
                return;
            }

            _tick = Time.frameCount;

            var hand = CardEntityLifecycleHook.HandOrNull();
            if (hand != null)
            {
                hand.ForEachHandSlotCard(CollectCard);
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
                _lives.Remove(_stale[i]);
            }
        }

        private void CollectCard(int slotIndex, ManagedCard card)
        {
            if (card?.Transform == null)
            {
                return;
            }

            if (_lives.TryGetValue(card.Uid, out var existing))
            {
                existing.Card = card;
                existing.SeenTick = _tick;
                return;
            }

            if (!SlotFrameConvergence.TryGetTower(card, out var tower) || tower.BoardFrame == null)
            {
                return;
            }

            _lives[card.Uid] = CreateLife(card, tower, _tick);
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
                // 首帧强制归零一次：拖拽回位 / 异常对齐等残留的 L1 偏移在重新入槽时清掉，
                // 避免新 CardLife 权重为 0 时因 Dirty=false 跳过 WriteHome 留下残影。
                Dirty = true,
                SeenTick = tick,
                LastWorldPos = card.Transform.position,
            };
        }

        private void OnDisable()
        {
            ResetImmediate();
        }
    }
}
