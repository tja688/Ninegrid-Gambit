using NineGrid.Presentation.Contracts;
using NineGrid.Presentation.Shared;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace NineGrid.Presentation.Flow.Battle
{
    /// <summary>
    /// 怪物击杀玩家：复用 <see cref="CardKillFlow"/>，攻击方/受击方对调，方向取棋盘方位反方向。
    /// </summary>
    [DisallowMultipleComponent]
    [MovedFrom(true, "NineGrid.Presentation.Performance", null, "CounterattackKillPerformance")]
    public sealed class CounterattackKillFlow : MonoBehaviour, IDirectedFlow
    {
        [SerializeField] private CardKillFlow killFlow;

        public bool IsPlaying => killFlow != null && killFlow.IsPlaying;
        public float ExpectedDuration => killFlow != null ? killFlow.ExpectedDuration : 0f;
        public float TotalDuration => killFlow != null ? killFlow.TotalDuration : 0f;

        private void Awake()
        {
            EnsureKillFlowReference();
        }

        private void OnDisable()
        {
            StopAndRestore();
        }

        public void Play(Transform enemy, Transform player, CardBattleDirection boardDirection)
        {
            Play(enemy, player, CardBattleDirectionUtil.ToVector2(boardDirection));
        }

        /// <param name="boardDirection">怪物相对玩家的棋盘方位（与玩家攻击时一致，如怪物在右侧则为 Right）。</param>
        public void Play(Transform enemy, Transform player, Vector2 boardDirection, bool includeStrike = true)
        {
            if (!isActiveAndEnabled || enemy == null || player == null)
            {
                return;
            }

            EnsureKillFlowReference();
            Vector2 strikeDirection = CardBattleDirectionUtil.Opposite(boardDirection);
            killFlow.Play(enemy, player, strikeDirection, includeStrike);
        }

        [ContextMenu("Stop And Restore")]
        public void StopAndRestore()
        {
            killFlow?.StopAndRestore();
        }

        private void EnsureKillFlowReference()
        {
            if (killFlow == null)
            {
                killFlow = GetComponent<CardKillFlow>();
            }

            if (killFlow == null)
            {
                killFlow = gameObject.AddComponent<CardKillFlow>();
            }
        }
    }
}
