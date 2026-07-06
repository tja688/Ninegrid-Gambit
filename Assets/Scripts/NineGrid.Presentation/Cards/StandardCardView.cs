using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.Presentation.Cards
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SortingGroup))]
    public sealed class StandardCardView : MonoBehaviour
    {
        private const int ArmorDigitSlotCount = 3;
        private static readonly float[] ArmorDigitSlotX =
        {
            0.0861454f,
            0.3361454f,
            0.5861454f,
        };

        private const int ArmorValueThreshold = 7;
        private const int MaxArmor = 999;

        [Header("Sprite Library")]
        [SerializeField] private PixelCardPackSpriteLibrary spriteLibrary;

        [Header("Anchors")]
        [SerializeField] private Transform attackAnchor;
        [SerializeField] private Transform lifeAnchor;
        [SerializeField] private Transform armorBlocksAnchor;
        [SerializeField] private Transform armorValueAnchor;

        [Header("Renderers")]
        [SerializeField] private SpriteRenderer cardBackgroundRenderer;
        [SerializeField] private SpriteRenderer cardFrameRenderer;
        [SerializeField] private SpriteRenderer mainIconRenderer;

        [Header("Layout")]
        [SerializeField] private float digitSpacing = 0.04f;
        [SerializeField] private float armorDigitSpacing = 0.25f;
        [SerializeField] private Vector3 armorDigitScale = Vector3.one;
        [SerializeField] private Color armorDigitColor = new(249f / 255f, 194f / 255f, 43f / 255f, 1f);
        [SerializeField] private float armorBlockSpacing = 0.156f;
        [SerializeField] private Vector3 armorBlockScale = new(1f, 1f, 1f);

        [Header("Sorting Orders (low -> high)")]
        [SerializeField] private int frameSortingOrder = -10;
        [SerializeField] private int backgroundSortingOrder = -9;
        [SerializeField] private int mainIconSortingOrder = 0;
        [SerializeField] private int statSortingOrder = 1;
        [SerializeField] private int armorSortingOrder = 2;

        [Header("Defaults")]
        [SerializeField] private int attack = 3;
        [SerializeField] private int health = 5;
        [SerializeField] private int armor;

        private SortingGroup _sortingGroup;
        private PixelDigitDisplay _attackDigits;
        private PixelDigitDisplay _lifeDigits;
        private PixelDigitDisplay _armorValueDigits;
        private ArmorBlockDisplay _armorBlocks;

        public int Attack => attack;
        public int Health => health;
        public int Armor => armor;

        private void Awake()
        {
            _sortingGroup = GetComponent<SortingGroup>();
            if (_sortingGroup != null)
            {
                _sortingGroup.sortingOrder = 0;
            }

            AutoWireReferences();
            DisableLegacyStatRenderers();
            ApplySortingOrders();
            BuildDisplays();
            RefreshAll(false);
        }

        public void SetAttack(int value, bool animate = true)
        {
            attack = Mathf.Max(0, value);
            _attackDigits?.SetValue(attack, animate);
        }

        public void SetHealth(int value, bool animate = true)
        {
            health = Mathf.Max(0, value);
            _lifeDigits?.SetValue(health, animate);
        }

        public void SetArmor(int value, bool animate = true)
        {
            armor = Mathf.Clamp(value, 0, MaxArmor);
            RefreshArmorDisplay(animate);
        }

        public void AddAttack(int delta, bool animate = true)
        {
            SetAttack(attack + delta, animate);
        }

        public void AddHealth(int delta, bool animate = true)
        {
            SetHealth(health + delta, animate);
        }

        public void AddArmor(int delta, bool animate = true)
        {
            SetArmor(armor + delta, animate);
        }

        public void SetMainIcon(Sprite sprite)
        {
            if (mainIconRenderer != null)
            {
                mainIconRenderer.sprite = sprite;
            }
        }

        public void SetFrameColor(Color color)
        {
            if (cardFrameRenderer != null)
            {
                cardFrameRenderer.color = color;
            }
        }

        private void BuildDisplays()
        {
            if (spriteLibrary == null)
            {
                Debug.LogWarning($"{nameof(StandardCardView)} on {name} is missing sprite library.", this);
                return;
            }

            var sortingLayerId = ResolveSortingLayerId();
            _attackDigits = new PixelDigitDisplay(attackAnchor, spriteLibrary, digitSpacing, sortingLayerId, statSortingOrder, this);
            _lifeDigits = new PixelDigitDisplay(lifeAnchor, spriteLibrary, digitSpacing, sortingLayerId, statSortingOrder, this);
            var armorDigitSlotOffsets = BuildArmorDigitSlotOffsets();
            _armorValueDigits = new PixelDigitDisplay(
                armorValueAnchor,
                spriteLibrary,
                armorDigitSpacing,
                sortingLayerId,
                armorSortingOrder,
                this,
                DigitAlignment.FixedSlotsOnesRight,
                armorDigitSlotOffsets,
                armorDigitScale,
                armorDigitColor);
            _armorBlocks = new ArmorBlockDisplay(armorBlocksAnchor, spriteLibrary, armorBlockScale, armorBlockSpacing, sortingLayerId, armorSortingOrder, this);
        }

        private void RefreshAll(bool animate)
        {
            SetAttack(attack, animate);
            SetHealth(health, animate);
            SetArmor(armor, animate);
        }

        private void RefreshArmorDisplay(bool animate)
        {
            if (_armorBlocks == null || _armorValueDigits == null)
            {
                return;
            }

            if (armor <= 0)
            {
                _armorBlocks.SetCount(0, animate);
                _armorValueDigits.SetVisible(false);
                return;
            }

            if (armor <= ArmorValueThreshold)
            {
                _armorValueDigits.SetVisible(false);
                _armorBlocks.SetCount(armor, animate);
                return;
            }

            _armorValueDigits.SetVisible(true);
            _armorValueDigits.SetValue(armor, animate);
            _armorBlocks.SetCount(1, animate);
        }

        private void AutoWireReferences()
        {
            attackAnchor ??= FindChild("Attack");
            lifeAnchor ??= FindChild("Life");
            armorBlocksAnchor ??= FindChild("ArmorBlocks");
            armorValueAnchor ??= FindChild("ArmorValue");
            cardFrameRenderer ??= FindRenderer("Card Frame");
            mainIconRenderer ??= FindRenderer("MainIcon");
            cardBackgroundRenderer ??= GetComponent<SpriteRenderer>();

#if UNITY_EDITOR
            if (spriteLibrary == null)
            {
                spriteLibrary = UnityEditor.AssetDatabase.LoadAssetAtPath<PixelCardPackSpriteLibrary>(
                    "Assets/Scripts/NineGrid.Presentation/Cards/PixelCardPackSpriteLibrary.asset");
            }
#endif

            if (armorBlocksAnchor == null)
            {
                armorBlocksAnchor = CreateAnchor("ArmorBlocks", new Vector3(-0.5388546f, -0.6682327f, 0f));
            }

            if (armorValueAnchor == null)
            {
                armorValueAnchor = CreateAnchor("ArmorValue", new Vector3(ArmorDigitSlotX[2], -0.7307327f, 0f));
            }
        }

        private void DisableLegacyStatRenderers()
        {
            DisableRendererOnAnchor(attackAnchor);
            DisableRendererOnAnchor(lifeAnchor);
        }

        private static void DisableRendererOnAnchor(Transform anchor)
        {
            if (anchor == null)
            {
                return;
            }

            var renderer = anchor.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }

        private float[] BuildArmorDigitSlotOffsets()
        {
            var onesX = armorValueAnchor != null ? armorValueAnchor.localPosition.x : ArmorDigitSlotX[2];
            var offsets = new float[ArmorDigitSlotCount];
            for (var i = 0; i < ArmorDigitSlotCount; i++)
            {
                var slotX = i < ArmorDigitSlotX.Length
                    ? ArmorDigitSlotX[i]
                    : onesX + (i - (ArmorDigitSlotCount - 1)) * armorDigitSpacing;
                offsets[i] = slotX - onesX;
            }

            return offsets;
        }

        private int ResolveSortingLayerId()
        {
            if (_sortingGroup != null)
            {
                return _sortingGroup.sortingLayerID;
            }

            if (mainIconRenderer != null)
            {
                return mainIconRenderer.sortingLayerID;
            }

            if (cardFrameRenderer != null)
            {
                return cardFrameRenderer.sortingLayerID;
            }

            var background = cardBackgroundRenderer ?? GetComponent<SpriteRenderer>();
            return background != null ? background.sortingLayerID : 0;
        }

        private void ApplySortingOrders()
        {
            if (cardFrameRenderer != null)
            {
                cardFrameRenderer.sortingOrder = frameSortingOrder;
            }

            if (cardBackgroundRenderer != null)
            {
                cardBackgroundRenderer.sortingOrder = backgroundSortingOrder;
            }

            if (mainIconRenderer != null)
            {
                mainIconRenderer.sortingOrder = mainIconSortingOrder;
            }
        }

        private Transform FindChild(string childName)
        {
            var child = transform.Find(childName);
            return child;
        }

        private SpriteRenderer FindRenderer(string childName)
        {
            var trimmed = childName.Trim();
            foreach (Transform child in transform)
            {
                if (child.name.Trim() == trimmed)
                {
                    return child.GetComponent<SpriteRenderer>();
                }
            }

            var namedChild = transform.Find(childName);
            return namedChild != null ? namedChild.GetComponent<SpriteRenderer>() : null;
        }

        private Transform CreateAnchor(string anchorName, Vector3 localPosition)
        {
            var anchor = new GameObject(anchorName).transform;
            anchor.SetParent(transform, false);
            anchor.localPosition = localPosition;
            return anchor;
        }

#if UNITY_EDITOR
        [ContextMenu("Capture Armor Block Layout From Children")]
        private void CaptureArmorBlockLayoutFromChildren()
        {
            var armorRoot = transform.Find("Armor");
            if (armorRoot == null)
            {
                return;
            }

            armorBlocksAnchor = armorRoot;
            var renderers = armorRoot.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers.Length > 0)
            {
                armorBlockScale = renderers[0].transform.localScale;
                if (renderers.Length > 1)
                {
                    armorBlockSpacing = Mathf.Abs(renderers[1].transform.localPosition.x - renderers[0].transform.localPosition.x);
                }
            }

            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
