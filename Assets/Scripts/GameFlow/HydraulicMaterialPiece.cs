using DG.Tweening;
using NineGrid.Presentation.Visuals;
using UnityEngine;

namespace NineGrid.GameFlow
{
    public enum HydraulicMaterialHome
    {
        Pool = 0,
        Sliding = 1,
        Table = 2,
        Anvil = 3,
        Dragging = 4,
    }

    /// <summary>
    /// 单份液压材料：桌面 / 锻造台 / 拖拽状态，以及锻造台漂浮微动。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class HydraulicMaterialPiece : MonoBehaviour
    {
        [SerializeField] SpriteRenderer spriteRenderer;
        [SerializeField] SelectableSceneElement selectable;
        [SerializeField] float bobAmplitude = 0.045f;
        [SerializeField] float bobSpeed = 2.2f;

        float _bobPhase;
        Tween _moveTween;
        Tween _scaleTween;
        int _baseSortingOrder = 2;

        public HydraulicMaterialHome Home { get; private set; } = HydraulicMaterialHome.Pool;
        public int TableSlot { get; private set; } = -1;
        public int AnvilIndex { get; private set; } = -1;
        public int StackIndex { get; private set; } = -1;
        public Vector3 RestPosition { get; private set; }
        public bool Bobbing { get; private set; }
        public bool IsInteractable =>
            Home == HydraulicMaterialHome.Table || Home == HydraulicMaterialHome.Anvil;

        public SpriteRenderer SpriteRenderer => spriteRenderer;
        public SelectableSceneElement Selectable => selectable;

        void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (selectable == null)
            {
                selectable = GetComponent<SelectableSceneElement>();
            }

            _bobPhase = Random.Range(0f, Mathf.PI * 2f);
            if (spriteRenderer != null)
            {
                _baseSortingOrder = spriteRenderer.sortingOrder;
            }
        }

        void LateUpdate()
        {
            if (!Bobbing || Home != HydraulicMaterialHome.Anvil)
            {
                return;
            }

            var offset = Mathf.Sin(Time.unscaledTime * bobSpeed + _bobPhase) * bobAmplitude;
            transform.position = RestPosition + new Vector3(0f, offset, 0f);
        }

        public void ConfigureSelectable()
        {
            if (selectable == null)
            {
                selectable = gameObject.GetComponent<SelectableSceneElement>();
                if (selectable == null)
                {
                    selectable = gameObject.AddComponent<SelectableSceneElement>();
                }
            }
        }

        public void SetPoolHidden(Vector3 tableScale)
        {
            KillMotion();
            Home = HydraulicMaterialHome.Pool;
            TableSlot = -1;
            AnvilIndex = -1;
            StackIndex = -1;
            Bobbing = false;
            RestPosition = transform.position;
            transform.localScale = tableScale;
            transform.localRotation = Quaternion.identity;
            SetSortingOrder(_baseSortingOrder);
            SetSelected(false);
            gameObject.SetActive(false);
        }

        public void BeginSlide(Vector3 spawn, Vector3 tableScale)
        {
            KillMotion();
            Home = HydraulicMaterialHome.Sliding;
            TableSlot = -1;
            AnvilIndex = -1;
            StackIndex = -1;
            Bobbing = false;
            transform.position = spawn;
            transform.localScale = tableScale;
            transform.localRotation = Quaternion.identity;
            RestPosition = spawn;
            SetSortingOrder(_baseSortingOrder + 5);
            SetSelected(false);
            gameObject.SetActive(true);
        }

        public void SettleOnTable(int slotIndex, Vector3 slotPosition, Vector3 tableScale, float duration, Ease ease)
        {
            KillMotion();
            Home = HydraulicMaterialHome.Table;
            TableSlot = slotIndex;
            AnvilIndex = -1;
            StackIndex = -1;
            Bobbing = false;
            RestPosition = slotPosition;
            gameObject.SetActive(true);
            SetSortingOrder(_baseSortingOrder);
            _scaleTween = transform.DOScale(tableScale, duration * 0.5f).SetEase(Ease.OutQuad).SetUpdate(true);
            _moveTween = transform.DOMove(slotPosition, duration).SetEase(ease).SetUpdate(true);
        }

        public void SnapToTable(int slotIndex, Vector3 slotPosition, Vector3 tableScale)
        {
            KillMotion();
            Home = HydraulicMaterialHome.Table;
            TableSlot = slotIndex;
            AnvilIndex = -1;
            StackIndex = -1;
            Bobbing = false;
            RestPosition = slotPosition;
            transform.position = slotPosition;
            transform.localScale = tableScale;
            SetSortingOrder(_baseSortingOrder);
            gameObject.SetActive(true);
        }

        public void BeginDrag(int dragSortingOrder)
        {
            KillMotion();
            Bobbing = false;
            Home = HydraulicMaterialHome.Dragging;
            SetSortingOrder(dragSortingOrder);
            SetSelected(true);
        }

        public void PlaceOnAnvil(
            int anvilIndex,
            int stackIndex,
            Vector3 restPosition,
            Vector3 anvilScale,
            float duration,
            Ease ease,
            bool enableBob)
        {
            KillMotion();
            Home = HydraulicMaterialHome.Anvil;
            TableSlot = -1;
            AnvilIndex = anvilIndex;
            StackIndex = stackIndex;
            RestPosition = restPosition;
            gameObject.SetActive(true);
            SetSortingOrder(_baseSortingOrder + stackIndex);
            SetSelected(false);

            if (duration <= 0f)
            {
                transform.position = restPosition;
                transform.localScale = anvilScale;
                Bobbing = enableBob;
                return;
            }

            Bobbing = false;
            _scaleTween = transform.DOScale(anvilScale, duration * 0.6f).SetEase(Ease.OutQuad).SetUpdate(true);
            _moveTween = transform
                .DOMove(restPosition, duration)
                .SetEase(ease)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    RestPosition = restPosition;
                    Bobbing = enableBob;
                });
        }

        public void FollowPointer(Vector3 worldPosition)
        {
            transform.position = worldPosition;
            RestPosition = worldPosition;
        }

        public void SetSelected(bool selected)
        {
            if (selectable != null)
            {
                selectable.SetHovered(selected);
            }
        }

        public void SetMaskInteraction(SpriteMaskInteraction interaction)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.maskInteraction = interaction;
            }
        }

        public void SetSortingOrder(int order)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.sortingOrder = order;
            }
        }

        public void KillMotion()
        {
            if (_moveTween != null && _moveTween.IsActive())
            {
                _moveTween.Kill();
            }

            if (_scaleTween != null && _scaleTween.IsActive())
            {
                _scaleTween.Kill();
            }

            _moveTween = null;
            _scaleTween = null;
            transform.DOKill();
        }

        void OnDestroy()
        {
            KillMotion();
        }
    }
}
