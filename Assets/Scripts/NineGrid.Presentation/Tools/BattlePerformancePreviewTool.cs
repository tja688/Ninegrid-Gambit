using System.Collections.Generic;
using DG.Tweening;
using NineGrid.Presentation.Flow.Battle;
using NineGrid.Presentation.Shared;
using UnityEngine;

namespace NineGrid.Presentation.Tools
{
    /// <summary>
    /// 战斗表演预览壳：按 6 玩家攻击、按 7 怪物反击、按 8 怪物击杀玩家、按 9 玩家击杀怪；
    /// 每次按键轮换方向（右→上→左→下）。自动在锚点生成占位演员并驱动对应 Performance。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattlePerformancePreviewTool : MonoBehaviour
    {
        [Header("Hotkeys")]
        [SerializeField] private KeyCode attackKey = KeyCode.Alpha6;
        [SerializeField] private KeyCode counterattackKey = KeyCode.Alpha7;
        [SerializeField] private KeyCode counterattackKillKey = KeyCode.Alpha8;
        [SerializeField] private KeyCode killKey = KeyCode.Alpha9;

        [Header("Stage")]
        [SerializeField] private Transform stageRoot;
        [SerializeField] private Transform centerAnchor;
        [SerializeField] private Vector3 rightOffset = new(2.0625f, 0f, 0f);
        [SerializeField] private Vector3 upOffset = new(0f, 2.5f, 0f);
        [SerializeField] private Vector3 leftOffset = new(-2.0625f, 0f, 0f);
        [SerializeField] private Vector3 downOffset = new(0f, -2.5f, 0f);

        [Header("Preview Actors")]
        [SerializeField] private Transform previewActorsRoot;
        [SerializeField] private GameObject playerPreviewPrefab;
        [SerializeField] private GameObject enemyPreviewPrefab;
        [SerializeField] private int playerSortingOrder = 100;
        [SerializeField] private int enemySortingOrder = 99;

        [Header("Performances")]
        [SerializeField] private CardAttackFlow attackPerformance;
        [SerializeField] private CardKillFlow killPerformance;
        [SerializeField] private CounterattackFlow counterattackPerformance;
        [SerializeField] private CounterattackKillFlow counterattackKillPerformance;

        private readonly List<Transform> spawnedActors = new();
        private int attackDirectionCursor;
        private int killDirectionCursor;
        private int counterattackDirectionCursor;
        private int counterattackKillDirectionCursor;

        private Transform ResolvedStageRoot => stageRoot != null ? stageRoot : transform;
        private Transform ResolvedActorsRoot => previewActorsRoot != null ? previewActorsRoot : ResolvedStageRoot;

        private void Awake()
        {
            EnsurePerformanceReferences();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (Input.GetKeyDown(attackKey))
            {
                PlayAttackStep();
                return;
            }

            if (Input.GetKeyDown(counterattackKey))
            {
                PlayCounterattackStep();
                return;
            }

            if (Input.GetKeyDown(counterattackKillKey))
            {
                PlayCounterattackKillStep();
                return;
            }

            if (Input.GetKeyDown(killKey))
            {
                PlayKillStep();
            }
        }

        [ContextMenu("Play Attack Step (Same As Key 6)")]
        public void PlayAttackStep()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning($"[{nameof(BattlePerformancePreviewTool)}] Enter Play Mode first.", this);
                return;
            }

            if (IsAnyPerformancePlaying())
            {
                return;
            }

