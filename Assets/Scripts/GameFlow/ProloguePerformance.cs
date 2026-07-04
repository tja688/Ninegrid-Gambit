using System;
using System.Collections;
using DG.Tweening;
using NineGrid.UI;
using UnityEngine;

namespace NineGrid.GameFlow
{
    /// <summary>
    /// 序章演出：空海 → 玩家入场 → 对话 → 敌方入场 → 就绪进入战斗环节。
    /// 点位使用场景中的 start / stay 标注；对话统一走 DialogueSystem。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProloguePerformance : MonoBehaviour
    {
        public static ProloguePerformance Instance { get; private set; }

        [Header("Player")]
        [SerializeField] Transform player;
        [SerializeField] Transform playerStart;
        [SerializeField] Transform playerStay;

        [Header("Enemy")]
        [SerializeField] Transform enemy;
        [SerializeField] Transform enemyStart;
        [SerializeField] Transform enemyStay;

        [Header("Dialogue")]
        [SerializeField] DialogueSystem dialogue;
        [SerializeField] string dialogueSequenceId = "prologue_intro";

        [Header("Timing")]
        [SerializeField] float initialSeaHold = 1f;
        [SerializeField] float sailDuration = 1f;
        [SerializeField] Ease sailEase = Ease.OutCubic;

        Coroutine _routine;
        Tween _sailTween;
        bool _isPlaying;

        public bool IsPlaying => _isPlaying;

        /// <summary>演出全部完成（双方已就位），可进入战斗环节。</summary>
        public event Action Completed;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[ProloguePerformance] 场景中存在多个实例，保留先创建的。");
                return;
            }

            Instance = this;
            ResolveRefs();
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            StopPerformance(invokeCompleted: false);
        }

        /// <summary>跳过演出，双方直接摆到 stay（战斗0 / 重开通道）。</summary>
        public void PrepareIdleBattleFormation()
        {
            StopPerformance(invokeCompleted: false);
            ResolveRefs();
            PlaceAt(player, playerStay);
            PlaceAt(enemy, enemyStay);
            SetShipActive(player, true);
            SetShipActive(enemy, true);
        }

        /// <summary>播放完整序章演出。</summary>
        public void Play()
        {
            if (_isPlaying)
            {
                Debug.LogWarning("[ProloguePerformance] 已在播放中，忽略重复 Play。");
                return;
            }

            ResolveRefs();
            StopPerformance(invokeCompleted: false);
            _isPlaying = true;
            _routine = StartCoroutine(RunRoutine());
        }

        IEnumerator RunRoutine()
        {
            // 开局：空海
            SetShipActive(player, false);
            SetShipActive(enemy, false);

            if (initialSeaHold > 0f)
            {
                yield return new WaitForSeconds(initialSeaHold);
            }

            // 玩家入镜 → stay
            yield return SailIn(player, playerStart, playerStay);

            // 停稳后对话
            yield return PlayDialogue();

            // 敌方入镜 → stay
            yield return SailIn(enemy, enemyStart, enemyStay);

            _isPlaying = false;
            _routine = null;
            Completed?.Invoke();
        }

        IEnumerator SailIn(Transform ship, Transform start, Transform stay)
        {
            if (ship == null)
            {
                yield break;
            }

            PlaceAt(ship, start);
            SetShipActive(ship, true);

            if (stay == null || sailDuration <= 0f)
            {
                PlaceAt(ship, stay);
                yield break;
            }

            var completed = false;
            _sailTween = ship
                .DOMove(stay.position, sailDuration)
                .SetEase(sailEase)
                .SetUpdate(true)
                .OnComplete(() => completed = true);

            yield return new WaitUntil(() => completed);
            _sailTween = null;
            PlaceAt(ship, stay);
        }

        IEnumerator PlayDialogue()
        {
            var system = dialogue != null ? dialogue : UiSystem.Instance != null ? UiSystem.Instance.Dialogue : null;
            if (system == null)
            {
                Debug.LogWarning("[ProloguePerformance] 找不到 DialogueSystem，跳过对话。");
                yield break;
            }

            var completed = false;
            void OnCompleted()
            {
                system.SequenceCompleted -= OnCompleted;
                completed = true;
            }

            system.SequenceCompleted += OnCompleted;
            if (!system.Play(dialogueSequenceId))
            {
                system.SequenceCompleted -= OnCompleted;
                yield break;
            }

            yield return new WaitUntil(() => completed);
        }

        void StopPerformance(bool invokeCompleted)
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            if (_sailTween != null && _sailTween.IsActive())
            {
                _sailTween.Kill();
            }

            _sailTween = null;

            var wasPlaying = _isPlaying;
            _isPlaying = false;

            if (invokeCompleted && wasPlaying)
            {
                Completed?.Invoke();
            }
        }

        void ResolveRefs()
        {
            if (player == null)
            {
                player = FindByName("player");
            }

            if (playerStart == null)
            {
                playerStart = FindByName("player start");
            }

            if (playerStay == null)
            {
                playerStay = FindByName("player stay");
            }

            if (enemy == null)
            {
                enemy = FindByName("enemy");
            }

            if (enemyStart == null)
            {
                enemyStart = FindByName("enemy start");
            }

            if (enemyStay == null)
            {
                enemyStay = FindByName("enemy stay");
            }

            if (dialogue == null && UiSystem.Instance != null)
            {
                dialogue = UiSystem.Instance.Dialogue;
            }
        }

        static Transform FindByName(string objectName)
        {
            var go = GameObject.Find(objectName);
            return go != null ? go.transform : null;
        }

        static void PlaceAt(Transform ship, Transform marker)
        {
            if (ship == null || marker == null)
            {
                return;
            }

            ship.position = marker.position;
        }

        static void SetShipActive(Transform ship, bool active)
        {
            if (ship != null)
            {
                ship.gameObject.SetActive(active);
            }
        }
    }
}
