using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting.APIUpdating;

namespace NineGrid.Presentation.Performance
{
    /// <summary>
    /// 玩家攻击单体：冲刺 + 敌人受击位移 + signal 驱动受击闪白。
    /// 烘焙自 NineGrid Battle Standard Player Attack To Right/Up Timeline。
    /// </summary>
    [MovedFrom("NineGrid.Presentation.AtomicRepresentationTools")]
    [DisallowMultipleComponent]
    public sealed class CardAttackPerformance : MonoBehaviour
    {
        [Header("Timing (Timeline — canonical Right)")]
        [SerializeField, Min(0f)] private float prepDuration = 0.1f;
        [SerializeField, Min(0f)] private float lungeDelay = 0.3f;
        [SerializeField, Min(0.01f)] private float lungeDuration = 0.3f;
        [SerializeField, Min(0f)] private float scaleSnapDelay = 0.3f;
        [SerializeField, Min(0.01f)] private float scaleSnapDuration = 0.2f;
        [SerializeField, Min(0f)] private float returnDelay = 0.5f;
        [SerializeField, Min(0.01f)] private float returnDuration = 0.2f;
        [SerializeField, Min(0f)] private float knockbackDelay = 0.4f;
        [SerializeField, Min(0.01f)] private float knockbackDuration = 0.2f;
        [SerializeField, Min(0f)] private float enemyReturnDelay = 0.6f;
        [SerializeField, Min(0.01f)] private float enemyReturnDuration = 0.2f;
        [SerializeField, Min(0f)] private float hitFlashDelay = 0.4f;

        [Header("Motion (canonical Right)")]
        [SerializeField] private Vector2 playerPrepPullback = new(-0.4f, 0f);
        [SerializeField] private Vector2 playerLunge = new(1.5f, 0f);
        [SerializeField] private Vector2 enemyKnockback = new(1f, 0f);
        [SerializeField] private Vector2 enemyKnockbackReturn = new(-1f, 0f);
        [SerializeField, Min(0f)] private float scalePunchAmount = 0.1f;

        [Header("Hit Flash")]
        [SerializeField] private Material flashMaterialTemplate;

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
        private CardHitFlashPerformance enemyHitFlash;

        private Sequence activeSequence;
        private Coroutine playCoroutine;
        private bool isPlaying;

        public bool IsPlaying => isPlaying;
        public float TotalDuration => enemyReturnDelay + enemyReturnDuration;

        private void OnDisable()
        {
            StopAndRestore();
        }

        private void OnValidate()
        {
            prepDuration = Mathf.Max(0f, prepDuration);
            lungeDuration = Mathf.Max(0.01f, lungeDuration);
            scaleSnapDuration = Mathf.Max(0.01f, scaleSnapDuration);
            returnDuration = Mathf.Max(0.01f, returnDuration);
            knockbackDuration = Mathf.Max(0.01f, knockbackDuration);
            enemyReturnDuration = Mathf.Max(0.01f, enemyReturnDuration);
            scalePunchAmount = Mathf.Max(0f, scalePunchAmount);
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

            StopPlaybackOnly();
            activePlayer = player;
            activeEnemy = enemy;
            CaptureBaselines(player, enemy);
            EnsureEnemyHitFlash(enemy);

            activeSequence = BuildAttackSequence(
                player,
                enemy,
                direction,
                playerBaselineLocalPosition,
                playerBaselineLocalScale);
            AttachAttackCallbacks(activeSequence);
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
            isPlaying = false;
        }

        /// <summary>
        /// 构建攻击子序列（无完成回调），供击杀等编排复用。
        /// </summary>
        public Sequence BuildAttackSequence(
            Transform player,
            Transform enemy,
            Vector2 direction,
            Vector3 playerBaselinePosition,
            Vector3 playerBaselineScale,
            bool includeEnemyReturn = true)
        {
            Sequence sequence = DOTween.Sequence()
                .SetTarget(this)
                .SetAutoKill(true)
                .Pause();

            if (ignoreTimeScale)
            {
                sequence.SetUpdate(true);
            }

            InsertStrikeIntoSequence(
                sequence,
                player,
                enemy,
                direction,
                playerBaselinePosition,
                playerBaselineScale,
                includeEnemyReturn);
            return sequence;
        }

