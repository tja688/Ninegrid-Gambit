using System;
using System.Collections.Generic;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Flow.BattleInfoPreview;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Ui;
using QFramework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NineGrid.Flow.MainMenu
{
    /// <summary>
    /// 菜单背景 / MainBG 动态底：Tiled 精灵按一格 wrap 平移（不改 UV，图案本身不变），
    /// 主菜单再投放正式接线怪物 / 道具 / 场地卡主图标。
    /// 图标摆放复用战斗信息展示的 <see cref="BattleInfoPreviewIconPlayer"/>（卡面 mainVisual）。
    /// 挂在 <c>Panels/MainBG</c> 与 <c>MainPanel/菜单背景</c>；选中该物体即可在 Inspector 调参。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("NineGrid/Flow/Main Menu Backdrop Presenter")]
    public sealed class MainMenuBackdropPresenter : MonoBehaviour
    {
        public const string BackgroundObjectName = "MainBG";
        public const string MenuBackgroundObjectName = "菜单背景";
        public const string RainRootName = "MainMenuIconRain";
        public const string TutorialDeckId = "deck.tutorial";

        private const string ArtChildName = "__Art";
        private const string IconSortingLayer = "UI";
        // Keep decorative rain below the normal main-menu UI orders (-6/-3/-2).
        private const int IconSortingBase = -20;
        private const float MinScale = 0.0001f;
        // Extra tiled coverage so wrapping one tile never exposes an empty edge.
        private const float WrapMarginTiles = 2f;
        private const float ReferencePixelHeight = 540f;
        private const float FallbackPixelsPerUnit = 32f;
        private static readonly int PixelSnapId = Shader.PropertyToID("_PixelSnap");

        [Header("场景绑定")]
        [SerializeField] private SpriteRenderer backgroundRenderer;
        [Tooltip("投放相机；空则 Camera.main。")]
        [SerializeField] private Camera worldCamera;

        [Header("背景滑动")]
        [SerializeField] private bool scrollEnabled = true;
        [Tooltip("世界单位/秒；沿下方角度匀速平移整块 Tiled 图案，按一格 wrap。物体可见时一直滚。")]
        [SerializeField] private float scrollSpeed = 0.55f;
        [Tooltip("0=向右，45=右上，-45=右下。")]
        [SerializeField] private float scrollAngleDegrees = -45f;

        [Header("主图标投放")]
        [SerializeField] private bool rainEnabled = true;
        [Tooltip("叠在卡面 mainVisual.uniformScale 之上；1 = 标准卡面立绘大小。")]
        [SerializeField] private float iconExternalScale = 1f;
        [Tooltip("下落重力（世界单位/秒²）。水平速度恒定，竖直加速，轨迹为抛物线。")]
        [SerializeField] private float fallGravity = 2.2f;
        [Tooltip("入场竖直速度（向下为正）。无上抛。")]
        [SerializeField] private float initialFallSpeed = 1.15f;
        [Tooltip("水平速度随机幅度（世界单位/秒）。")]
        [SerializeField] private float horizontalSpeedRange = 2.4f;
        [Tooltip("下落时绕 Z 轴旋转速度随机范围（度/秒）；正负随机。")]
        [SerializeField] private float spinSpeedMin = -260f;
        [SerializeField] private float spinSpeedMax = 260f;
        [SerializeField] private float spawnIntervalMin = 0.42f;
        [SerializeField] private float spawnIntervalMax = 0.88f;
        [SerializeField] private int maxLiveIcons = 12;
        [Tooltip("进入主菜单时先在画面里铺几枚，避免空场。")]
        [SerializeField] private int prewarmCount = 4;
        [SerializeField] private float spawnMargin = 1.6f;
        [SerializeField] private float despawnMargin = 2.4f;

        private static MainMenuBackdropPresenter sInstance;

        private readonly List<string> _pool = new List<string>(128);
        private readonly List<MainMenuFallingIcon> _live = new List<MainMenuFallingIcon>(16);

        private Transform _rainRoot;
        private MaterialPropertyBlock _propertyBlock;
        private bool _raining;
        private bool _capturedAuthored;
        private Vector3 _authoredLocalPos;
        private Vector2 _authoredTiledSize;
        private bool _authoredWasTiled;
        private Vector2 _scrollLocal;
        private float _spawnCooldown;
        private string _lastDefId;
        private int _sortCursor;
        private bool _loggedEmptyPool;

        public static MainMenuBackdropPresenter EnsureExists()
        {
            var menu = EnsureOn(FindSceneObjectByName(MenuBackgroundObjectName));
            var main = EnsureOn(FindSceneObjectByName(BackgroundObjectName));
            sInstance = menu != null ? menu : main;
            if (sInstance == null)
            {
                sInstance = FindFirstObjectByType<MainMenuBackdropPresenter>(FindObjectsInactive.Include);
                if (sInstance != null)
                {
                    sInstance.EnsureBindings();
                }
            }

            return sInstance;
        }

        private static MainMenuBackdropPresenter EnsureOn(GameObject host)
        {
            if (host == null)
            {
                return null;
            }

            var presenter = host.GetComponent<MainMenuBackdropPresenter>();
            if (presenter == null)
            {
                presenter = host.AddComponent<MainMenuBackdropPresenter>();
            }

            presenter.EnsureBindings();
            return presenter;
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
            sInstance = this;
            EnsureBindings();
            CaptureAuthoredIfNeeded();
            PrepareRendererVisuals();
        }

        private void OnEnable()
        {
            EnsureBindings();
            CaptureAuthoredIfNeeded();
            PrepareRendererVisuals();
            EnsureWrapMargin();
            ApplyScrollTransform();
        }

        private void OnDestroy()
        {
            StopRain();
            RestoreAuthoredTransform();
            if (ReferenceEquals(sInstance, this))
            {
                sInstance = null;
            }
        }

        private void OnDisable()
        {
            StopRain();
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            if (dt > 0f && scrollEnabled)
            {
                TickScroll(dt);
            }

            var wantRain = rainEnabled && ShouldRunRain();
            if (wantRain && !_raining)
            {
                StartRain();
            }
            else if (!wantRain && _raining)
            {
                StopRain();
            }

            if (_raining && dt > 0f)
            {
                TickRain(dt);
            }
        }

        public void EnsureBindings()
        {
            if (backgroundRenderer == null)
            {
                backgroundRenderer = GetComponent<SpriteRenderer>();
            }

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }
        }

        private void StartRain()
        {
            EnsureBindings();
            _raining = true;
            _spawnCooldown = 0f;
            RebuildPool();
            EnsureRainRoot();
            PrewarmIcons();
        }

        private void StopRain()
        {
            _raining = false;
            ClearLiveIcons();
        }

        private void TickScroll(float dt)
        {
            if (backgroundRenderer == null || backgroundRenderer.sprite == null)
            {
                return;
            }

            CaptureAuthoredIfNeeded();
            var tileLocal = GetTileLocalSize();
            if (tileLocal.x < MinScale || tileLocal.y < MinScale)
            {
                return;
            }

            EnsureWrapMargin();
            var scale = backgroundRenderer.transform.lossyScale;
            var rad = scrollAngleDegrees * Mathf.Deg2Rad;
            var localDx = Mathf.Cos(rad) * scrollSpeed * dt / Mathf.Max(MinScale, Mathf.Abs(scale.x));
            var localDy = Mathf.Sin(rad) * scrollSpeed * dt / Mathf.Max(MinScale, Mathf.Abs(scale.y));
            _scrollLocal.x = RepeatPositive(_scrollLocal.x + localDx, tileLocal.x);
            _scrollLocal.y = RepeatPositive(_scrollLocal.y + localDy, tileLocal.y);
            ApplyScrollTransform();
        }

        private void CaptureAuthoredIfNeeded()
        {
            if (_capturedAuthored || backgroundRenderer == null)
            {
                return;
            }

            _authoredLocalPos = backgroundRenderer.transform.localPosition;
            _authoredWasTiled = backgroundRenderer.drawMode == SpriteDrawMode.Tiled;
            _authoredTiledSize = backgroundRenderer.size;
            _capturedAuthored = true;
        }

        private Vector2 GetTileLocalSize()
        {
            if (backgroundRenderer == null || backgroundRenderer.sprite == null)
            {
                return Vector2.zero;
            }

            var size = backgroundRenderer.sprite.bounds.size;
            return new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y));
        }

        private void EnsureWrapMargin()
        {
            if (backgroundRenderer == null || !_capturedAuthored || !_authoredWasTiled)
            {
                return;
            }

            var tile = GetTileLocalSize();
            if (tile.x < MinScale || tile.y < MinScale)
            {
                return;
            }

            var need = new Vector2(
                _authoredTiledSize.x + tile.x * WrapMarginTiles,
                _authoredTiledSize.y + tile.y * WrapMarginTiles);
            var current = backgroundRenderer.size;
            if (current.x + 0.001f < need.x || current.y + 0.001f < need.y)
            {
                backgroundRenderer.drawMode = SpriteDrawMode.Tiled;
                backgroundRenderer.size = need;
            }
        }

        private void ApplyScrollTransform()
        {
            if (backgroundRenderer == null || !_capturedAuthored)
            {
                return;
            }

            var t = backgroundRenderer.transform;
            var pos = _authoredLocalPos;
            pos.x += _scrollLocal.x;
            pos.y += _scrollLocal.y;

            var parent = t.parent;
            var world = parent != null ? parent.TransformPoint(pos) : pos;
            var pixel = GetWorldPixelSize();
            world.x = Mathf.Round(world.x / pixel) * pixel;
            world.y = Mathf.Round(world.y / pixel) * pixel;

            if (parent != null)
            {
                var local = parent.InverseTransformPoint(world);
                local.z = _authoredLocalPos.z;
                t.localPosition = local;
            }
            else
            {
                world.z = _authoredLocalPos.z;
                t.localPosition = world;
            }
        }

        private void RestoreAuthoredTransform()
        {
            if (!_capturedAuthored || backgroundRenderer == null)
            {
                return;
            }

            backgroundRenderer.transform.localPosition = _authoredLocalPos;
            if (_authoredWasTiled)
            {
                backgroundRenderer.drawMode = SpriteDrawMode.Tiled;
                backgroundRenderer.size = _authoredTiledSize;
            }

            backgroundRenderer.SetPropertyBlock(null);
        }

        private void PrepareRendererVisuals()
        {
            if (backgroundRenderer == null)
            {
                return;
            }

            backgroundRenderer.SetPropertyBlock(null);
            DisableSpritePixelSnap();
        }

        private void DisableSpritePixelSnap()
        {
            if (backgroundRenderer == null)
            {
                return;
            }

            _propertyBlock ??= new MaterialPropertyBlock();
            backgroundRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(PixelSnapId, 0f);
            backgroundRenderer.SetPropertyBlock(_propertyBlock);
        }

        private float GetWorldPixelSize()
        {
            var cam = worldCamera != null ? worldCamera : Camera.main;
            if (cam != null && cam.orthographic && cam.orthographicSize > MinScale)
            {
                var ppu = ReferencePixelHeight / (2f * cam.orthographicSize);
                if (ppu > MinScale)
                {
                    return 1f / ppu;
                }
            }

            return 1f / FallbackPixelsPerUnit;
        }

        private void TickRain(float dt)
        {
            if (!rainEnabled || _pool.Count == 0)
            {
                return;
            }

            _spawnCooldown -= dt;
            if (_live.Count >= Mathf.Max(1, maxLiveIcons) || _spawnCooldown > 0f)
            {
                return;
            }

            if (TrySpawnIcon(prewarmAlongFall: false))
            {
                _spawnCooldown = UnityEngine.Random.Range(
                    Mathf.Max(0.05f, spawnIntervalMin),
                    Mathf.Max(spawnIntervalMin, spawnIntervalMax));
            }
            else
            {
                _spawnCooldown = 0.25f;
            }
        }

        private void PrewarmIcons()
        {
            var count = Mathf.Clamp(prewarmCount, 0, Mathf.Max(1, maxLiveIcons));
            for (var i = 0; i < count; i++)
            {
                TrySpawnIcon(prewarmAlongFall: true);
            }
        }

        private bool TrySpawnIcon(bool prewarmAlongFall)
        {
            if (_pool.Count == 0)
            {
                return false;
            }

            EnsureRainRoot();
            if (_rainRoot == null)
            {
                return false;
            }

            var cam = worldCamera != null ? worldCamera : Camera.main;
            if (cam == null)
            {
                return false;
            }

            var defId = PickDefId();
            if (string.IsNullOrEmpty(defId))
            {
                return false;
            }

            if (!TryGetViewportBounds(cam, out var min, out var max))
            {
                return false;
            }

            var go = new GameObject("FallingIcon");
            go.transform.SetParent(_rainRoot, false);
            var spawnX = UnityEngine.Random.Range(min.x - spawnMargin * 0.35f, max.x + spawnMargin * 0.35f);
            var spawnY = max.y + spawnMargin;
            if (prewarmAlongFall)
            {
                spawnY = UnityEngine.Random.Range(min.y + 0.5f, max.y + spawnMargin * 0.5f);
            }

            go.transform.position = new Vector3(spawnX, spawnY, 0f);

            var artGo = new GameObject(ArtChildName);
            artGo.transform.SetParent(go.transform, false);
            var art = artGo.AddComponent<SpriteRenderer>();
            art.sortingLayerID = SortingLayer.NameToID(IconSortingLayer);
            art.sortingOrder = IconSortingBase + _sortCursor;
            _sortCursor = (_sortCursor + 1) % 8;
            if (backgroundRenderer != null && backgroundRenderer.sharedMaterial != null)
            {
                art.sharedMaterial = backgroundRenderer.sharedMaterial;
            }

            var player = go.AddComponent<BattleInfoPreviewIconPlayer>();
            player.BindTarget(art, null);
            player.PlayIdleOrStatic(defId, Mathf.Max(MinScale, iconExternalScale));
            if (art.sprite == null)
            {
                Destroy(go);
                return false;
            }

            var vx = UnityEngine.Random.Range(-Mathf.Abs(horizontalSpeedRange), Mathf.Abs(horizontalSpeedRange));
            var vy = -Mathf.Abs(initialFallSpeed);
            var spinMin = Mathf.Min(spinSpeedMin, spinSpeedMax);
            var spinMax = Mathf.Max(spinSpeedMin, spinSpeedMax);
            var spin = spinMin == spinMax
                ? spinMin
                : UnityEngine.Random.Range(spinMin, spinMax);
            var falling = go.AddComponent<MainMenuFallingIcon>();
            falling.Launch(
                new Vector3(vx, vy, 0f),
                spin,
                Mathf.Max(0.01f, fallGravity),
                min.y - despawnMargin,
                Mathf.Max(Mathf.Abs(min.x), Mathf.Abs(max.x)) + despawnMargin + 4f,
                OnIconDespawned);
            _live.Add(falling);
            _lastDefId = defId;
            return true;
        }

        private void OnIconDespawned(MainMenuFallingIcon icon)
        {
            _live.Remove(icon);
        }

        private void ClearLiveIcons()
        {
            for (var i = _live.Count - 1; i >= 0; i--)
            {
                var icon = _live[i];
                if (icon != null)
                {
                    Destroy(icon.gameObject);
                }
            }

            _live.Clear();
            if (_rainRoot != null)
            {
                Destroy(_rainRoot.gameObject);
                _rainRoot = null;
            }
        }

        private void EnsureRainRoot()
        {
            if (_rainRoot != null)
            {
                return;
            }

            var parent = backgroundRenderer != null
                ? backgroundRenderer.transform.parent
                : transform.parent;
            var go = new GameObject(RainRootName);
            _rainRoot = go.transform;
            if (parent != null)
            {
                _rainRoot.SetParent(parent, false);
                var sibling = backgroundRenderer != null
                    ? backgroundRenderer.transform.GetSiblingIndex() + 1
                    : transform.GetSiblingIndex() + 1;
                _rainRoot.SetSiblingIndex(sibling);
            }

            _rainRoot.localPosition = Vector3.zero;
            _rainRoot.localRotation = Quaternion.identity;
            _rainRoot.localScale = Vector3.one;
        }

        private void RebuildPool()
        {
            _pool.Clear();
            foreach (var contentId in CardPresentationConfigCatalog.AllContentIds)
            {
                if (!CardPresentationConfigCatalog.TryGet(contentId, out var dto) || dto == null)
                {
                    continue;
                }

                if (!IsFormalDropKind(dto.kind))
                {
                    continue;
                }

                if (dto.isReserve)
                {
                    continue;
                }

                var deckId = dto.deckId != null ? dto.deckId.Trim() : string.Empty;
                if (FormalContentWiring.IsUnofficialDeck(deckId)
                    || string.Equals(deckId, TutorialDeckId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!HasMainVisual(dto))
                {
                    continue;
                }

                _pool.Add(dto.contentId);
            }

            if (_pool.Count == 0 && !_loggedEmptyPool)
            {
                _loggedEmptyPool = true;
                Debug.LogWarning("[MainMenuBackdrop] 正式接线主图标池为空，跳过投放。");
            }
        }

        private string PickDefId()
        {
            if (_pool.Count == 0)
            {
                return null;
            }

            if (_pool.Count == 1)
            {
                return _pool[0];
            }

            for (var attempt = 0; attempt < 6; attempt++)
            {
                var pick = _pool[UnityEngine.Random.Range(0, _pool.Count)];
                if (!string.Equals(pick, _lastDefId, StringComparison.Ordinal))
                {
                    return pick;
                }
            }

            return _pool[UnityEngine.Random.Range(0, _pool.Count)];
        }

        private static bool IsFormalDropKind(string kind)
        {
            if (string.IsNullOrWhiteSpace(kind))
            {
                return false;
            }

            return string.Equals(kind, "Monster", StringComparison.OrdinalIgnoreCase)
                || string.Equals(kind, "HelpCard", StringComparison.OrdinalIgnoreCase)
                || string.Equals(kind, "Item", StringComparison.OrdinalIgnoreCase)
                || string.Equals(kind, "Trap", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasMainVisual(CardPresentationConfigDto dto)
        {
            if (dto.sprites != null && !string.IsNullOrWhiteSpace(dto.sprites.mainIcon))
            {
                return true;
            }

            var slots = dto.animations != null ? dto.animations.slots : null;
            if (slots == null)
            {
                return false;
            }

            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null || string.IsNullOrWhiteSpace(slot.path))
                {
                    continue;
                }

                if (string.Equals(slot.sourceType, "folder", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(slot.sourceType, "atlas", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetViewportBounds(Camera cam, out Vector2 min, out Vector2 max)
        {
            min = default;
            max = default;
            if (cam == null)
            {
                return false;
            }

            var depth = Mathf.Abs(cam.transform.position.z);
            var bl = cam.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
            var tr = cam.ViewportToWorldPoint(new Vector3(1f, 1f, depth));
            min = new Vector2(Mathf.Min(bl.x, tr.x), Mathf.Min(bl.y, tr.y));
            max = new Vector2(Mathf.Max(bl.x, tr.x), Mathf.Max(bl.y, tr.y));
            return max.x - min.x > MinScale && max.y - min.y > MinScale;
        }

        private static bool ShouldRunRain()
        {
            if (!IsMainMenuShellState())
            {
                return false;
            }

            // 选人 / 加载存档 / 设置等叠层仍处 MainMenu 相位，图标雨暂停；纹理滚动不受此门禁。
            if (CharacterSelectPanel.IsOpen
                || MainMenuLoadPanel.IsOpen
                || PlayerAudioSettingsPanel.IsOpen
                || PureBlackScreenOverlay.IsActive)
            {
                return false;
            }

            return true;
        }

        private static bool IsMainMenuShellState()
        {
            var arch = NineGridArchitecture.Current ?? NineGridArchitecture.Interface;
            var shell = arch != null ? arch.GetSystem<IGameFlowShellSystem>() : null;
            if (shell == null)
            {
                return true;
            }

            return shell.State.Value == GameFlowShellState.MainMenu;
        }

        private static GameObject FindSceneObjectByName(string objectName)
        {
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var found = FindDeep(roots[i].transform, objectName);
                if (found != null)
                {
                    return found.gameObject;
                }
            }

            return null;
        }

        private static Transform FindDeep(Transform root, string objectName)
        {
            if (root == null)
            {
                return null;
            }

            if (string.Equals(root.name, objectName, StringComparison.Ordinal))
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static float RepeatPositive(float value, float length)
        {
            if (length < MinScale)
            {
                return 0f;
            }

            var wrapped = value % length;
            if (wrapped < 0f)
            {
                wrapped += length;
            }

            return wrapped;
        }
    }

    /// <summary>主菜单投放图标：水平匀速 + 竖直加速，仅下落抛物线。</summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuFallingIcon : MonoBehaviour
    {
        private Vector3 _velocity;
        private float _angularVelocity;
        private float _gravity;
        private float _killY;
        private float _killAbsX;
        private Action<MainMenuFallingIcon> _onDespawn;
        private bool _launched;

        public void Launch(
            Vector3 velocity,
            float angularVelocityDegrees,
            float gravity,
            float killY,
            float killAbsX,
            Action<MainMenuFallingIcon> onDespawn)
        {
            _velocity = velocity;
            if (_velocity.y > 0f)
            {
                _velocity.y = 0f;
            }

            _angularVelocity = angularVelocityDegrees;
            _gravity = gravity;
            _killY = killY;
            _killAbsX = killAbsX;
            _onDespawn = onDespawn;
            _launched = true;
        }

        private void Update()
        {
            if (!_launched)
            {
                return;
            }

            var dt = Time.unscaledDeltaTime;
            _velocity.y -= _gravity * dt;
            transform.position += _velocity * dt;
            if (Mathf.Abs(_angularVelocity) > 0.0001f)
            {
                transform.Rotate(0f, 0f, _angularVelocity * dt, Space.Self);
            }

            var pos = transform.position;
            if (pos.y <= _killY || Mathf.Abs(pos.x) >= _killAbsX)
            {
                Despawn();
            }
        }

        private void OnDestroy()
        {
            _onDespawn?.Invoke(this);
            _onDespawn = null;
        }

        private void Despawn()
        {
            _launched = false;
            var callback = _onDespawn;
            _onDespawn = null;
            callback?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
