#if UNITY_EDITOR || DEVELOPMENT_BUILD

using Cysharp.Threading.Tasks;
using NineGrid.Flow;
using NineGrid.Flow.Diagnostics;
using UnityEngine;

namespace NineGrid.DevTest.Flow
{
    [DisallowMultipleComponent]
    public sealed class InBattleManagerDevKeys : TestKeyModuleBehaviour
    {
        private const int CheatAvatarHp = 99;

        [Tooltip("运行时自动查找 InBattleManagerSingleton.Instance；也可手动拖入覆盖。")]
        [SerializeField] private InBattleManagerSingleton inBattleManager;

        protected override string ModuleId => "in-battle-manager";

        protected override void OnEnable()
        {
            if (inBattleManager == null)
            {
                inBattleManager = GetComponent<InBattleManagerSingleton>();
            }

            if (inBattleManager == null)
            {
                inBattleManager = InBattleManagerSingleton.Instance;
            }

            base.OnEnable();
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
                .Bind(KeyCode.Keypad7, "BUG现场戳点", StampBugScene);
        }

        private static void StampBugScene()
        {
            PerfTraceRecorder.StampUserObservation("BugScene");
        }

        private void CheatForceNodeVictory()
        {
            var manager = ResolveManager();
            if (manager == null)
            {
                return;
            }

            if (!manager.TryCheatForceNodeVictory())
            {
                Debug.LogWarning("[InBattleManagerDevKeys] 强制胜利失败：请先进入正式对局（InteractionLoop）。");
            }
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

        private void CheatAvatarHpTo99()
        {
            var manager = ResolveManager();
            if (manager == null)
            {
                return;
            }

            if (!manager.TryCheatSetAvatarHp(CheatAvatarHp))
            {
                Debug.LogWarning("[InBattleManagerDevKeys] 改血失败：请先进入正式对局。");
            }
        }

        private static void ExportBattleAndFlowTrace()
        {
            BattleTraceRecorder.ExportBothNow();
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

        private InBattleManagerSingleton ResolveManager()
        {
            if (inBattleManager != null)
            {
                return inBattleManager;
            }

            inBattleManager = InBattleManagerSingleton.Instance;
            if (inBattleManager == null)
            {
                Debug.LogWarning("[InBattleManagerDevKeys] 未找到 InBattleManagerSingleton。");
            }

            return inBattleManager;
        }
    }
}

#endif
