using System;
using Cysharp.Threading.Tasks;
using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 跑图存档服务（表现层唯一入口）：
    /// - 检查点：正式局每个战斗节点开始前由 <see cref="GameFlowOrchestrator"/> 捕获并写入自动槽；
    /// - 手动存档：把当前检查点写入手动槽（颗粒度=该战斗开始，局中存档回滚到本场战斗开局）；
    /// - 读档：回主菜单收口后以恢复模式重开流程壳（<see cref="GameFlowRunOptions.CreateRestore"/>）。
    /// 落盘经 <see cref="RunSaveStoreHook"/>（Easy Save 3 桥）。
    /// </summary>
    public static class RunSaveService
    {
        public const string AutoSlotId = "auto";
        /// <summary>手动槽数量：与「UI槽位」面板可视高度对齐（读档列表 = 自动档 + 3 手动 = 4 行）。</summary>
        public const int ManualSlotCount = 3;
        private const float LoadReturnTimeoutSeconds = 4f;

        private static RunSaveSnapshot sCheckpoint;
        private static bool sLoading;

        /// <summary>当前内存检查点（本场/最近一场战斗开始时的快照）；主菜单或 run 结束后为 null。</summary>
        public static RunSaveSnapshot CurrentCheckpoint => sCheckpoint;

        public static bool IsLoading => sLoading;

        public static string ManualSlotId(int index)
        {
            return "manual_" + index;
        }

        /// <summary>战斗节点开始前捕获检查点并刷新自动存档。失败只告警，不阻断开局。</summary>
        public static void CaptureCheckpoint(int shellGlobalNodeIndex)
        {
            try
            {
                var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
                if (arch == null)
                {
                    return;
                }

                sCheckpoint = RunSaveGame.Capture(arch, shellGlobalNodeIndex);
                WriteSlot(AutoSlotId, sCheckpoint);
                Debug.Log(
                    $"[RunSave] 检查点已捕获 层{sCheckpoint.floor} 节点{sCheckpoint.DisplayNode}"
                    + $"（全局{shellGlobalNodeIndex}）并写入自动存档。");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RunSave] 检查点捕获失败：" + ex.Message);
            }
        }

        /// <summary>run 终局（胜利/失败）：清检查点并删除自动存档；手动槽保留。</summary>
        public static void HandleRunEnded()
        {
            sCheckpoint = null;
            try
            {
                RunSaveStoreHook.StoreOrNull()?.Delete(AutoSlotId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RunSave] 清除自动存档失败：" + ex.Message);
            }
        }

        /// <summary>把当前检查点写入手动槽。无检查点（未在局内）返回 false。</summary>
        public static bool SaveCheckpointToSlot(int manualIndex)
        {
            if (sCheckpoint == null)
            {
                Debug.LogWarning("[RunSave] 无可存进度（尚未进入战斗节点）。");
                return false;
            }

            try
            {
                WriteSlot(ManualSlotId(manualIndex), sCheckpoint);
                Debug.Log($"[RunSave] 已存档到槽位 {manualIndex}。");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError("[RunSave] 存档失败：" + ex.Message);
                return false;
            }
        }

        public static bool TryReadSlot(string slotId, out RunSaveSnapshot snapshot)
        {
            snapshot = null;
            try
            {
                var store = RunSaveStoreHook.StoreOrNull();
                string json;
                if (store == null || !store.TryRead(slotId, out json) || string.IsNullOrEmpty(json))
                {
                    return false;
                }

                var parsed = JsonUtility.FromJson<RunSaveSnapshot>(json);
                if (parsed == null || parsed.version != RunSaveSnapshot.CurrentVersion)
                {
                    Debug.LogWarning(
                        $"[RunSave] 槽位 {slotId} 版本不兼容（{parsed?.version.ToString() ?? "null"}），忽略。");
                    return false;
                }

                snapshot = parsed;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RunSave] 读取槽位 {slotId} 失败：" + ex.Message);
                return false;
            }
        }

        /// <summary>读档：停掉当前流程（若在局内）→ 以恢复模式重开，从快照的战斗节点开始。</summary>
        public static bool RequestLoadSlot(string slotId)
        {
            if (sLoading)
            {
                Debug.LogWarning("[RunSave] 读档进行中，忽略重复请求。");
                return false;
            }

            RunSaveSnapshot snapshot;
            if (!TryReadSlot(slotId, out snapshot))
            {
                Debug.LogWarning($"[RunSave] 槽位 {slotId} 无可用存档。");
                return false;
            }

            LoadAsync(snapshot).Forget();
            return true;
        }

        private static async UniTaskVoid LoadAsync(RunSaveSnapshot snapshot)
        {
            sLoading = true;
            try
            {
                var shell = GameFlowShellSystem.EnsureRegistered();
                if (shell.IsBusy || shell.State.Value != GameFlowShellState.MainMenu)
                {
                    shell.ReturnToMainMenu();
                    var deadline = Time.realtimeSinceStartup + LoadReturnTimeoutSeconds;
                    while ((shell.IsBusy || shell.State.Value != GameFlowShellState.MainMenu)
                           && Time.realtimeSinceStartup < deadline)
                    {
                        await UniTask.Yield();
                    }

                    if (shell.IsBusy)
                    {
                        Debug.LogError("[RunSave] 读档失败：当前对局未能收口（仍 busy）。");
                        return;
                    }
                }

                // 收口后再让一帧，避免与 EnterMainMenu 的同帧表现清理竞争。
                await UniTask.Yield();
                Debug.Log(
                    $"[RunSave] 读档开始 层{snapshot.floor} 节点{snapshot.DisplayNode}"
                    + $" seed={snapshot.seed}。");
                shell.BeginRun(GameFlowRunOptions.CreateRestore(snapshot));
            }
            catch (Exception ex)
            {
                Debug.LogError("[RunSave] 读档异常：" + ex.Message);
            }
            finally
            {
                sLoading = false;
            }
        }

        private static void WriteSlot(string slotId, RunSaveSnapshot snapshot)
        {
            var store = RunSaveStoreHook.StoreOrNull();
            if (store == null)
            {
                throw new InvalidOperationException("存档后端未注册（Easy Save 3 桥缺失）。");
            }

            snapshot.StampSavedAtNow();
            store.Write(slotId, JsonUtility.ToJson(snapshot));
        }
    }
}
