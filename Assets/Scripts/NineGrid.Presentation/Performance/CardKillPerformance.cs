using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting.APIUpdating;

namespace NineGrid.Presentation.Performance
{
    /// <summary>
    /// 玩家击杀单体：攻击编排 + 敌人淡出消失 + signal 驱动受击闪白。
    /// 烘焙自 NineGrid Battle Standard Player Kill Right/Up Timeline。
    /// </summary>
    [MovedFrom("NineGrid.Presentation.AtomicRepresentationTools")]
    [DisallowMultipleComponent]
    public sealed class CardKillPerformance : MonoBehaviour
    {
        [Header("Attack Block")]
        [SerializeField] private CardAttackPerformance attackPerformance;

        [Header("Kill Fade (Timeline)")]
        [SerializeField, Min(0f)] private float enemyFadeDelay = 0.5f;
        [SerializeField, Min(0.01f)] private float enemyFadeDuration = 0.3f;
        [SerializeField] private bool hideEnemyAfterFade = true;

        [Header("Playback Safety")]
        [SerializeField] private bool deferPlayOneFrame = true;
        [SerializeField] private bool ignoreTimeScale;

        [Header("Events")]
        [SerializeField] private UnityEvent onComplete;

        private Transform activePlayer;
        private Transform activeEnemy;
        private Vector3 playerBaselineLocalPosition;
        private Vector3 playerBaselineLocalScale;
        private Vector3 enemyBaselineLocalPosition;
        private SpriteRenderer enemySpriteRenderer;
        private Color enemyBaselineColor;
        private bool enemyWasActive;

        private Sequence activeSequence;
        private Coroutine playCoroutine;
        private bool isPlaying;

        public bool IsPlaying => isPlaying;
        public float TotalDuration => Mathf.Max(
            attackPerformance != null ? attackPerformance.TotalDuration : 0f,
            enemyFadeDelay + enemyFadeDuration);

        private void Awake()
        {
            EnsureAttackPerformanceReference();
        }

        private void OnDisable()
        {
            StopAndRestore();
        }

        private void OnValidate()
        {
            enemyFadeDuration = Mathf.Max(0.01f, enemyFadeDuration);
        }

        public void Play(Transform player, Transform enemy, CardBattleDirection direction)
        {
            Play(player, enemy, CardBattleDirectionUtil.ToVector2(direction));
        }

        public void Play(Transform player, Transform enemy, Vector2 direction)
        {
            if (!isActiveAndEnabled || player == null || enemy == null)
            {
                return;
            }

            EnsureAttackPerformanceReference();
            StopPlaybackOnly();

            activePlayer = player;
            activeEnemy = enemy;
            CaptureBaselines(player, enemy);

            activeSequence = BuildKillSequence(player, enemy, direction);
            isPlaying = true;

            if (deferPlayOneFrame)
            {
                playCoroutine = StartCoroutine(PlayNextFrame(activeSequence));
            }
            else
            {
                StartSequence(activeSequence);
            }
        }

        [ContextMenu("Stop And Restore")]
        public void StopAndRestore()
        {
            StopPlaybackOnly();
            RestoreBaselines();
            activePlayer = null;
            activeEnemy = null;
            enemySpriteRenderer = null;
            isPlaying = false;
        }

        private Sequence BuildKillSequence(Transform player, Transform enemy, Vector2 direction)
        {
            Sequence attackSequence = attackPerformance.BuildAttackSequence(
                player,
                enemy,
                direction,
                playerBaselineLocalPosition,
                playerBaselineLocalScale);

            Sequence sequence = DOTween.Sequence()
                .SetTarget(this)
                .SetAutoKill(true)
                .Pause();

            if (ignoreTimeScale)
            {
                sequence.SetUpdate(true);
            }

            sequence.Insert(0f, attackSequence);

            if (enemySpriteRenderer != null)
            {
                Color targetColor = enemyBaselineColor;
                targetColor.a = 0f;
                Tween fadeTween = DOTween
                    .To(() => enemySpriteRenderer.color, value => enemySpriteRenderer.color = value, targetColor, enemyFadeDuration)
                    .SetEase(Ease.Linear)
                    .SetDelay(enemyFadeDelay)
                    .SetTarget(enemySpriteRenderer);
                ConfigureTween(fadeTween);
                sequence.Insert(0f, fadeTween);
            }

            sequence.OnComplete(HandleSequenceComplete);
            sequence.OnKill(HandleSequenceKilled);
            return sequence;
        }

        private void CaptureBaselines(Transform player, Transform enemy)
        {
            playerBaselineLocalPosition = player.localPosition;
            playerBaselineLocalScale = player.localScale;
            enemyBaselineLocalPosition = enemy.localPosition;

            enemySpriteRenderer = enemy.GetComponent<SpriteRenderer>();
            if (enemySpriteRenderer != null)
            {
                enemyBaselineColor = enemySpriteRenderer.color;
            }

            enemyWasActive = enemy.gameObject.activeSelf;
            enemy.gameObject.SetActive(true);
        }

        private void RestoreBaselines()
        {
            if (activePlayer != null)
            {
                activePlayer.localPosition = playerBaselineLocalPosition;
                activePlayer.localScale = playerBaselineLocalScale;
            }

            if (activeEnemy != null)
            {
                activeEnemy.localPosition = enemyBaselineLocalPosition;

                if (enemySpriteRenderer != null)
                {
                    enemySpriteRenderer.color = enemyBaselineColor;
                }

                if (hideEnemyAfterFade)
                {
                    activeEnemy.gameObject.SetActive(enemyWasActive);
                }

                CardHitFlashPerformance flash = activeEnemy.GetComponent<CardHitFlashPerformance>();
                flash?.StopAndRestore();
            }
        }

        private void ConfigureTween(Tween tween)
        {
            tween.SetTarget(this);

            if (ignoreTimeScale)
            {
                tween.SetUpdate(true);
            }
        }

        private IEnumerator PlayNextFrame(Sequence sequence)
        {
            yield return null;
            playCoroutine = null;
            StartSequence(sequence);
        }

        private void StartSequence(Sequence sequence)
        {
            if (sequence == null)
            {
                return;
            }

            sequence.Restart();
        }

        private void StopPlaybackOnly()
        {
            if (playCoroutine != null)
            {
                StopCoroutine(playCoroutine);
                playCoroutine = null;
            }

            if (activeSequence != null && activeSequence.IsActive())
            {
                activeSequence.OnComplete(null);
                activeSequence.OnKill(null);
                activeSequence.Kill();
            }

            activeSequence = null;
            DOTween.Kill(this);

            if (activePlayer != null)
            {
                DOTween.Kill(activePlayer);
            }

            if (activeEnemy != null)
            {
                DOTween.Kill(activeEnemy);
            }
        }

        private void HandleSequenceComplete()
        {
            activeSequence = null;
            isPlaying = false;

            if (hideEnemyAfterFade && activeEnemy != null)
            {
                activeEnemy.gameObject.SetActive(false);
            }
            else
            {
                RestoreBaselines();
            }

            onComplete?.Invoke();
        }

        private void HandleSequenceKilled()
        {
            if (activeSequence != null && !activeSequence.IsComplete())
            {
                activeSequence = null;
                isPlaying = false;
            }
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
