using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace NineGrid.Presentation.Performance
{
    /// <summary>
    /// 怪物反击玩家：复用 <see cref="CardAttackPerformance"/>，攻击方/受击方对调，方向取棋盘方位反方向。
    /// </summary>
    [MovedFrom("NineGrid.Presentation.AtomicRepresentationTools")]
    [DisallowMultipleComponent]
    public sealed class CounterattackPerformance : MonoBehaviour
    {
        [SerializeField] private CardAttackPerformance attackPerformance;

        public bool IsPlaying => attackPerformance != null && attackPerformance.IsPlaying;

        public float TotalDuration => attackPerformance != null ? attackPerformance.TotalDuration : 0f;

        private void Awake()
        {
            EnsureAttackPerformanceReference();
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

            EnsureAttackPerformanceReference();
            Vector2 strikeDirection = CardBattleDirectionUtil.Opposite(boardDirection);
            attackPerformance.Play(enemy, player, strikeDirection);
        }

        [ContextMenu("Stop And Restore")]
        public void StopAndRestore()
        {
            attackPerformance?.StopAndRestore();
        }

        private void EnsureAttackPerformanceReference()
        {
            if (attackPerformance == null)
            {
                attackPerformance = GetComponent<CardAttackPerformance>();
            }

            if (attackPerformance == null)
            {
                attackPerformance = gameObject.AddComponent<CardAttackPerformance>();
            }
        }
    }
}
