using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting.APIUpdating;

namespace NineGrid.Presentation.Performance
{
    /// <summary>
    /// 玩家击杀单体：冲刺（无敌人回缩）+ 敌人淡出消失 + signal 驱动受击闪白。
    /// 烘焙自 NineGrid Battle Standard Player Kill Right/Up Timeline。
    /// </summary>
    [MovedFrom("NineGrid.Presentation.AtomicRepresentationTools")]
    [DisallowMultipleComponent]
    public sealed class CardKillPerformance : MonoBehaviour
    {
        [Header("Strike Block")]
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
        private SpriteRenderer[] enemySpriteRenderers = System.Array.Empty<SpriteRenderer>();
        private Color[] enemyBaselineColors = System.Array.Empty<Color>();
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
            enemySpriteRenderers = System.Array.Empty<SpriteRenderer>();
            enemyBaselineColors = System.Array.Empty<Color>();
            isPlaying = false;
        }

        private Sequence BuildKillSequence(Transform player, Transform enemy, Vector2 direction)
        {
            Sequence sequence = DOTween.Sequence()
                .SetTarget(this)
                .SetAutoKill(true)
                .Pause();

            if (ignoreTimeScale)
            {
                sequence.SetUpdate(true);
            }

            attackPerformance.InsertStrikeIntoSequence(
                sequence,
                player,
                enemy,
                direction,
                playerBaselineLocalPosition,
                playerBaselineLocalScale,
                includeEnemyReturn: false);

            if (enemySpriteRenderers.Length > 0)
            {
                sequence.InsertCallback(enemyFadeDelay, () => PrepareEnemyForFade(enemy));

                for (int i = 0; i < enemySpriteRenderers.Length; i++)
                {
                    SpriteRenderer renderer = enemySpriteRenderers[i];
                    if (renderer == null)
                    {
                        continue;
                    }

                    int rendererIndex = i;
                    Tween fadeTween = DOTween
                        .To(
                            () => 1f,
                            alpha => ApplyEnemyFadeAlpha(renderer, rendererIndex, alpha),
                            0f,
                            enemyFadeDuration)
                        .SetEase(Ease.Linear)
                        .SetTarget(renderer);

                    if (ignoreTimeScale)
                    {
                        fadeTween.SetUpdate(true);
                    }

                    sequence.Insert(enemyFadeDelay, fadeTween);
                }
            }

            sequence.OnComplete(HandleSequenceComplete);
            sequence.OnKill(HandleSequenceKilled);
            return sequence;
        }

        private void PrepareEnemyForFade(Transform enemy)
        {
            ReleaseEnemyFlashEffects(enemy);

            for (int i = 0; i < enemySpriteRenderers.Length; i++)
            {
                SpriteRenderer renderer = enemySpriteRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                ApplyEnemyFadeAlpha(renderer, i, 1f);
            }
        }

        private void ApplyEnemyFadeAlpha(SpriteRenderer renderer, int colorIndex, float alpha)
        {
            if (renderer == null)
            {
                return;
            }

            Color color = colorIndex >= 0 && colorIndex < enemyBaselineColors.Length
                ? enemyBaselineColors[colorIndex]
                : renderer.color;
            color.a = Mathf.Clamp01(alpha);
            renderer.color = color;
        }

        private void CaptureBaselines(Transform player, Transform enemy)
        {
            playerBaselineLocalPosition = player.localPosition;
            playerBaselineLocalScale = player.localScale;
            enemyBaselineLocalPosition = enemy.localPosition;

            enemySpriteRenderers = enemy.GetComponentsInChildren<SpriteRenderer>(true);
            enemyBaselineColors = new Color[enemySpriteRenderers.Length];
            for (int i = 0; i < enemySpriteRenderers.Length; i++)
            {
                SpriteRenderer renderer = enemySpriteRenderers[i];
                enemyBaselineColors[i] = renderer != null ? renderer.color : Color.white;
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
                RestoreEnemyVisualState(activeEnemy);

                if (hideEnemyAfterFade)
                {
                    activeEnemy.gameObject.SetActive(enemyWasActive);
                }
            }
        }

        private void RestoreEnemyVisualState(Transform enemy)
        {
            ReleaseEnemyFlashEffects(enemy);

            for (int i = 0; i < enemySpriteRenderers.Length; i++)
            {
                SpriteRenderer renderer = enemySpriteRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Color color = i < enemyBaselineColors.Length ? enemyBaselineColors[i] : Color.white;
                renderer.color = color;
            }
        }

        private static void ReleaseEnemyFlashEffects(Transform enemy)
        {
            if (enemy == null)
            {
                return;
            }

            CardHitFlashPerformance[] flashes = enemy.GetComponentsInChildren<CardHitFlashPerformance>(true);
            for (int i = 0; i < flashes.Length; i++)
            {
                flashes[i]?.ReleaseFlashForExternalEffect();
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

            for (int i = 0; i < enemySpriteRenderers.Length; i++)
            {
                SpriteRenderer renderer = enemySpriteRenderers[i];
                if (renderer != null)
                {
                    DOTween.Kill(renderer);
                }
            }
        }

        private void HandleSequenceComplete()
        {
            activeSequence = null;
            isPlaying = false;

            if (hideEnemyAfterFade && activeEnemy != null)
            {
                activeEnemy.gameObject.SetActive(false);
                RestoreEnemyVisualState(activeEnemy);
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
