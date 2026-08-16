using System;
using System.Collections.Generic;
using NineGrid.Content.Vfx;
using NineGrid.Core;
using NineGrid.Flow.Diagnostics;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 消费 <see cref="GoldGainPresentationRequested"/>：发稳定 gold-flight VFX Cue、驱动 HUD 数字窗，并写 FlowTrace。
    /// UI/FX 单向消费，不回写 PlayerModel；VFX 失败时 HUD 立即收敛，不占主线 ack。
    /// </summary>
    public sealed class GoldGainPresentationBinder : MonoBehaviour
    {
        private const string GoldFlightSemanticRole = "gold-flight";

        /// <summary>金币盔甲代偿的 GoldModified 事件 Message 标记（CoreActions 同源）。</summary>
        private const string GoldArmorSpendReason = "goldArmor";

        private static GoldGainPresentationBinder sInstance;
        private IUnRegister mEventUnRegister;
        private bool mRetrySubscriptionPending;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            sInstance = null;
        }

        /// <summary>
        /// 确保存在且已订阅：已有实例也补订阅；架构暂不可用时延后重试，避免事件发出时无人听。
        /// </summary>
        public static void EnsureInstalled()
        {
            if (sInstance == null)
            {
                var existing = FindFirstObjectByType<GoldGainPresentationBinder>();
                if (existing != null)
                {
                    sInstance = existing;
                }
                else
                {
                    var host = new GameObject(nameof(GoldGainPresentationBinder));
                    sInstance = host.AddComponent<GoldGainPresentationBinder>();
                }
            }

            sInstance.EnsureSubscribed();
        }

        /// <summary>
        /// 直连增益视觉（未用帮助卡 / DevTest 等）：发 gold-flight VFX + HUD 数字窗 + 增益音效，不写 FlowTrace。
        /// 清关变卖残留道具卡入账时 isVictorySettlement=true，命中「只留胜利结算哗啦啦」开关。
        /// </summary>
        public static VfxCueResult PresentGainVisual(
            int delta,
            int amountAfter,
            Vector3? originWorld,
            bool isVictorySettlement = false)
        {
            GoldCoinGainClatter.Pulse(
                delta,
                isVictorySettlement,
                "GoldGainPresentationBinder.PresentGainVisual");
            return RequestGoldFlightAndDriveHud(delta, amountAfter, originWorld);
        }

        private void Awake()
        {
            if (sInstance != null && sInstance != this)
            {
                Destroy(gameObject);
                return;
            }

            sInstance = this;
            EnsureSubscribed();
        }

        private void Update()
        {
            if (mRetrySubscriptionPending)
            {
                EnsureSubscribed();
            }
        }

        private void OnDestroy()
        {
            UnregisterEvents();
            if (sInstance == this)
            {
                sInstance = null;
            }
        }

        private void EnsureSubscribed()
        {
            if (mEventUnRegister != null)
            {
                mRetrySubscriptionPending = false;
                return;
            }

            RegisterEvents();
        }

        private void RegisterEvents()
        {
            UnregisterEvents();
            var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            if (arch == null)
            {
                // 架构暂不可用：挂起重试，不静默永久丢失订阅。
                mRetrySubscriptionPending = true;
                return;
            }

            mRetrySubscriptionPending = false;
            mEventUnRegister = arch.RegisterEvent<GoldGainPresentationRequested>(OnGoldGainPresentationRequested);
        }

        private void UnregisterEvents()
        {
            mEventUnRegister?.UnRegister();
            mEventUnRegister = null;
        }

        private static void OnGoldGainPresentationRequested(GoldGainPresentationRequested e)
        {
            if (e.IsSpend)
            {
                FlowRoomEconomyAudioCues.PulseGoldPresentation(
                    isSpend: true,
                    "GoldGainPresentationBinder.OnGoldGainPresentationRequested");
                var hud = PlayerInfoHudPresenter.TryGetInstance();
                hud?.SnapGold(e.AmountAfter);
                GoldHudDomainHost.Instance?.SnapIconToBase();
                if (string.Equals(e.Reason, GoldArmorSpendReason, StringComparison.Ordinal)
                    && e.OriginWorld.HasValue)
                {
                    PulseGoldArmorSpendFlight(e.OriginWorld.Value, Mathf.Abs(e.Delta));
                }

                RecordGoldChangedFlow(
                    FlowTraceNames.GoldSpent,
                    e.Delta,
                    e.AmountAfter,
                    e.Reason,
                    e.SourceDefId,
                    e.ActionName);
                return;
            }

            GoldCoinGainClatter.Pulse(
                e.Delta,
                isVictorySettlement: false,
                "GoldGainPresentationBinder.OnGoldGainPresentationRequested");
            RequestGoldFlightAndDriveHud(e.Delta, e.AmountAfter, e.OriginWorld);
            RecordGoldChangedFlow(
                FlowTraceNames.GoldGained,
                e.Delta,
                e.AmountAfter,
                e.Reason,
                e.SourceDefId,
                e.ActionName);
        }

        private static VfxCueResult RequestGoldFlightAndDriveHud(
            int delta,
            int amountAfter,
            Vector3? originWorld)
        {
            amountAfter = Mathf.Max(0, amountAfter);
            var hud = PlayerInfoHudPresenter.TryGetInstance();
            var gainDelta = Mathf.Max(0, delta);
            if (gainDelta <= 0)
            {
                hud?.SnapGold(amountAfter);
                return new VfxCueResult
                {
                    Outcome = VfxCueOutcome.Played,
                    CueId = GoldGainVfxCues.FlyIn,
                    PresentationPlan = VfxPresentationPlan.None,
                };
            }

            var spatial = new VfxSpatialContext(
                GoldFlightSemanticRole,
                GoldHudDomainHost.Instance,
                originWorld,
                diagnosticOwnerUid: 0,
                amount: gainDelta);

            var result = TriggerPulseHub.PulseVfx(
                VfxCueRequest.Simple(
                    GoldGainVfxCues.FlyIn,
                    "GoldGainPresentationBinder.RequestGoldFlightAndDriveHud"),
                spatial);

            if (result != null && result.HasPresentationPlan)
            {
                hud?.PresentGoldGainWindow(gainDelta, amountAfter, result.PresentationPlan);
            }
            else
            {
                // 播放器创建失败：HUD 立即收敛；VFX issue 已由 Runtime 记录，不等待主线。
                hud?.SnapGold(amountAfter);
            }

            return result;
        }

        /// <summary>
        /// 金币盔甲代偿的受击位置反馈：从受击卡到金币 HUD 的短途金币飞损脉冲（纯装饰，
        /// 不占主线 ack；VFX 失败按既有金币表现策略降级——HUD SnapGold 已先行收敛）。
        /// 复用 gold-flight 播放器：PositionSnapshot=受击位置，DomainHost=金币 HUD 终点。
        /// </summary>
        private static void PulseGoldArmorSpendFlight(Vector3 originWorld, int goldSpent)
        {
            var spatial = new VfxSpatialContext(
                GoldFlightSemanticRole,
                GoldHudDomainHost.Instance,
                originWorld,
                diagnosticOwnerUid: 0,
                amount: goldSpent);
            TriggerPulseHub.PulseVfx(
                VfxCueRequest.Simple(
                    GoldGainVfxCues.FlyIn,
                    "GoldGainPresentationBinder.PulseGoldArmorSpendFlight"),
                spatial);
        }

        public static void RecordGoldChangedFlow(
            string eventName,
            int delta,
            int amountAfter,
            string reason,
            string sourceDefId,
            string actionName)
        {
            try
            {
                if (!FlowTraceRecorder.Enabled)
                {
                    return;
                }

                FlowTraceRecorder.BeginSessionIfNeeded();
                FlowTraceRecorder.Record(
                    FlowTraceCategory.Economy,
                    eventName,
                    new Dictionary<string, string>
                    {
                        { "delta", delta.ToString() },
                        { "amountAfter", amountAfter.ToString() },
                        { "reason", reason ?? string.Empty },
                        { "sourceDefId", sourceDefId ?? string.Empty },
                        { "action", actionName ?? string.Empty },
                    },
                    refBattleOpIndex: BattleTraceRecorder.LastOpIndex);
            }
            catch
            {
                // 诊断失败不阻塞主线
            }
        }
    }
}
