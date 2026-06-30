using NineGrid.Presentation.Shared;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace NineGrid.Presentation.Performance
{
    /// <summary>
    /// 怪物击杀玩家：复用 <see cref="CardKillPerformance"/>，攻击方/受击方对调，方向取棋盘方位反方向。
    /// </summary>
    [MovedFrom("NineGrid.Presentation.AtomicRepresentationTools")]
    [DisallowMultipleComponent]
    public sealed class CounterattackKillPerformance : MonoBehaviour
    {
        [SerializeField] private CardKillPerformance killPerformance;

        public bool IsPlaying => killPerformance != null && killPerformance.IsPlaying;

        public float TotalDuration => killPerformance != null ? killPerformance.TotalDuration : 0f;

        private void Awake()
        {
            EnsureKillPerformanceReference();
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

            EnsureKillPerformanceReference();
            Vector2 strikeDirection = CardBattleDirectionUtil.Opposite(boardDirection);
            killPerformance.Play(enemy, player, strikeDirection);
        }

        [ContextMenu("Stop And Restore")]
        public void StopAndRestore()
        {
            killPerformance?.StopAndRestore();
        }

        private void EnsureKillPerformanceReference()
        {
            if (killPerformance == null)
            {
                killPerformance = GetComponent<CardKillPerformance>();
            }

            if (killPerformance == null)
            {
                killPerformance = gameObject.AddComponent<CardKillPerformance>();
            }
        }
    }
}
