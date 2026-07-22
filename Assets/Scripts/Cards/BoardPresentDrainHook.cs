using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 盘面 Present drain 统一入口：由 NineGrid.Presentation Controller 接线，避免 CombatHitSink 业务委托。
    /// </summary>
    public static class BoardPresentDrainHook
    {
        public static Action<Func<PostKillBoardPresentationResult, CancellationToken, UniTask>> WireDrain;

        /// <summary>Present 盘面摘要；由 InBattle DrainPostKillBoard 经 Controller 注册。</summary>
        public static Func<PostKillBoardPresentationResult, CancellationToken, UniTask> Drain;

        public static void RequestWire(
            Func<PostKillBoardPresentationResult, CancellationToken, UniTask> drain)
        {
            Drain = drain;
            WireDrain?.Invoke(drain);
        }

        public static UniTask RequestDrain(
            PostKillBoardPresentationResult result,
            CancellationToken cancellationToken = default)
        {
            if (Drain == null)
            {
                Debug.LogWarning("[BoardPresentDrainHook] Drain 未注册。");
                return UniTask.CompletedTask;
            }

            return Drain(result, cancellationToken);
        }
    }
}
