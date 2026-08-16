using System;
using System.Collections.Generic;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NineGrid.Flow.BoardBriefTip
{
    /// <summary>
    /// 场地框 <c>GroundPanel </c>、<c>MainBG</c> 与 <c>GroundAnchors</c> 格面随地下城环境切换（ADR-0053 扩展）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VenueEnvironmentPresenter : MonoBehaviour
    {
        public const string GroundPanelObjectName = "GroundPanel ";
        public const string MainBackgroundObjectName = "MainBG";
        public const string GroundAnchorsObjectName = "GroundAnchors";

        private static VenueEnvironmentPresenter sInstance;

        [SerializeField] private SpriteRenderer groundPanelRenderer;
        [SerializeField] private SpriteRenderer mainBackgroundRenderer;
        [SerializeField] private SpriteRenderer[] slotRenderers;

        private string mLastGroundPanelPath = null;
        private string mLastMainBackgroundHex = null;
        private string mLastSlotHex = null;
        private readonly Dictionary<string, Sprite> mSpriteCache = new Dictionary<string, Sprite>(StringComparer.Ordinal);

        public static VenueEnvironmentPresenter EnsureExists()
        {
            var found = FindFirstObjectByType<VenueEnvironmentPresenter>(FindObjectsInactive.Include);
            if (found != null)
            {
                found.EnsureBindings();
                AdoptInstance(found);
                return found;
            }

            if (sInstance != null)
            {
                sInstance.EnsureBindings();
                return sInstance;
            }

            var host = FindGroundPanelObject();
            if (host != null)
            {
                sInstance = host.GetComponent<VenueEnvironmentPresenter>();
                if (sInstance == null)
                {
                    sInstance = host.AddComponent<VenueEnvironmentPresenter>();
                }

                sInstance.EnsureBindings();
                return sInstance;
            }

            var go = new GameObject(nameof(VenueEnvironmentPresenter));
            sInstance = go.AddComponent<VenueEnvironmentPresenter>();
            return sInstance;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EnsureExists();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureExists();
        }

        private void Awake()
        {
            EnsureBindings();
            if (groundPanelRenderer != null && groundPanelRenderer.gameObject.name == GroundPanelObjectName)
            {
                AdoptInstance(this);
            }
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(sInstance, this))
            {
                sInstance = null;
            }
        }

        private void LateUpdate()
        {
            var arch = NineGridArchitecture.Current;
            if (arch == null)
            {
                return;
            }

            var environment = arch.SendQuery(new GetCurrentDungeonEnvironmentQuery());
            Apply(environment);
        }

        public void Apply(DungeonEnvironmentInfo environment)
        {
            var self = EnsureExists();
            if (!ReferenceEquals(self, this))
            {
                self.Apply(environment);
                return;
            }

            EnsureBindings();
            ApplyGroundPanel(environment.GroundPanelResourcePath);
            ApplyMainBackground(environment.MainBackgroundColorHex);
            ApplySlotTint(environment.SlotColorHex);
        }

        public void EnsureBindings()
        {
            if (groundPanelRenderer == null)
            {
                var groundPanel = FindGroundPanelObject();
                if (groundPanel != null)
                {
                    groundPanelRenderer = groundPanel.GetComponent<SpriteRenderer>();
                }
            }

            if (mainBackgroundRenderer == null)
            {
                var mainBg = FindSceneObjectByName(MainBackgroundObjectName);
                if (mainBg != null)
                {
                    mainBackgroundRenderer = mainBg.GetComponent<SpriteRenderer>();
                }
            }

            if (slotRenderers == null || slotRenderers.Length == 0 || slotRenderers[0] == null)
            {
                slotRenderers = CollectSlotRenderers();
            }
        }

        private void ApplyGroundPanel(string resourcePath)
        {
            if (groundPanelRenderer == null || string.IsNullOrWhiteSpace(resourcePath))
            {
                return;
            }

            if (string.Equals(mLastGroundPanelPath, resourcePath, StringComparison.Ordinal))
            {
                return;
            }

            var sprite = LoadGroundPanelSprite(resourcePath);
            if (sprite == null)
            {
                return;
            }

            groundPanelRenderer.sprite = sprite;
            mLastGroundPanelPath = resourcePath;
        }

        private void ApplyMainBackground(string colorHex)
        {
            if (mainBackgroundRenderer == null || string.IsNullOrWhiteSpace(colorHex))
            {
                return;
            }

            if (string.Equals(mLastMainBackgroundHex, colorHex, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!TryParseHexColor(colorHex, out var color))
            {
                return;
            }

            mainBackgroundRenderer.color = color;
            mLastMainBackgroundHex = colorHex;
        }

        private void ApplySlotTint(string colorHex)
        {
            if (string.IsNullOrWhiteSpace(colorHex))
            {
                return;
            }

            if (string.Equals(mLastSlotHex, colorHex, StringComparison.OrdinalIgnoreCase)
                && slotRenderers != null
                && slotRenderers.Length > 0)
            {
                return;
            }

            if (!TryParseHexColor(colorHex, out var color))
            {
                return;
            }

            if (slotRenderers == null || slotRenderers.Length == 0 || slotRenderers[0] == null)
            {
                slotRenderers = CollectSlotRenderers();
            }

            if (slotRenderers == null || slotRenderers.Length == 0)
            {
                return;
            }

            for (var i = 0; i < slotRenderers.Length; i++)
            {
                var renderer = slotRenderers[i];
                if (renderer != null)
                {
                    renderer.color = color;
                }
            }

            mLastSlotHex = colorHex;
        }

        private static SpriteRenderer[] CollectSlotRenderers()
        {
            var root = FindSceneObjectByName(GroundAnchorsObjectName);
            if (root == null)
            {
                return Array.Empty<SpriteRenderer>();
            }

            var childCount = root.transform.childCount;
            var list = new List<SpriteRenderer>(childCount);
            for (var i = 0; i < childCount; i++)
            {
                var renderer = root.transform.GetChild(i).GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    list.Add(renderer);
                }
            }

            return list.ToArray();
        }

        private Sprite LoadGroundPanelSprite(string resourcePath)
        {
            if (mSpriteCache.TryGetValue(resourcePath, out var cached) && cached != null)
            {
                return cached;
            }

            var sprite = CardPresentationSpritePath.LoadSprite(resourcePath);
            if (sprite != null)
            {
                mSpriteCache[resourcePath] = sprite;
            }

            return sprite;
        }

        private static bool TryParseHexColor(string hex, out Color color)
        {
            color = Color.white;
            if (string.IsNullOrWhiteSpace(hex))
            {
                return false;
            }

            var normalized = hex.Trim();
            if (normalized.StartsWith("#", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(1);
            }

            if (normalized.Length != 6)
            {
                return false;
            }

            if (!byte.TryParse(normalized.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out var r)
                || !byte.TryParse(normalized.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var g)
                || !byte.TryParse(normalized.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
            {
                return false;
            }

            color = new Color32(r, g, b, 255);
            return true;
        }

        private static GameObject FindGroundPanelObject()
        {
            var byName = FindSceneObjectByName(GroundPanelObjectName);
            if (byName != null)
            {
                return byName;
            }

            return FindSceneObjectByName("GroundPanel");
        }

        private static GameObject FindSceneObjectByName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return null;
            }

            var all = Resources.FindObjectsOfTypeAll<Transform>();
            for (var i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null || t.name != objectName)
                {
                    continue;
                }

                if (!t.gameObject.scene.IsValid())
                {
                    continue;
                }

                return t.gameObject;
            }

            return GameObject.Find(objectName);
        }

        private static void AdoptInstance(VenueEnvironmentPresenter presenter)
        {
            if (presenter == null)
            {
                return;
            }

            if (sInstance != null
                && !ReferenceEquals(sInstance, presenter)
                && sInstance.gameObject != null
                && sInstance.gameObject.name == nameof(VenueEnvironmentPresenter))
            {
                var orphan = sInstance.gameObject;
                sInstance = presenter;
                Destroy(orphan);
                return;
            }

            sInstance = presenter;
        }
    }
}
