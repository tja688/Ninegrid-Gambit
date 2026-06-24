using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace NineGrid.Presentation.Visuals
{
    /// <summary>
    /// 单/多位数字直接跳变显示，数值变化时可选轻微缩放反馈（增/减）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DirectDigitDisplay : MonoBehaviour
    {
        private static readonly Vector3 DefaultRestScale = Vector3.one;

        [Header("Sprites")]
        [SerializeField] private SpriteRenderer templateRenderer;
        [SerializeField] private Sprite[] digitSprites = new Sprite[10];

        [Header("Layout")]
        [SerializeField, Min(0f)] private float columnGap = 0.02f;

        [Header("Feedback")]
        [SerializeField, Min(0f)] private float punchDuration = 0.12f;
        [SerializeField, Min(1f)] private float punchUpScale = 1.12f;
        [SerializeField, Min(0.01f)] private float punchDownScale = 0.92f;
        [SerializeField] private Ease punchEase = Ease.OutQuad;

        private readonly List<SpriteRenderer> digitRenderers = new();
        private int displayedValue;
        private Vector3 restScale = DefaultRestScale;
        private Vector3 anchorLocalPosition;
        private float digitStep = 0.3125f;
        private bool built;
        private bool anchorCaptured;

        public float PunchDuration => punchDuration;

        private void Awake()
        {
            if (templateRenderer == null)
            {
                templateRenderer = GetComponent<SpriteRenderer>();
            }

            if (templateRenderer != null)
            {
                restScale = templateRenderer.transform.localScale;
            }

            CaptureAnchor();
        }

        private void CaptureAnchor()
        {
            if (anchorCaptured)
            {
                return;
            }

            anchorLocalPosition = transform.localPosition;
            anchorCaptured = true;
        }

        public void ConfigureSprites(Sprite[] sprites)
        {
            if (!HasValidDigitSprites(sprites))
            {
                return;
            }

            digitSprites = sprites;
            digitStep = Mathf.Max(0.001f, sprites[0].bounds.size.x + columnGap);
            built = false;
        }

        public void SnapTo(int value)
        {
            value = Mathf.Max(0, value);
            if (!TryEnsureBuilt(value))
            {
                return;
            }

            displayedValue = value;
            DOTween.Kill(transform);
            ApplyValue(value);
            transform.localScale = restScale;
        }

        public float PlayTo(int value)
        {
            value = Mathf.Max(0, value);
            if (!TryEnsureBuilt(value))
            {
                return 0f;
            }

            if (value == displayedValue)
            {
                return 0f;
            }

            bool increased = value > displayedValue;
            displayedValue = value;
            DOTween.Kill(transform);
            ApplyValue(value);

            if (punchDuration <= 0f)
            {
                transform.localScale = restScale;
                return 0f;
            }

            float peak = increased ? punchUpScale : punchDownScale;
            transform.localScale = restScale;
            transform
                .DOScale(restScale * peak, punchDuration * 0.5f)
                .SetEase(punchEase)
                .SetLoops(2, LoopType.Yoyo)
                .SetTarget(transform);
            return punchDuration;
        }

        private bool TryEnsureBuilt(int value)
        {
            ResolveDigitSprites();
            if (!HasValidDigitSprites(digitSprites))
            {
                return false;
            }

            int requiredDigits = ResolveDigitCount(value);
            if (!built || digitRenderers.Count != requiredDigits)
            {
                BuildDigits(requiredDigits);
                built = true;
            }

            return true;
        }

        private void ResolveDigitSprites()
        {
            if (HasValidDigitSprites(digitSprites))
            {
                return;
            }

            if (templateRenderer == null)
            {
                templateRenderer = GetComponent<SpriteRenderer>();
            }

            if (templateRenderer == null || templateRenderer.sprite == null)
            {
                return;
            }

            Sprite[] loaded = TableNineDigitSpriteLibrary.LoadDigitsFromSameSheet(templateRenderer.sprite);
            if (HasValidDigitSprites(loaded))
            {
                digitSprites = loaded;
                digitStep = Mathf.Max(0.001f, loaded[0].bounds.size.x + columnGap);
            }
        }

        private void BuildDigits(int digitCount)
        {
            ClearExtraDigits();
            CaptureAnchor();

            if (templateRenderer == null)
            {
                templateRenderer = GetComponent<SpriteRenderer>();
            }

            if (templateRenderer == null)
            {
                return;
            }

            restScale = templateRenderer.transform.localScale;
            digitRenderers.Clear();
            digitRenderers.Add(templateRenderer);
            templateRenderer.enabled = true;

            if (digitCount <= 1)
            {
                transform.localPosition = anchorLocalPosition;
                built = true;
                return;
            }

            float groupWidth = digitCount * digitStep;
            float leftX = anchorLocalPosition.x - (groupWidth - digitStep) * 0.5f;
            transform.localPosition = new Vector3(leftX, anchorLocalPosition.y, anchorLocalPosition.z);

            for (var i = 1; i < digitCount; i++)
            {
                var digitObject = new GameObject($"Digit_{i}");
                digitObject.transform.SetParent(transform, false);
                digitObject.transform.localPosition = new Vector3(i * digitStep, 0f, 0f);
                digitObject.transform.localScale = Vector3.one;

                var renderer = digitObject.AddComponent<SpriteRenderer>();
                renderer.sprite = digitSprites[0];
                renderer.sortingLayerID = templateRenderer.sortingLayerID;
                renderer.sortingOrder = templateRenderer.sortingOrder;
                renderer.color = templateRenderer.color;
                renderer.flipX = templateRenderer.flipX;
                renderer.flipY = templateRenderer.flipY;
                digitRenderers.Add(renderer);
            }

            built = true;
        }

        private void ApplyValue(int value)
        {
            for (var i = 0; i < digitRenderers.Count; i++)
            {
                SpriteRenderer renderer = digitRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                int place = (int)Mathf.Pow(10f, digitRenderers.Count - i - 1);
                int digit = Mathf.FloorToInt(value / (float)place) % 10;
                renderer.sprite = digitSprites[digit];
                bool hideLeading = place > 1 && value < place;
                renderer.enabled = !hideLeading;
            }
        }

        private void ClearExtraDigits()
        {
            for (var i = digitRenderers.Count - 1; i >= 1; i--)
            {
                if (digitRenderers[i] != null)
                {
                    Destroy(digitRenderers[i].gameObject);
                }
            }

            digitRenderers.Clear();
            built = false;
        }

        private static bool HasValidDigitSprites(Sprite[] sprites)
        {
            if (sprites == null || sprites.Length < 10)
            {
                return false;
            }

            for (var i = 0; i < 10; i++)
            {
                if (sprites[i] == null)
                {
                    return false;
                }
            }

            return true;
        }

        private static int ResolveDigitCount(int value)
        {
            if (value >= 100)
            {
                return 3;
            }

            if (value >= 10)
            {
                return 2;
            }

            return 1;
        }
    }
}