        /// <summary>
        /// 将冲刺段直接插入宿主 Sequence（避免嵌套子序列导致并行淡出失效）。
        /// </summary>
        public void InsertStrikeIntoSequence(
            Sequence sequence,
            Transform player,
            Transform enemy,
            Vector2 direction,
            Vector3 playerBaselinePosition,
            Vector3 playerBaselineScale,
            bool includeEnemyReturn = true)
        {
            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            Vector3 prep = CardBattleDirectionUtil.ToVector3(
                CardBattleDirectionUtil.RotateFromCanonicalRight(playerPrepPullback, dir));
            Vector3 lunge = CardBattleDirectionUtil.ToVector3(
                CardBattleDirectionUtil.RotateFromCanonicalRight(playerLunge, dir));
            Vector3 knock = CardBattleDirectionUtil.ToVector3(
                CardBattleDirectionUtil.RotateFromCanonicalRight(enemyKnockback, dir));
            Vector3 knockReturn = CardBattleDirectionUtil.ToVector3(
                CardBattleDirectionUtil.RotateFromCanonicalRight(enemyKnockbackReturn, dir));

            Tween playerPrepMove = player
                .DOLocalMove(prep, prepDuration)
                .SetRelative(true)
                .SetEase(Ease.InOutQuad)
                .SetDelay(0f);
            ConfigureTween(playerPrepMove);

            Tween playerScalePunch = player
                .DOScale(playerBaselineScale + Vector3.one * scalePunchAmount, prepDuration)
                .SetEase(Ease.InOutQuad)
                .SetDelay(0f);
            ConfigureTween(playerScalePunch);

            Tween playerLungeTween = player
                .DOLocalMove(lunge, lungeDuration)
                .SetRelative(true)
                .SetEase(Ease.OutQuad)
                .SetDelay(lungeDelay);
            ConfigureTween(playerLungeTween);

            Tween playerScaleSnap = player
                .DOScale(playerBaselineScale, scaleSnapDuration)
                .SetEase(Ease.OutQuad)
                .SetDelay(scaleSnapDelay);
            ConfigureTween(playerScaleSnap);

            Tween playerReturn = player
                .DOLocalMove(playerBaselinePosition, returnDuration)
                .SetEase(Ease.InOutQuad)
                .SetDelay(returnDelay);
            ConfigureTween(playerReturn);

            Tween enemyKnockTween = enemy
                .DOLocalMove(knock, knockbackDuration)
                .SetRelative(true)
                .SetEase(Ease.OutQuad)
                .SetDelay(knockbackDelay);
            ConfigureTween(enemyKnockTween);

            sequence.Insert(0f, playerPrepMove);
            sequence.Insert(0f, playerScalePunch);
            sequence.Insert(0f, playerLungeTween);
            sequence.Insert(0f, playerScaleSnap);
            sequence.Insert(0f, playerReturn);
            sequence.Insert(0f, enemyKnockTween);

            if (includeEnemyReturn)
            {
                Tween enemyReturnTween = enemy
                    .DOLocalMove(knockReturn, enemyReturnDuration)
                    .SetRelative(true)
                    .SetEase(Ease.InOutQuad)
                    .SetDelay(enemyReturnDelay);
                ConfigureTween(enemyReturnTween);
                sequence.Insert(0f, enemyReturnTween);
            }

            sequence.InsertCallback(hitFlashDelay, () => PlayEnemyHitFlashOn(enemy));
        }

        private void AttachAttackCallbacks(Sequence sequence)
        {
            sequence.OnComplete(HandleSequenceComplete);
            sequence.OnKill(HandleSequenceKilled);
        }

        private void ConfigureTween(Tween tween)
        {
            tween.SetTarget(this);

            if (ignoreTimeScale)
            {
                tween.SetUpdate(true);
            }
        }

        private void EnsureEnemyHitFlash(Transform enemy)
        {
            enemyHitFlash = enemy.GetComponent<CardHitFlashPerformance>();
            if (enemyHitFlash == null)
            {
                enemyHitFlash = enemy.gameObject.AddComponent<CardHitFlashPerformance>();
            }

            if (flashMaterialTemplate != null)
            {
                enemyHitFlash.SetFlashMaterialTemplate(flashMaterialTemplate);
            }
        }

        private void PlayEnemyHitFlash()
        {
            PlayEnemyHitFlashOn(activeEnemy);
        }

        private void PlayEnemyHitFlashOn(Transform enemy)
        {
            if (enemy == null)
            {
                return;
            }

            CardHitFlashPerformance flash = enemyHitFlash;
            if (flash == null || flash.gameObject != enemy.gameObject)
            {
                flash = enemy.GetComponent<CardHitFlashPerformance>();
                if (flash == null)
                {
                    flash = enemy.gameObject.AddComponent<CardHitFlashPerformance>();
                }

                if (flashMaterialTemplate != null)
                {
                    flash.SetFlashMaterialTemplate(flashMaterialTemplate);
                }
            }

            flash.PlayHitFlash();
        }

        private void CaptureBaselines(Transform player, Transform enemy)
        {
            playerBaselineLocalPosition = player.localPosition;
            playerBaselineLocalScale = player.localScale;
            enemyBaselineLocalPosition = enemy.localPosition;
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
            }

            enemyHitFlash?.StopAndRestore();
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
            RestoreBaselines();
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
    }
}
