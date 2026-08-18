#if UNITY_EDITOR || DEVELOPMENT_BUILD

using Cysharp.Threading.Tasks;
using NineGrid.Core;
using NineGrid.DevTest.Commands;
using NineGrid.Flow;
using NineGrid.Flow.Diagnostics;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.DevTest.Flow
{
    [DisallowMultipleComponent]
    public sealed class InBattleManagerDevKeys : TestKeyModuleBehaviour
    {
        private const int CheatAvatarHp = 99;

        [Tooltip("运行时查找场景中的 BattleSessionController；也可手动拖入覆盖。")]
        [SerializeField] private BattleSessionController inBattleManager;

        protected override string ModuleId => "in-battle-manager";

        protected override void OnEnable()
        {
            if (inBattleManager == null)
            {
                inBattleManager = GetComponent<BattleSessionController>();
            }

            if (inBattleManager == null)
            {
                inBattleManager = UnityEngine.Object.FindFirstObjectByType<BattleSessionController>();
            }

            base.OnEnable();
        }

        private void Update()
        {
            PollQuickTestDigitKeys();
        }

        private static void PollQuickTestDigitKeys()
        {
            var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            if (arch == null)
            {
                return;
            }

            var shell = arch.GetSystem<IGameFlowShellSystem>();
            if (shell == null || !shell.IsQuickTestMode)
            {
                return;
            }

            for (var i = 0; i <= 9; i++)
            {
                var alpha = KeyCode.Alpha0 + i;
                var keypad = KeyCode.Keypad0 + i;
                if (KeyboardUtility.GetKeyDown(alpha) || KeyboardUtility.GetKeyDown(keypad))
                {
                    if (QuickTestProjectileEffectState.TrySetActiveIndex(i, out var presetId, out var name))
                    {
                        Debug.Log($"[QuickTest] 顺劈斧测试弹道切换为 [{i}]: {presetId} ({name})");
                    }
                    break;
                }
            }
        }

        protected override void ConfigureBindings(TestKeyRegistrationBuilder builder)
        {
            builder
                .Bind(KeyCode.Keypad1, "真实局内入场", () => RunRealBattleEntryAsync().Forget())
                .Bind(KeyCode.Keypad2, "探测节点结算", () => TrySettlement())
                .Bind(KeyCode.Keypad3, "玩家血量=99", CheatAvatarHpTo99)
                .Bind(KeyCode.Keypad4, "立即导出 Battle+FlowLog", ExportBattleAndFlowTrace)
                .Bind(KeyCode.Keypad5, "开关 Battle/FlowTrace", ToggleBattleTrace)
                .Bind(KeyCode.Keypad6, "强制本局胜利", CheatForceNodeVictory)
                .Bind(KeyCode.Keypad7, "BUG现场戳点", StampBugScene)
                .Bind(KeyCode.KeypadMinus, "QuickTest跳过战斗", CheatQuickTestSkipBattle);
        }

        private static void StampBugScene()
        {
            PerfTraceRecorder.StampUserObservation("BugScene");
        }

        private void CheatQuickTestSkipBattle()
        {
            var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            if (arch == null)
            {
                return;
            }

            var shell = arch.GetSystem<IGameFlowShellSystem>();
            if (shell == null || !shell.IsQuickTestMode)
            {
                Debug.Log("[InBattleManagerDevKeys] 跳过战斗仅 QuickTest 模式可用。");
                return;
            }

            CheatForceNodeVictory();
        }

        private void CheatForceNodeVictory()
        {
            var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            if (arch == null)
            {
                return;
            }

            arch.SendCommand(new CheatForceNodeVictoryCommand());
        }

        private void CheatAvatarHpTo99()
        {
            var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            if (arch == null)
            {
                return;
            }

            arch.SendCommand(new CheatSetAvatarHpCommand(CheatAvatarHp));
        }

        private async UniTaskVoid RunRealBattleEntryAsync()
        {
            var manager = ResolveManager();
            if (manager == null)
            {
                return;
            }

            manager.BootstrapRun();
            await manager.StartBattleNodeAsync();
        }

        private void TrySettlement()
        {
            var manager = ResolveManager();
            if (manager == null)
            {
                return;
            }

            if (!manager.TryEnterNodeSettlement())
            {
                Debug.Log("[InBattleManagerDevKeys] 内核尚未确认通关，未进入结算。");
            }
        }

        private static void ExportBattleAndFlowTrace()
        {
            BattleTraceRecorder.ExportBothNow(automatic: false);
            var battleOk = BattleTraceRecorder.CurrentSession != null
                && BattleTraceRecorder.CurrentSession.ops != null
                && BattleTraceRecorder.CurrentSession.ops.Count > 0;
            var flowOk = FlowTraceRecorder.CurrentSession != null
                && FlowTraceRecorder.CurrentSession.events != null
                && FlowTraceRecorder.CurrentSession.events.Count > 0;
            var perfOk = PerfTraceRecorder.CurrentSession != null
                && PerfTraceRecorder.CurrentSession.events != null
                && PerfTraceRecorder.CurrentSession.events.Count > 0;
            var registryOk = RegistryTraceRecorder.CurrentSession != null
                && RegistryTraceRecorder.CurrentSession.events != null
                && RegistryTraceRecorder.CurrentSession.events.Count > 0;
            if (!battleOk && !flowOk && !perfOk && !registryOk)
            {
                Debug.LogWarning("[InBattleManagerDevKeys] Battle/Core/Perf/Registry Trace 导出失败或无数据。");
            }
        }

        private static void ToggleBattleTrace()
        {
            BattleTraceRecorder.Enabled = !BattleTraceRecorder.Enabled;
            FlowTraceRecorder.Enabled = BattleTraceRecorder.Enabled;
            PerfTraceRecorder.Enabled = BattleTraceRecorder.Enabled;
            RegistryTraceRecorder.Enabled = BattleTraceRecorder.Enabled;
            Debug.Log(
                "[InBattleManagerDevKeys] BattleTrace.Enabled = "
                + BattleTraceRecorder.Enabled
                + ", FlowTrace.Enabled = "
                + FlowTraceRecorder.Enabled
                + ", PerfTrace.Enabled = "
                + PerfTraceRecorder.Enabled
                + ", RegistryTrace.Enabled = "
                + RegistryTraceRecorder.Enabled);
        }

        private BattleSessionController ResolveManager()
        {
            if (inBattleManager != null)
            {
                return inBattleManager;
            }

            inBattleManager = UnityEngine.Object.FindFirstObjectByType<BattleSessionController>();
            if (inBattleManager == null)
            {
                Debug.LogWarning("[InBattleManagerDevKeys] 未找到 BattleSessionController。");
            }

            return inBattleManager;
        }
    }
}

#endif
