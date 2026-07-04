using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace NineGrid.GameFlow
{
    /// <summary>
    /// 液压场景材料滑道：从管道倾倒到桌面平台，缓动落位后静止。
    /// 管道遮盖图转为不可见 SpriteMask，材料在管内被裁切、滑出后可见。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HydraulicMaterialLane : MonoBehaviour
    {
        enum MaterialState
        {
            Idle = 0,
            Sliding = 1,
            Settled = 2,
        }

        [Header("Refs")]
        [SerializeField] Transform sceneRoot;
        [SerializeField] Transform pipeMask;
        [SerializeField] Transform spawnPoint;
        [SerializeField] Transform pipeExitPoint;
        [SerializeField] Transform[] materials;
        [SerializeField] Transform[] slotPoints;

        [Header("Motion")]
        [SerializeField] float pipeSlideDuration = 0.55f;
        [SerializeField] float platformSlideDuration = 0.7f;
        [SerializeField] Ease pipeSlideEase = Ease.InQuad;
        [SerializeField] Ease platformSlideEase = Ease.OutCubic;
        [SerializeField] Vector3 materialScale = new Vector3(0.25f, 0.25f, 0.25f);

        readonly List<Tween> _activeTweens = new List<Tween>(4);
        MaterialState[] _states;
        Vector3[] _slotPositions;
        bool _laneReady;

        public int SettledCount
        {
            get
            {
                if (_states == null)
                {
                    return 0;
                }

                var count = 0;
                for (var i = 0; i < _states.Length; i++)
                {
                    if (_states[i] == MaterialState.Settled || _states[i] == MaterialState.Sliding)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int Capacity => materials != null ? materials.Length : 0;

        public bool CanDeliver
        {
            get
            {
                if (!_laneReady || materials == null)
                {
                    return false;
                }

                for (var i = 0; i < _states.Length; i++)
                {
                    if (_states[i] == MaterialState.Idle)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        void Awake()
        {
            ResolveRefs();
            SetupPipeMask();
            CacheSlots();
            PrepareMaterials();
            _laneReady = true;
            ResetLane();
        }

        void OnDestroy()
        {
            KillTweens();
        }

        /// <summary>液压场景入场完成 / 退场时调用，清空桌面材料。</summary>
        public void ResetLane()
        {
            KillTweens();

            if (materials == null || _states == null)
            {
                return;
            }

            for (var i = 0; i < materials.Length; i++)
            {
                var mat = materials[i];
                if (mat == null)
                {
                    continue;
                }

                mat.DOKill();
                mat.gameObject.SetActive(false);
                mat.localScale = materialScale;
                mat.localRotation = Quaternion.identity;
                _states[i] = MaterialState.Idle;
            }
        }

        /// <summary>发射下一份材料：沿管道滑出 → 桌面缓动落位。</summary>
        public bool TryDeliver()
        {
            if (!_laneReady)
            {
                ResolveRefs();
                SetupPipeMask();
                CacheSlots();
                PrepareMaterials();
                _laneReady = true;
            }

            if (materials == null || _states == null || spawnPoint == null || pipeExitPoint == null)
            {
                return false;
            }

            var index = -1;
            for (var i = 0; i < _states.Length; i++)
            {
                if (_states[i] == MaterialState.Idle)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                return false;
            }

            var mat = materials[index];
            if (mat == null)
            {
                return false;
            }

            var slot = GetSlotPosition(index);
            _states[index] = MaterialState.Sliding;

            mat.DOKill();
            mat.position = spawnPoint.position;
            mat.localScale = materialScale;
            mat.localRotation = Quaternion.identity;
            mat.gameObject.SetActive(true);
            ApplyMaskInteraction(mat, SpriteMaskInteraction.VisibleOutsideMask);

            var captured = mat;
            var capturedIndex = index;
            var sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Append(captured.DOMove(pipeExitPoint.position, pipeSlideDuration).SetEase(pipeSlideEase));
            sequence.Append(captured.DOMove(slot, platformSlideDuration).SetEase(platformSlideEase));
            sequence.OnComplete(() =>
            {
                if (captured != null)
                {
                    captured.position = slot;
                    // 落位后不再受遮罩影响，避免边缘闪烁。
                    ApplyMaskInteraction(captured, SpriteMaskInteraction.None);
                }

                if (_states != null && capturedIndex >= 0 && capturedIndex < _states.Length)
                {
                    _states[capturedIndex] = MaterialState.Settled;
                }
            });

            _activeTweens.Add(sequence);
            return true;
        }

        void ResolveRefs()
        {
            if (sceneRoot == null)
            {
                sceneRoot = transform;
            }

            if (pipeMask == null)
            {
                pipeMask = FindDeepChild(sceneRoot, "管道遮盖图");
            }

            if (spawnPoint == null)
            {
                spawnPoint = FindDeepChild(sceneRoot, "MaterialSpawn");
            }

            if (pipeExitPoint == null)
            {
                pipeExitPoint = FindDeepChild(sceneRoot, "MaterialPipeExit");
            }

            if (NeedsResolve(materials))
            {
                materials = new[]
                {
                    FindDeepChild(sceneRoot, "material"),
                    FindDeepChild(sceneRoot, "material (1)"),
                    FindDeepChild(sceneRoot, "material (2)"),
                };
            }

            if (NeedsResolve(slotPoints))
            {
                slotPoints = new[]
                {
                    FindDeepChild(sceneRoot, "MaterialSlot_0"),
                    FindDeepChild(sceneRoot, "MaterialSlot_1"),
                    FindDeepChild(sceneRoot, "MaterialSlot_2"),
                };
            }
        }

        void SetupPipeMask()
        {
            if (pipeMask == null)
            {
                return;
            }

            var renderer = pipeMask.GetComponent<SpriteRenderer>();
            Sprite maskSprite = null;
            if (renderer != null)
            {
                maskSprite = renderer.sprite;
                // 遮盖图只参与裁切，不渲染给玩家看。
                renderer.enabled = false;
            }

            var mask = pipeMask.GetComponent<SpriteMask>();
            if (mask == null)
            {
                mask = pipeMask.gameObject.AddComponent<SpriteMask>();
            }

            if (maskSprite != null)
            {
                mask.sprite = maskSprite;
            }

            mask.alphaCutoff = 0.2f;
            mask.isCustomRangeActive = false;
            mask.frontSortingLayerID = SortingLayer.NameToID("Factory");
            mask.backSortingLayerID = SortingLayer.NameToID("Factory");
        }

        void CacheSlots()
        {
            var count = materials != null ? materials.Length : 0;
            _slotPositions = new Vector3[count];
            _states = new MaterialState[count];

            for (var i = 0; i < count; i++)
            {
                if (slotPoints != null && i < slotPoints.Length && slotPoints[i] != null)
                {
                    _slotPositions[i] = slotPoints[i].position;
                }
                else if (materials[i] != null)
                {
                    // 无槽位标记时，沿桌面从左往右排布。
                    var basePos = materials[0] != null
                        ? new Vector3(materials[0].position.x, materials[0].position.y, 0f)
                        : new Vector3(-2.9f, -3.47f, 0f);
                    _slotPositions[i] = basePos + new Vector3(i * 1.1f, 0f, 0f);
                }
            }
        }

        void PrepareMaterials()
        {
            if (materials == null)
            {
                return;
            }

            for (var i = 0; i < materials.Length; i++)
            {
                var mat = materials[i];
                if (mat == null)
                {
                    continue;
                }

                mat.localScale = materialScale;
                mat.localRotation = Quaternion.identity;
                ApplyMaskInteraction(mat, SpriteMaskInteraction.VisibleOutsideMask);
            }
        }

        Vector3 GetSlotPosition(int index)
        {
            if (_slotPositions != null && index >= 0 && index < _slotPositions.Length)
            {
                return _slotPositions[index];
            }

            return pipeExitPoint != null ? pipeExitPoint.position : Vector3.zero;
        }

        void KillTweens()
        {
            for (var i = 0; i < _activeTweens.Count; i++)
            {
                var tween = _activeTweens[i];
                if (tween != null && tween.IsActive())
                {
                    tween.Kill();
                }
            }

            _activeTweens.Clear();

            if (materials == null)
            {
                return;
            }

            for (var i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null)
                {
                    materials[i].DOKill();
                }
            }
        }

        static void ApplyMaskInteraction(Transform target, SpriteMaskInteraction interaction)
        {
            if (target == null)
            {
                return;
            }

            var renderer = target.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.maskInteraction = interaction;
            }
        }

        static bool NeedsResolve(Transform[] targets)
        {
            if (targets == null || targets.Length == 0)
            {
                return true;
            }

            for (var i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null)
                {
                    return true;
                }
            }

            return false;
        }

        static Transform FindDeepChild(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            var direct = root.Find(childName);
            if (direct != null)
            {
                return direct;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }

                var nested = FindDeepChild(child, childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }
    }
}
