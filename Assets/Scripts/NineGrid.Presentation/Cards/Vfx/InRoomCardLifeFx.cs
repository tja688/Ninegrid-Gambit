using System.Collections.Generic;
using NineGrid.Cards.Convergence;
using NineGrid.Flow.AttributeBoard;
using NineGrid.Flow.RewardBoard;
using NineGrid.Flow.ShopBoard;
using NineGrid.Flow.TavernBoard;
using UnityEngine;

namespace NineGrid.Cards.Vfx
{
    /// <summary>
    /// 房内板面"活着"的表现层：商店货架 / 卡店候选与服务 / 奖励货架 / 属性板候选
    /// 持续呈现的卡牌状对象做低频悬浮飘动（ADR-0051 家族扩展）。
    /// 纯装饰：不读写 Core 规则状态、不参与 Batch-ack、不改货架/候选权威（L0/L2/L3 一律不碰）。
    /// 真卡写独占 L1 BoardFrame（塔型统一，收敛动画期间让位归零）；
    /// 非塔型选项卡对象（RoomOptionFacePrefab 系：道具牌格升级 / 卡店服务）退化为
    /// 对根 localPosition 的增量偏移——不存基准快照，外部 tween 搬动/对齐可跟随，归零时精确回写。
    /// 刷新 / 离开等纯图标按钮不纳入（非卡牌，保持静止）。
    /// </summary>
    public static class InRoomCardLifeFx
    {
        /// <summary>全局开关。关闭后所有偏移立刻归零。</summary>
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
        private static InRoomCardLifeRunner sRunner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            sRunner = null;
            sEnabled = true;
            DriftScale = 1f;
        }

