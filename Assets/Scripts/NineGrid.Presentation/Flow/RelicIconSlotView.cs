using TMPro;
using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 遗物栏单槽显示视图：锚点保留 Collider/HitProxy；子树挂「标准遗物图标模板」只负责图标与计数。
    /// </summary>
    internal sealed class RelicIconSlotView
    {
        public const string IconChildName = "图标本体";
        public const string CounterChildName = "计数";
        public const string DisplayRootName = "标准遗物图标模板";

        public Transform SlotRoot { get; }
        public ContentIconSlotHitProxy HitProxy { get; }
        public Transform DisplayRoot { get; private set; }
        public SpriteRenderer IconRenderer { get; private set; }
        public TextMeshPro CounterText { get; private set; }

        public RelicIconSlotView(Transform slotRoot)
        {
            SlotRoot = slotRoot;
            HitProxy = slotRoot != null ? slotRoot.GetComponent<ContentIconSlotHitProxy>() : null;
        }

        public bool IsValid =>
            SlotRoot != null && DisplayRoot != null && IconRenderer != null;

        public void EnsureDisplay(GameObject iconPrefab)
        {
            if (SlotRoot == null)
            {
                return;
            }

            if (DisplayRoot == null)
            {
                DisplayRoot = FindExistingDisplayRoot(SlotRoot);
            }

            if (DisplayRoot == null && iconPrefab != null)
            {
                var instance = Object.Instantiate(iconPrefab, SlotRoot, false);
                instance.name = DisplayRootName;
                var t = instance.transform;
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;
                t.localScale = Vector3.one;
                DisplayRoot = t;
            }

            if (DisplayRoot == null)
            {
                return;
            }

            if (IconRenderer == null)
            {
                var icon = DisplayRoot.Find(IconChildName);
                IconRenderer = icon != null
                    ? icon.GetComponent<SpriteRenderer>()
                    : DisplayRoot.GetComponentInChildren<SpriteRenderer>(true);
            }

            if (CounterText == null)
            {
                var counter = DisplayRoot.Find(CounterChildName);
                CounterText = counter != null
                    ? counter.GetComponent<TextMeshPro>()
                    : DisplayRoot.GetComponentInChildren<TextMeshPro>(true);
            }

            // 锚点自身 SpriteRenderer 不再承担业务图标（命中盒仍可留空 SR）。
            var anchorSr = SlotRoot.GetComponent<SpriteRenderer>();
            if (anchorSr != null)
            {
                anchorSr.sprite = null;
                anchorSr.enabled = false;
            }
        }

        public void ApplySprite(Sprite sprite)
        {
            if (IconRenderer == null)
            {
                return;
            }

            IconRenderer.sprite = sprite;
            IconRenderer.enabled = sprite != null;
            if (DisplayRoot != null)
            {
                DisplayRoot.gameObject.SetActive(sprite != null);
            }
        }

        public void ClearVisual()
        {
            ApplySprite(null);
            SetCounter(null, visible: false);
            if (HitProxy != null)
            {
                HitProxy.DefId = string.Empty;
            }
        }

        public void BindDefId(string defId)
        {
            if (HitProxy != null)
            {
                HitProxy.DefId = defId ?? string.Empty;
            }
        }

        public void SetCounter(string text, bool visible)
        {
            if (CounterText == null)
            {
                return;
            }

            CounterText.gameObject.SetActive(visible);
            if (visible)
            {
                CounterText.text = text ?? string.Empty;
            }
        }

        public void SetDisplayVisible(bool visible)
        {
            if (DisplayRoot != null)
            {
                DisplayRoot.gameObject.SetActive(visible);
            }
        }

        private static Transform FindExistingDisplayRoot(Transform slotRoot)
        {
            for (var i = 0; i < slotRoot.childCount; i++)
            {
                var child = slotRoot.GetChild(i);
                if (child != null
                    && (child.name == DisplayRootName
                        || child.name.StartsWith(DisplayRootName, System.StringComparison.Ordinal)))
                {
                    return child;
                }
            }

            return null;
        }
    }
}
