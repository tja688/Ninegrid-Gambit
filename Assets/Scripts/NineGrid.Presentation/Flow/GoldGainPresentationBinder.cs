using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Flow.Diagnostics;
using NineGrid.Flow.Presentation;
using QFramework;
using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 消费 <see cref="GoldGainPresentationRequested"/>：驱动飞币/HUD，并写 FlowTrace。
    /// UI/FX 单向消费，不回写 PlayerModel。
    /// </summary>
    public sealed class GoldGainPresentationBinder : MonoBehaviour
    {
        private static GoldGainPresentationBinder sInstance;
        private IUnRegister mEventUnRegister;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            sInstance = null;
        }

        public static void EnsureInstalled()
        {
            if (sInstance != null)
            {
                return;
            }

            var existing = FindFirstObjectByType<GoldGainPresentationBinder>();
            if (existing != null)
            {
                sInstance = existing;
                return;
            }

            var host = new GameObject(nameof(GoldGainPresentationBinder));
            sInstance = host.AddComponent<GoldGainPresentationBinder>();
        }

        private void Awake()
        {
            if (sInstance != null && sInstance != this)
            {
                Destroy(gameObject);
                return;
            }

            sInstance = this;
            RegisterEvents();
        }

        private void OnDestroy()
        {
            UnregisterEvents();
            if (sInstance == this)
            {
                sInstance = null;
            }
        }

        private void RegisterEvents()
        {
            UnregisterEvents();
            var arch = NineGridArchitecture.Interface;
            if (arch == null)
            {
                return;
            }

            mEventUnRegister = arch.RegisterEvent<GoldGainPresentationRequested>(OnGoldGainPresentationRequested);
        }

        private void UnregisterEvents()
        {
            mEventUnRegister?.UnRegister();
            mEventUnRegister = null;
        }

        private static void OnGoldGainPresentationRequested(GoldGainPresentationRequested e)
        {
            FlowRoomEconomyAudioCues.PulseGoldPresentation(
                e.IsSpend,
                "GoldGainPresentationBinder.OnGoldGainPresentationRequested");
            var goldFx = UnityEngine.Object.FindFirstObjectByType<GoldGainFxManagerSingleton>();
            if (e.IsSpend)
            {
                goldFx?.SnapToCore(e.AmountAfter);
                RecordGoldChangedFlow(
                    FlowTraceNames.GoldSpent,
                    e.Delta,
                    e.AmountAfter,
                    e.Reason,
                    e.SourceDefId,
                    e.ActionName);
                return;
            }

            goldFx?.PlayGain(e.Delta, e.AmountAfter, e.OriginWorld);
            RecordGoldChangedFlow(
                FlowTraceNames.GoldGained,
                e.Delta,
                e.AmountAfter,
                e.Reason,
                e.SourceDefId,
                e.ActionName);
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