            CardBattleDirection direction = CardBattleDirectionUtil.NextInCycle(ref attackDirectionCursor);
            SetupActorsForDirection(direction, out Transform player, out Transform enemy);
            EnsurePerformanceReferences();
            attackPerformance.Play(player, enemy, direction);
        }

        [ContextMenu("Play Kill Step (Same As Key 9)")]
        public void PlayKillStep()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning($"[{nameof(BattlePerformancePreviewTool)}] Enter Play Mode first.", this);
                return;
            }

            if (IsAnyPerformancePlaying())
            {
                return;
            }

            CardBattleDirection direction = CardBattleDirectionUtil.NextInCycle(ref killDirectionCursor);
            SetupActorsForDirection(direction, out Transform player, out Transform enemy);
            EnsurePerformanceReferences();
            killPerformance.Play(player, enemy, direction);
        }

        [ContextMenu("Play Counterattack Step (Same As Key 7)")]
        public void PlayCounterattackStep()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning($"[{nameof(BattlePerformancePreviewTool)}] Enter Play Mode first.", this);
                return;
            }

            if (IsAnyPerformancePlaying())
            {
                return;
            }

            CardBattleDirection direction = CardBattleDirectionUtil.NextInCycle(ref counterattackDirectionCursor);
            SetupActorsForDirection(direction, out Transform player, out Transform enemy);
            EnsurePerformanceReferences();
            counterattackPerformance.Play(enemy, player, direction);
        }

        [ContextMenu("Play Counterattack Kill Step (Same As Key 8)")]
        public void PlayCounterattackKillStep()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning($"[{nameof(BattlePerformancePreviewTool)}] Enter Play Mode first.", this);
                return;
            }

            if (IsAnyPerformancePlaying())
            {
                return;
            }

            CardBattleDirection direction = CardBattleDirectionUtil.NextInCycle(ref counterattackKillDirectionCursor);
            SetupActorsForDirection(direction, out Transform player, out Transform enemy);
            EnsurePerformanceReferences();
            counterattackKillPerformance.Play(enemy, player, direction);
        }

        [ContextMenu("Clear Preview Actors")]
        public void ClearPreviewActors()
        {
            ClearPreviewActors(resetDirectionCursor: true);
        }

        private void ClearPreviewActors(bool resetDirectionCursor)
        {
            attackPerformance?.StopAndRestore();
            killPerformance?.StopAndRestore();
            counterattackPerformance?.StopAndRestore();
            counterattackKillPerformance?.StopAndRestore();

            SelectionOptionVisual.DestroyActors(spawnedActors);

            if (resetDirectionCursor)
            {
                attackDirectionCursor = 0;
                killDirectionCursor = 0;
                counterattackDirectionCursor = 0;
                counterattackKillDirectionCursor = 0;
            }
        }

        private bool IsAnyPerformancePlaying()
        {
            return (attackPerformance != null && attackPerformance.IsPlaying)
                || (killPerformance != null && killPerformance.IsPlaying)
                || (counterattackPerformance != null && counterattackPerformance.IsPlaying)
                || (counterattackKillPerformance != null && counterattackKillPerformance.IsPlaying);
        }

        private void SetupActorsForDirection(
            CardBattleDirection direction,
            out Transform player,
            out Transform enemy)
        {
            ClearPreviewActors(resetDirectionCursor: false);

            Vector3 center = ResolveCenterPosition();
            Vector3 enemyOffset = ResolveEnemyOffset(direction);
            Transform parent = ResolvedActorsRoot;

            player = CreatePreviewActor(
                parent,
                "BattlePreview_Player",
                playerPreviewPrefab,
                center,
                playerSortingOrder);
            enemy = CreatePreviewActor(
                parent,
                "BattlePreview_Enemy",
                enemyPreviewPrefab,
                center + enemyOffset,
                enemySortingOrder);

            spawnedActors.Add(player);
            spawnedActors.Add(enemy);
        }

        private Vector3 ResolveCenterPosition()
        {
            Transform playerAnchor = ResolvePlayerCenterAnchor();
            if (playerAnchor != null)
            {
                return playerAnchor.position;
            }

            return ResolvedStageRoot.position;
        }

        /// <summary>
        /// 优先解析九宫格玩家中心锚（slot5_Player）；避免误用外圈 slot 作 centerAnchor。
        /// </summary>
        private Transform ResolvePlayerCenterAnchor()
        {
            if (centerAnchor != null)
            {
                if (IsPlayerCenterSlot(centerAnchor))
                {
                    return centerAnchor;
                }

                Transform ringRoot = centerAnchor.parent;
                if (ringRoot != null)
                {
                    for (int i = 0; i < ringRoot.childCount; i++)
                    {
                        Transform child = ringRoot.GetChild(i);
                        if (IsPlayerCenterSlot(child))
                        {
                            return child;
                        }
                    }
                }
            }

            return centerAnchor;
        }

        private static bool IsPlayerCenterSlot(Transform slot)
        {
            if (slot == null)
            {
                return false;
            }

            string slotName = slot.name;
            if (slotName.Contains("Player"))
            {
                return true;
            }

            return slotName == "slot5" || slotName.StartsWith("slot5_");
        }

        private Vector3 ResolveEnemyOffset(CardBattleDirection direction)
        {
            return direction switch
            {
                CardBattleDirection.Right => rightOffset,
                CardBattleDirection.Up => upOffset,
                CardBattleDirection.Left => leftOffset,
                CardBattleDirection.Down => downOffset,
                _ => rightOffset,
            };
        }

        private static Transform CreatePreviewActor(
            Transform parent,
            string name,
            GameObject prefab,
            Vector3 worldPosition,
            int sortingOrder)
        {
            Transform actor = SelectionOptionVisual.CreatePreviewCard(
                parent,
                0,
                prefab,
                parent.InverseTransformPoint(worldPosition),
                0f,
                sortingOrder);
            actor.name = name;
            actor.position = worldPosition;
            return actor;
        }

        private void EnsurePerformanceReferences()
        {
            if (attackPerformance == null)
            {
                attackPerformance = GetComponent<CardAttackFlow>();
            }

            if (killPerformance == null)
            {
                killPerformance = GetComponent<CardKillFlow>();
            }

            if (attackPerformance == null)
            {
                attackPerformance = gameObject.AddComponent<CardAttackFlow>();
            }

            if (killPerformance == null)
            {
                killPerformance = gameObject.AddComponent<CardKillFlow>();
            }

            if (counterattackPerformance == null)
            {
                counterattackPerformance = GetComponent<CounterattackFlow>();
            }

            if (counterattackKillPerformance == null)
            {
                counterattackKillPerformance = GetComponent<CounterattackKillFlow>();
            }

            if (counterattackPerformance == null)
            {
                counterattackPerformance = gameObject.AddComponent<CounterattackFlow>();
            }

            if (counterattackKillPerformance == null)
            {
                counterattackKillPerformance = gameObject.AddComponent<CounterattackKillFlow>();
            }
        }

        private void OnDestroy()
        {
            ClearPreviewActors();
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
            {
                attackPerformance?.StopAndRestore();
                killPerformance?.StopAndRestore();
                counterattackPerformance?.StopAndRestore();
                counterattackKillPerformance?.StopAndRestore();
            }
        }
    }
}