        /// <summary>
        /// 悬浮飘动是常驻表现，不能等第一次进房才建 Runner——这里主动起一次。
        /// 无房会话时 Runner 每帧只做一次空成员同步，代价可忽略。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EnsureRunner();
        }

        /// <summary>清零所有偏移（切场景 / 关表现时用）。</summary>
        public static void Reset()
        {
            sRunner?.ResetImmediate();
        }

        private static InRoomCardLifeRunner EnsureRunner()
        {
            if (sRunner != null)
            {
                return sRunner;
            }

            if (!Application.isPlaying)
            {
                return null;
            }

            var host = new GameObject("~InRoomCardLifeRunner");
            host.hideFlags = HideFlags.DontSave;
            Object.DontDestroyOnLoad(host);
            sRunner = host.AddComponent<InRoomCardLifeRunner>();
            return sRunner;
        }
    }

    /// <summary>
    /// 房内板面生命感执行体。每帧末（LateUpdate）聚合四个房 Presenter 当前货架/候选集合，
    /// 真卡写 L1 局部位移/微旋，选项卡对象写根 localPosition 增量偏移。
    /// 权威闲置位姿：L1 = (0,0,0) + identity；选项卡 = 外部搬动后的当前位置。
    /// 与手牌层（HandCardLifeFx）、场地卡层（BoardCardLifeFx）成员集合互不重叠。
    /// </summary>
    [DefaultExecutionOrder(15000)]
    public sealed class InRoomCardLifeRunner : MonoBehaviour
    {
        /// <summary>飘动幅度（世界单位）。32 PPU 下 0.12 ≈ 3.8 像素。房内卡是玩家注视焦点，与手牌同档。</summary>
        private const float DriftAmplitudeX = 0.09f;
        private const float DriftAmplitudeY = 0.12f;

        /// <summary>真卡飘动微旋。像素画旋转会糊边，只给"悬浮感"够用的极小角度；选项卡不旋转。</summary>
        private const float DriftRollDegrees = 0.4f;

        /// <summary>飘动权重淡入/淡出秒数。淡出更快 = 收敛/搬动时干脆让位。</summary>
        private const float WeightRiseSeconds = 0.9f;
        private const float WeightFallSeconds = 0.28f;

        /// <summary>每卡飘动基频区间（Hz）。彼此错频才不像整排货架在一起呼吸。</summary>
        private const float DriftHzMin = 0.30f;
        private const float DriftHzMax = 0.52f;

        /// <summary>L2/L3 局部位移超过此值视为"正被收敛/搬动"，L1 让位归零。</summary>
        private const float FrameEngagedEpsilon = 0.0015f;

        private const float SettleEpsilon = 0.0004f;

        private sealed class CardLife
        {
            public ManagedCard Card;
            public CardTransformTower Tower;
            public float PhaseX;
            public float PhaseY;
            public float PhaseRoll;
            public float Hz;
            public float Weight;
            public bool Dirty;
            public int SeenTick;
        }

        private sealed class GoLife
        {
            public Transform Go;
            public float PhaseX;
            public float PhaseY;
            public float Hz;
            public float Weight;
            public Vector2 LastOffset;
            public bool Dirty;
            public int SeenTick;
        }

        private readonly Dictionary<int, CardLife> _cardLives = new();
        private readonly Dictionary<int, GoLife> _goLives = new();
        private readonly List<int> _stale = new();
        private int _tick = -1;

        internal void ResetImmediate()
        {
            foreach (var life in _cardLives.Values)
            {
                life.Weight = 0f;
                WriteHome(life);
            }

            foreach (var life in _goLives.Values)
            {
                life.Weight = 0f;
                WriteGoHome(life);
            }
        }

        private void LateUpdate()
        {
            if (!InRoomCardLifeFx.Enabled)
            {
                return;
            }

            SyncMembership();
            if (_cardLives.Count == 0 && _goLives.Count == 0)
            {
                return;
            }

            var dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            var time = Time.unscaledTime;
            var driftScale = Mathf.Max(0f, InRoomCardLifeFx.DriftScale);

            foreach (var life in _cardLives.Values)
            {
                TickCard(life, dt, time, driftScale);
            }

            foreach (var life in _goLives.Values)
            {
                TickGo(life, dt, time, driftScale);
            }
        }

        private void TickCard(CardLife life, float dt, float time, float driftScale)
        {
            if (life.Tower == null || life.Tower.BoardFrame == null)
            {
                return;
            }

            if (IsEngaged(life))
            {
                // 收敛/搬动中：不做缓动，直接让位，避免与 L2/L3 的权威运动叠加出双重位移。
                life.Weight = 0f;
                WriteHome(life);
                return;
            }

            // 飘动常驻：不被主线/场地门禁暂停（ADR-0051 修订 2026-08-14 家族纪律）。权重只在 DriftScale 归零时回落。
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

        private void TickGo(GoLife life, float dt, float time, float driftScale)
        {
            if (life.Go == null)
            {
                return;
            }

            var wantsDrift = driftScale > 0.001f;
            var target = wantsDrift ? 1f : 0f;
            var rate = target > life.Weight ? WeightRiseSeconds : WeightFallSeconds;
            life.Weight = Mathf.MoveTowards(life.Weight, target, dt / Mathf.Max(0.0001f, rate));

            var driftGain = life.Weight * driftScale;
            var driftX = 0f;
            var driftY = 0f;
            if (driftGain > 0.0001f)
            {
                var w = Mathf.PI * 2f * life.Hz;
                driftX = (Mathf.Sin(time * w + life.PhaseX) * 0.65f
                          + Mathf.Sin(time * w * 1.618f + life.PhaseX * 2.3f) * 0.35f) * DriftAmplitudeX;
                driftY = (Mathf.Sin(time * w * 0.83f + life.PhaseY) * 0.6f
                          + Mathf.Sin(time * w * 1.37f + life.PhaseY * 1.7f) * 0.4f) * DriftAmplitudeY;

                driftX *= driftGain;
                driftY *= driftGain;
            }

            var settled = Mathf.Abs(driftX) < SettleEpsilon && Mathf.Abs(driftY) < SettleEpsilon;
            if (settled)
            {
                WriteGoHome(life);
                return;
            }

            // 增量基准：上一帧偏移已叠加在 localPosition 上，反推基准 → 外部 tween（MoveToWorld 等）
            // 搬动位置时下一帧自动跟随，不存基准快照、无回弹。
            var baseLocal = life.Go.localPosition - (Vector3)life.LastOffset;
            life.Go.localPosition = new Vector3(baseLocal.x + driftX, baseLocal.y + driftY, baseLocal.z);
            life.LastOffset = new Vector2(driftX, driftY);
            life.Dirty = true;
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

        private static void WriteGoHome(GoLife life)
        {
            if (!life.Dirty || life.Go == null)
            {
                life.Dirty = false;
                return;
            }

            life.Go.localPosition -= (Vector3)life.LastOffset;
            life.LastOffset = Vector2.zero;
            life.Dirty = false;
        }

        private static bool IsEngaged(CardLife life)
        {
            var card = life.Card;
            if (card?.Transform == null || card.DisplayMode != CardDisplayMode.GroundCardMode)
            {
                return true;
            }

            var tower = life.Tower;
            if (tower.SlotFrame != null
                && tower.SlotFrame.localPosition.sqrMagnitude > FrameEngagedEpsilon * FrameEngagedEpsilon)
            {
                return true;
            }

            if (tower.EffectFrame != null
                && tower.EffectFrame.localPosition.sqrMagnitude > FrameEngagedEpsilon * FrameEngagedEpsilon)
            {
                return true;
            }

            return SlotFrameConvergence.IsSlotConvergenceActive(card);
        }

        /// <summary>
        /// 成员同步：聚合四个房 Presenter 的当前货架/候选集合（表现权威）。
        /// 移出集合的对象先归零再摘除；卡被接走（DisplayMode 变更）由 IsEngaged 自然让位，
        /// 离开集合后由 stale 清理摘除。
        /// </summary>
        private void SyncMembership()
        {
            if (_tick == Time.frameCount)
            {
                return;
            }

            _tick = Time.frameCount;

            var shop = ShopBoardPresenter.Current;
            CollectCardList(shop.ShelfCards);
            CollectGoList(shop.ShelfOptionGos);

            var reward = RewardBoardPresenter.Current;
            CollectCardList(reward.ShelfCards);

            var tavern = TavernBoardPresenter.Current;
            CollectCardList(tavern.CandidateCards);
            CollectGoList(tavern.ServiceGos);

            var attribute = AttributeBoardPresenter.Current;
            CollectCardList(attribute.CandidateCards);

            _stale.Clear();
            foreach (var pair in _cardLives)
            {
                if (pair.Value.SeenTick != _tick)
                {
                    _stale.Add(pair.Key);
                }
            }

            for (var i = 0; i < _stale.Count; i++)
            {
                if (_cardLives.TryGetValue(_stale[i], out var life))
                {
                    WriteHome(life);
                }

                _cardLives.Remove(_stale[i]);
            }

            _stale.Clear();
            foreach (var pair in _goLives)
            {
                if (pair.Value.SeenTick != _tick)
                {
                    _stale.Add(pair.Key);
                }
            }

            for (var i = 0; i < _stale.Count; i++)
            {
                if (_goLives.TryGetValue(_stale[i], out var life))
                {
                    WriteGoHome(life);
                }

                _goLives.Remove(_stale[i]);
            }
        }

        private void CollectCardList(IReadOnlyList<ManagedCard> cards)
        {
            if (cards == null)
            {
                return;
            }

            for (var i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (card?.Transform == null)
                {
                    continue;
                }

                if (_cardLives.TryGetValue(card.Uid, out var existing))
                {
                    existing.Card = card;
                    existing.SeenTick = _tick;
                    continue;
                }

                if (!SlotFrameConvergence.TryGetTower(card, out var tower) || tower.BoardFrame == null)
                {
                    continue;
                }

                _cardLives[card.Uid] = CreateCardLife(card, tower, _tick);
            }
        }

        private void CollectGoList(IReadOnlyList<GameObject> gos)
        {
            if (gos == null)
            {
                return;
            }

            for (var i = 0; i < gos.Count; i++)
            {
                var go = gos[i];
                if (go == null)
                {
                    continue;
                }

                var id = go.GetInstanceID();
                if (_goLives.TryGetValue(id, out var existing))
                {
                    existing.Go = go.transform;
                    existing.SeenTick = _tick;
                    continue;
                }

                _goLives[id] = CreateGoLife(go.transform, _tick);
            }
        }

        private static CardLife CreateCardLife(ManagedCard card, CardTransformTower tower, int tick)
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
                PhaseX = a * Mathf.PI * 2f,
                PhaseY = b * Mathf.PI * 2f,
                PhaseRoll = c * Mathf.PI * 2f,
                Hz = Mathf.Lerp(DriftHzMin, DriftHzMax, (a + b) * 0.5f),
                // 首帧强制归零一次：入场前残留的 L1 偏移在接管时清掉。
                Dirty = true,
                SeenTick = tick,
            };
        }

        private static GoLife CreateGoLife(Transform go, int tick)
        {
            var hash = (uint)go.GetInstanceID() * 2654435761u;
            var a = ((hash >> 8) & 0xFFFF) / 65535f;
            var b = ((hash >> 16) & 0xFFFF) / 65535f;

            return new GoLife
            {
                Go = go,
                PhaseX = a * Mathf.PI * 2f,
                PhaseY = b * Mathf.PI * 2f,
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
