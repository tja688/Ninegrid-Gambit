using System;
using DG.Tweening;
using UnityEngine;

namespace NineGrid.Presentation.Visuals
{
    /// <summary>
    /// 单数位「滚筒」数字：0–9 竖条 + 遮罩视口，数值变化时沿最短路径滚动。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DrumRollDigitDisplay : MonoBehaviour
    {
        [Header("Sprites")]
        [SerializeField] private SpriteRenderer templateRenderer;
        [SerializeField] private Sprite[] digitSprites = new Sprite[10];

        [Header("Layout")]
        [SerializeField, Min(0.001f)] private float digitStep = 0.3125f;
        [SerializeField] private int maxDigits = 1;
        [SerializeField, Min(0f)] private float columnGap = 0.02f;

        [Header("Motion")]
        [SerializeField, Min(0.01f)] private float rollDuration = 0.35f;
        [SerializeField] private Ease rollEase = Ease.OutCubic;

        private Transform viewportRoot;
        private ColumnState[] columns = Array.Empty<ColumnState>();
        private int displayedValue;
        private bool built;
        private Tween activeTween;

        private sealed class ColumnState
        {
            public Transform Root;
            public Transform Strip;
            public int Place = 1;
            public float AnimatedPlaceValue;
            public GameObject ViewportObject;
        }

        public bool IsRolling => activeTween != null && activeTween.IsActive();

        public float RollDuration => rollDuration;

        private void OnDisable()
        {
            KillTween();
        }

        public void ConfigureSprites(Sprite[] sprites)
        {
            if (!HasValidDigitSprites(sprites))
            {
                return;
            }

            digitSprites = sprites;
            digitStep = Mathf.Max(0.001f, sprites[0].bounds.size.y);
            InvalidateBuild();
        }

        public void SnapTo(int value)
        {
            value = Mathf.Max(0, value);
            if (!TryEnsureBuilt(value))
            {
                return;
            }

            displayedValue = value;
            KillTween();
            ApplyValueToColumns(value, animateStrip: false);
        }

        public float PlayTo(int value, Action onComplete = null)
        {
            value = Mathf.Max(0, value);
            if (!TryEnsureBuilt(value))
            {
                onComplete?.Invoke();
                return 0f;
            }

            if (value == displayedValue && !IsRolling)
            {
                onComplete?.Invoke();
                return 0f;
            }

            KillTween();
            int fromValue = displayedValue;
            displayedValue = value;
            EnsureColumnCount(ResolveDigitCount(value));

            float maxDuration = 0f;
            Sequence sequence = DOTween.Sequence().SetTarget(this);
            for (var i = 0; i < columns.Length; i++)
            {
                ColumnState column = columns[i];
                int fromDigit = GetPlaceValue(fromValue, column.Place);
                int toDigit = GetPlaceValue(value, column.Place);
                if (fromDigit == toDigit)
                {
                    continue;
                }

                float fromAnimated = column.AnimatedPlaceValue;
                float toAnimated = fromAnimated + ShortestDigitDelta(fromDigit, toDigit);
                maxDuration = Mathf.Max(maxDuration, rollDuration);

                sequence.Join(
                    DOTween.To(
                            () => column.AnimatedPlaceValue,
                            v =>
                            {
                                column.AnimatedPlaceValue = v;
                                ApplyStripPosition(column, v);
                            },
                            toAnimated,
                            rollDuration)
                        .SetEase(rollEase)
                        .SetTarget(this));
            }

            if (maxDuration <= 0f)
            {
                SnapTo(value);
                onComplete?.Invoke();
                return 0f;
            }

            sequence.OnComplete(() =>
            {
                SnapTo(value);
                onComplete?.Invoke();
            });
            activeTween = sequence;
            return maxDuration;
        }

        private bool TryEnsureBuilt(int value)
        {
            ResolveDigitSprites();
            if (!HasValidDigitSprites(digitSprites))
            {
                return false;
            }

            int requiredDigits = ResolveDigitCount(value);
            if (!built || columns.Length != requiredDigits)
            {
                BuildColumns(requiredDigits);
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
                digitStep = Mathf.Max(0.001f, loaded[0].bounds.size.y);
            }
        }

        private void BuildColumns(int digitCount)
        {
            if (templateRenderer == null)
            {
                templateRenderer = GetComponent<SpriteRenderer>();
            }

            if (templateRenderer != null)
            {
                templateRenderer.enabled = false;
            }

            InvalidateBuild();

            digitCount = Mathf.Clamp(digitCount, 1, maxDigits);
            maxDigits = Mathf.Max(maxDigits, digitCount);
            columns = new ColumnState[digitCount];

            float totalWidth = digitCount * digitStep + (digitCount - 1) * columnGap;
            float startX = -totalWidth * 0.5f + digitStep * 0.5f;

            for (var i = 0; i < digitCount; i++)
            {
                int place = (int)Mathf.Pow(10f, digitCount - i - 1);

                var viewportObject = new GameObject(i == 0 ? "DrumRollViewport" : $"DrumRollViewport_{i}");
                viewportObject.transform.SetParent(transform, false);
                viewportObject.transform.localPosition = new Vector3(startX + i * (digitStep + columnGap), 0f, 0f);

                var mask = viewportObject.AddComponent<SpriteMask>();
                mask.sprite = digitSprites[0];
                mask.alphaCutoff = 0.01f;

                var columnRoot = new GameObject("DigitColumn");
                columnRoot.transform.SetParent(viewportObject.transform, false);

                var stripObject = new GameObject("Strip");
                stripObject.transform.SetParent(columnRoot.transform, false);

                int sortingLayerId = templateRenderer != null ? templateRenderer.sortingLayerID : 0;
                int sortingOrder = templateRenderer != null ? templateRenderer.sortingOrder + 1 : 1;

                for (var digit = 0; digit < 10; digit++)
                {
                    var digitObject = new GameObject($"Digit_{digit}");
                    digitObject.transform.SetParent(stripObject.transform, false);
                    digitObject.transform.localPosition = new Vector3(0f, -digit * digitStep, 0f);

                    var renderer = digitObject.AddComponent<SpriteRenderer>();
                    renderer.sprite = digitSprites[digit];
                    renderer.sortingLayerID = sortingLayerId;
                    renderer.sortingOrder = sortingOrder;
                    renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                }

                columns[i] = new ColumnState
                {
                    Root = columnRoot.transform,
                    Strip = stripObject.transform,
                    Place = place,
                    AnimatedPlaceValue = 0f,
                    ViewportObject = viewportObject
                };
            }

            viewportRoot = columns.Length > 0 ? columns[0].ViewportObject.transform.parent : null;
            built = true;
        }

        private void EnsureColumnCount(int digitCount)
        {
            digitCount = Mathf.Clamp(digitCount, 1, maxDigits);
            if (columns.Length == digitCount)
            {
                return;
            }

            int value = displayedValue;
            BuildColumns(digitCount);
            ApplyValueToColumns(value, animateStrip: false);
        }

        private void ApplyValueToColumns(int value, bool animateStrip)
        {
            for (var i = 0; i < columns.Length; i++)
            {
                ColumnState column = columns[i];
                int placeValue = GetPlaceValue(value, column.Place);
                column.AnimatedPlaceValue = placeValue;
                ApplyStripPosition(column, placeValue);

                bool hideLeading = column.Place > 1 && value < column.Place;
                if (column.ViewportObject != null)
                {
                    column.ViewportObject.SetActive(!hideLeading);
                }
            }
        }

        private void InvalidateBuild()
        {
            built = false;
            KillTween();

            if (columns.Length > 0)
            {
                for (var i = 0; i < columns.Length; i++)
                {
                    if (columns[i].ViewportObject != null)
                    {
                        Destroy(columns[i].ViewportObject);
                    }
                }
            }
            else if (viewportRoot != null)
            {
                Destroy(viewportRoot.gameObject);
            }

            viewportRoot = null;
            columns = Array.Empty<ColumnState>();
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

        private static int GetPlaceValue(int value, int place)
        {
            if (place <= 0)
            {
                return 0;
            }

            return Mathf.FloorToInt(value / (float)place) % 10;
        }

        private static int ShortestDigitDelta(int fromDigit, int toDigit)
        {
            int offset = (10 + toDigit - fromDigit) % 10;
            if (offset > 5)
            {
                offset -= 10;
            }

            return offset;
        }

        private void ApplyStripPosition(ColumnState column, float animatedPlaceValue)
        {
            float wrapped = animatedPlaceValue % 10f;
            if (wrapped < 0f)
            {
                wrapped += 10f;
            }

            column.Strip.localPosition = new Vector3(0f, wrapped * digitStep, 0f);
        }

        private void KillTween()
        {
            if (activeTween != null && activeTween.IsActive())
            {
                activeTween.Kill();
            }

            activeTween = null;
            DOTween.Kill(this);
        }
    }
}
