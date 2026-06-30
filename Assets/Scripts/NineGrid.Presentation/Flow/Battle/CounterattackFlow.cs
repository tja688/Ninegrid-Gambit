using NineGrid.Presentation.Contracts;
using NineGrid.Presentation.Shared;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace NineGrid.Presentation.Flow.Battle
{
    /// <summary>
    /// 怪物反击玩家：复用 <see cref="CardAttackFlow"/>，攻击方/受击方对调，方向取棋盘方位反方向。
    /// </summary>
    [DisallowMultipleComponent]
    [MovedFrom(true, "NineGrid.Presentation.Performance", null, "CounterattackPerformance")]
    public sealed class CounterattackFlow : MonoBehaviour, IDirectedFlow
    {
        [SerializeField] private CardAttackFlow attackFlow;

        public bool IsPlaying => attackFlow != null && attackFlow.IsPlaying;
        public float ExpectedDuration => attackFlow != null ? attackFlow.ExpectedDuration : 0f;
        public float TotalDuration => attackFlow != null ? attackFlow.TotalDuration : 0f;

        private void Awake()
        {
            EnsureAttackFlowReference();
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
        public void Play(Transform enemy, Transform player, Vector2 boardDirection)
        {
            if (!isActiveAndEnabled || enemy == null || player == null)
            {
                return;
            }

            EnsureAttackFlowReference();
            Vector2 strikeDirection = CardBattleDirectionUtil.Opposite(boardDirection);
            attackFlow.Play(enemy, player, strikeDirection);
        }

        [ContextMenu("Stop And Restore")]
        public void StopAndRestore()
        {
            attackFlow?.StopAndRestore();
        }

        private void EnsureAttackFlowReference()
        {
            if (attackFlow == null)
            {
                attackFlow = GetComponent<CardAttackFlow>();
            }

            if (attackFlow == null)
            {
                attackFlow = gameObject.AddComponent<CardAttackFlow>();
            }
        }
    }
}
