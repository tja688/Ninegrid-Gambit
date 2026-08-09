using System;
using NineGrid.Content.Audio;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using UnityEngine;

namespace NineGrid.Presentation.Ui
{
    /// <summary>
    /// 局内功能菜单（含音量模块）场景接线：主菜单「菜单按钮」打开，半黑屏 / 关闭钮 / Escape 关闭。
    /// 仅绑定 <see cref="IPlayerAudioSettingsSystem"/>，不生成运行时 ugui。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class PlayerAudioSettingsPanel : MonoBehaviour
    {
        public const string PanelRootName = "局内功能菜单BG";
        public const string MenuButtonName = "菜单按钮";
        public const string VolumeModuleName = "音量模块";
        public const string CloseButtonPath = "功能模块/关闭面板";
        private const string DimmerReason = "in-run-function-menu";
        private const float SliderHandleHalfPad = 0.28f;

        private static PlayerAudioSettingsPanel sInstance;

        private IPlayerAudioSettingsSystem mSettings;
        private GameObject mPanelRoot;
        private GameObject mMenuButton;
        private BoxCollider2D mMenuButtonCollider;
        private Transform mMasterMuteOn;
        private Transform mBgmMuteOff;
        private VolumeSliderBinder mBgmSlider;
        private VolumeSliderBinder mSfxSlider;
        private bool mDimmerHeld;
        private bool mBound;
        private VolumeSliderBinder mDragging;

        public static bool IsOpen =>
            sInstance != null
            && sInstance.mPanelRoot != null
            && sInstance.mPanelRoot.activeSelf;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindSceneInstance() != null)
            {
                return;
            }

            var panel = FindSceneNamed(PanelRootName);
            if (panel == null)
            {
                return;
            }

            if (panel.GetComponent<PlayerAudioSettingsPanel>() == null)
            {
                panel.AddComponent<PlayerAudioSettingsPanel>();
            }
        }

        public static void CloseIfOpen()
        {
            if (sInstance != null)
            {
                sInstance.SetOpen(false);
            }
        }

        public static void RequestOpen()
        {
            var live = FindSceneInstance() ?? EnsureFromScene();
            live?.SetOpen(true);
        }

        private void Awake()
        {
            sInstance = this;
            mPanelRoot = gameObject;
            if (mPanelRoot.activeSelf)
            {
                mPanelRoot.SetActive(false);
            }

            if (Application.isPlaying)
            {
                EnsureBound();
            }
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                EnsureBound();
            }
        }

        private void OnDestroy()
        {
            if (mSettings != null)
            {
                mSettings.Changed -= OnSettingsChanged;
            }

            if (mDimmerHeld)
            {
                BattleUiDimmerOverlay.Release(DimmerReason);
                mDimmerHeld = false;
            }

            if (sInstance == this)
            {
                sInstance = null;
            }
        }

        private void Update()
        {
            if (mDragging != null)
            {
                if (!WorldPointerUtility.IsPrimaryHeld())
                {
                    mDragging.EndGesture();
                    mDragging = null;
                }
                else
                {
                    mDragging.ApplyFromPointer();
                }
            }

            if (!IsOpen)
            {
                return;
            }

            if (KeyboardUtility.GetKeyDown(KeyCode.Escape))
            {
                TriggerPulseHub.PulseAudio(AudioCueRequest.Simple(
                    InteractionAudioCues.PlayerAudioEscape,
                    "PlayerAudioSettingsPanel.Update.Escape"));
                SetOpen(false);
            }
        }

        private void EnsureBound()
        {
            if (mBound)
            {
                return;
            }

            mSettings = PlayerAudioSettingsSystem.EnsureRegistered();
            mSettings.Changed += OnSettingsChanged;

            mMenuButton = FindSceneNamed(MenuButtonName);
            WireMenuButton(mMenuButton);

            var uiRoot = FindSceneNamed("UI面板");
            BattleUiDimmerOverlay.EnsureBound(
                uiRoot != null ? uiRoot.transform.Find("半黑屏BG")?.gameObject : null);

            WirePanelSwallow(mPanelRoot);
            WireCloseButton(mPanelRoot.transform.Find(CloseButtonPath));

            var volume = mPanelRoot.transform.Find(VolumeModuleName);
            if (volume != null)
            {
                mMasterMuteOn = volume.Find("总音量关/总音量开");
                mBgmMuteOff = volume.Find("音乐开/音乐关");
                WireMuteToggle(volume.Find("总音量关"), PlayerAudioBus.Master, "player_audio.master.mute");
                WireMuteToggle(volume.Find("音乐开"), PlayerAudioBus.Bgm, "player_audio.bgm.mute");
                mBgmSlider = WireSlider(volume.Find("bgm音量滑条"), PlayerAudioBus.Bgm, "player_audio.bgm.volume");
                mSfxSlider = WireSlider(volume.Find("SFX音量滑条 (1)"), PlayerAudioBus.Sfx, "player_audio.sfx.volume");
            }

            RefreshVisuals(mSettings.Current);
            mBound = true;
        }

        private void SetOpen(bool open)
        {
            EnsureBound();
            if (mPanelRoot == null)
            {
                return;
            }

            if (open == mPanelRoot.activeSelf)
            {
                return;
            }

            if (open)
            {
                if (!BattleUiDimmerOverlay.TryAcquire(DimmerReason))
                {
                    Debug.LogWarning("[PlayerAudioSettings] 半黑屏 Acquire 失败，仍打开功能菜单。");
                }
                else
                {
                    mDimmerHeld = true;
                }

                mPanelRoot.SetActive(true);
                RefreshVisuals(mSettings.Current);
                InteractionAudioCues.Pulse(
                    InteractionAudioCues.UiConfirm,
                    "PlayerAudioSettingsPanel.SetOpen",
                    "player_audio.open");
            }
            else
            {
                mDragging = null;
                mPanelRoot.SetActive(false);
                if (mDimmerHeld)
                {
                    BattleUiDimmerOverlay.Release(DimmerReason);
                    mDimmerHeld = false;
                }

                InteractionAudioCues.Pulse(
                    InteractionAudioCues.UiCancel,
                    "PlayerAudioSettingsPanel.SetOpen",
                    "player_audio.close");
            }

            if (mMenuButtonCollider != null)
            {
                mMenuButtonCollider.enabled = !open;
            }
        }

        private void OnSettingsChanged(PlayerAudioSettingsSnapshot snapshot)
        {
            RefreshVisuals(snapshot);
        }

        private void RefreshVisuals(PlayerAudioSettingsSnapshot snapshot)
        {
            if (mMasterMuteOn != null)
            {
                mMasterMuteOn.gameObject.SetActive(!snapshot.MasterMuted);
            }

            if (mBgmMuteOff != null)
            {
                mBgmMuteOff.gameObject.SetActive(snapshot.BgmMuted);
            }

            mBgmSlider?.SetNormalized(snapshot.BgmVolume);
            mSfxSlider?.SetNormalized(snapshot.SfxVolume);
        }

        private void WireMenuButton(GameObject button)
        {
            if (button == null)
            {
                return;
            }

            mMenuButtonCollider = EnsureCollider(button, new Vector2(0.5f, 0.5f));
            var hit = button.GetComponent<FunctionMenuHitProxy>();
            if (hit == null)
            {
                hit = button.AddComponent<FunctionMenuHitProxy>();
            }

            hit.Configure(
                () => SetOpen(true),
                BattleUiDimmerOverlay.CloseHitSort + 1,
                PointerHitSurfacePriorities.Overlay);
        }

        private void WireCloseButton(Transform close)
        {
            if (close == null)
            {
                return;
            }

            EnsureCollider(close.gameObject, new Vector2(0.5f, 0.5f));
            var proxy = close.GetComponent<UiOverlayHitProxy>();
            if (proxy == null)
            {
                proxy = close.gameObject.AddComponent<UiOverlayHitProxy>();
            }

            proxy.Configure(
                UiOverlayHitAction.CloseInRunFunctionMenu,
                BattleUiDimmerOverlay.CloseHitSort,
                PointerHitSurfacePriorities.Overlay);
        }

        private static void WirePanelSwallow(GameObject panel)
        {
            if (panel == null)
            {
                return;
            }

            var col = panel.GetComponent<BoxCollider2D>();
            if (col == null)
            {
                col = panel.AddComponent<BoxCollider2D>();
            }

            // 面板 BG 精灵偏小；按内容包围盒铺 Swallow，避免点在菜单空白处穿透半黑屏误关。
            var bounds = ComputeLocalContentBounds(panel.transform);
            col.size = new Vector2(Mathf.Max(3f, bounds.size.x), Mathf.Max(3f, bounds.size.y));
            col.offset = bounds.center;
            col.isTrigger = false;
            col.enabled = true;

            var proxy = panel.GetComponent<UiOverlayHitProxy>();
            if (proxy == null)
            {
                proxy = panel.AddComponent<UiOverlayHitProxy>();
            }

            proxy.Configure(
                UiOverlayHitAction.Swallow,
                BattleUiDimmerOverlay.HitSort + 1,
                PointerHitSurfacePriorities.Overlay);
        }

        private void WireMuteToggle(Transform root, PlayerAudioBus bus, string contentId)
        {
            if (root == null)
            {
                return;
            }

            EnsureCollider(root.gameObject, PreferSpriteSize(root));
            var child = root.childCount > 0 ? root.GetChild(0) : null;
            if (child != null)
            {
                EnsureCollider(child.gameObject, PreferSpriteSize(child));
            }

            void Toggle()
            {
                if (mSettings == null)
                {
                    return;
                }

                InteractionAudioCues.Pulse(
                    InteractionAudioCues.UiConfirm,
                    "PlayerAudioSettingsPanel.MuteToggle",
                    contentId);
                mSettings.SetMuted(bus, !mSettings.Current.IsMuted(bus));
            }

            BindClick(root.gameObject, Toggle, BattleUiDimmerOverlay.CloseHitSort);
            if (child != null)
            {
                BindClick(child.gameObject, Toggle, BattleUiDimmerOverlay.CloseHitSort + 1);
            }
        }

        private VolumeSliderBinder WireSlider(Transform track, PlayerAudioBus bus, string contentId)
        {
            if (track == null)
            {
                return null;
            }

            var handle = track.Find("音量滑柄");
            if (handle == null)
            {
                return null;
            }

            var trackSr = track.GetComponent<SpriteRenderer>();
            var trackHalf = trackSr != null && trackSr.sprite != null
                ? trackSr.sprite.bounds.extents.x
                : 1.3f;
            var minX = -(trackHalf - SliderHandleHalfPad);
            var maxX = trackHalf - SliderHandleHalfPad;

            EnsureCollider(track.gameObject, PreferSpriteSize(track));
            EnsureCollider(handle.gameObject, PreferSpriteSize(handle));

            var binder = track.GetComponent<VolumeSliderBinder>();
            if (binder == null)
            {
                binder = track.gameObject.AddComponent<VolumeSliderBinder>();
            }

            binder.Configure(
                mSettings,
                bus,
                handle,
                minX,
                maxX,
                contentId);

            BindClick(track.gameObject, () =>
            {
                binder.ApplyFromPointer();
                mDragging = binder;
            }, BattleUiDimmerOverlay.CloseHitSort);
            BindClick(handle.gameObject, () =>
            {
                binder.ApplyFromPointer();
                mDragging = binder;
            }, BattleUiDimmerOverlay.CloseHitSort + 1);
            return binder;
        }

        private static void BindClick(GameObject go, Action onClick, int hitSort)
        {
            var hit = go.GetComponent<FunctionMenuHitProxy>();
            if (hit == null)
            {
                hit = go.AddComponent<FunctionMenuHitProxy>();
            }

            hit.Configure(onClick, hitSort, PointerHitSurfacePriorities.Overlay);
        }

        private static BoxCollider2D EnsureCollider(GameObject go, Vector2 size)
        {
            var col = go.GetComponent<BoxCollider2D>();
            if (col == null)
            {
                col = go.AddComponent<BoxCollider2D>();
            }

            col.size = size;
            col.offset = Vector2.zero;
            col.isTrigger = false;
            col.enabled = true;
            return col;
        }

        private static Vector2 PreferSpriteSize(Transform t)
        {
            var sr = t.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                var size = sr.sprite.bounds.size;
                return new Vector2(Mathf.Max(0.25f, size.x), Mathf.Max(0.25f, size.y));
            }

            return new Vector2(0.5f, 0.5f);
        }

        private static Bounds ComputeLocalContentBounds(Transform root)
        {
            var has = false;
            var min = Vector3.zero;
            var max = Vector3.zero;
            void Acc(Transform t)
            {
                var sr = t.GetComponent<SpriteRenderer>();
                if (sr != null && sr.sprite != null)
                {
                    var world = sr.bounds;
                    var localMin = root.InverseTransformPoint(world.min);
                    var localMax = root.InverseTransformPoint(world.max);
                    var a = Vector3.Min(localMin, localMax);
                    var b = Vector3.Max(localMin, localMax);
                    if (!has)
                    {
                        min = a;
                        max = b;
                        has = true;
                    }
                    else
                    {
                        min = Vector3.Min(min, a);
                        max = Vector3.Max(max, b);
                    }
                }

                for (var i = 0; i < t.childCount; i++)
                {
                    Acc(t.GetChild(i));
                }
            }

            Acc(root);
            if (!has)
            {
                return new Bounds(Vector3.zero, new Vector3(8f, 10f, 0.1f));
            }

            var center = (min + max) * 0.5f;
            var size = max - min;
            return new Bounds(center, size);
        }

        private static PlayerAudioSettingsPanel FindSceneInstance()
        {
            var all = Resources.FindObjectsOfTypeAll<PlayerAudioSettingsPanel>();
            for (var i = 0; i < all.Length; i++)
            {
                var panel = all[i];
                if (panel != null && IsSceneObject(panel.gameObject))
                {
                    return panel;
                }
            }

            return null;
        }

        private static PlayerAudioSettingsPanel EnsureFromScene()
        {
            var root = FindSceneNamed(PanelRootName);
            if (root == null)
            {
                return null;
            }

            var panel = root.GetComponent<PlayerAudioSettingsPanel>();
            return panel != null ? panel : root.AddComponent<PlayerAudioSettingsPanel>();
        }

        private static GameObject FindSceneNamed(string name)
        {
            var all = Resources.FindObjectsOfTypeAll<Transform>();
            for (var i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null || t.name != name || !IsSceneObject(t.gameObject))
                {
                    continue;
                }

                return t.gameObject;
            }

            return null;
        }

        private static bool IsSceneObject(GameObject go)
        {
            return go != null && go.scene.IsValid() && go.scene.isLoaded;
        }

        /// <summary>局内功能菜单点击代理（生产路径；非作弊专用）。</summary>
        [DisallowMultipleComponent]
        [RequireComponent(typeof(BoxCollider2D))]
        private sealed class FunctionMenuHitProxy : MonoBehaviour, IPointerHitTarget
        {
            private BoxCollider2D mCollider;
            private Action mOnClick;
            private int mHitSort = BattleUiDimmerOverlay.CloseHitSort;
            private int mTypePriority = PointerHitSurfacePriorities.Overlay;

            public Collider2D HitCollider =>
                mCollider != null ? mCollider : (mCollider = GetComponent<BoxCollider2D>());

            public int HitSortOrder => mHitSort;
            public int HitTypePriority => mTypePriority;

            public void Configure(Action onClick, int hitSort, int typePriority)
            {
                mOnClick = onClick;
                mHitSort = hitSort;
                mTypePriority = typePriority;
            }

            private void Awake()
            {
                mCollider = GetComponent<BoxCollider2D>();
                if (mCollider != null)
                {
                    mCollider.isTrigger = false;
                }
            }

            private void OnEnable() => PointerHitRegistry.Register(this);

            private void OnDisable() => PointerHitRegistry.Unregister(this);

            public void HandlePointerEnter()
            {
            }

            public void HandlePointerExit()
            {
            }

            public void HandlePointerDown() => mOnClick?.Invoke();
        }

        [DisallowMultipleComponent]
        private sealed class VolumeSliderBinder : MonoBehaviour
        {
            private IPlayerAudioSettingsSystem mSettings;
            private PlayerAudioBus mBus;
            private Transform mHandle;
            private float mMinX;
            private float mMaxX;
            private string mContentId;
            private bool mQuiet;

            public void Configure(
                IPlayerAudioSettingsSystem settings,
                PlayerAudioBus bus,
                Transform handle,
                float minX,
                float maxX,
                string contentId)
            {
                mSettings = settings;
                mBus = bus;
                mHandle = handle;
                mMinX = minX;
                mMaxX = maxX;
                mContentId = contentId;
            }

            public void SetNormalized(float value)
            {
                if (mHandle == null)
                {
                    return;
                }

                var t = Mathf.Clamp01(value);
                var x = Mathf.Lerp(mMinX, mMaxX, t);
                var local = mHandle.localPosition;
                local.x = x;
                mHandle.localPosition = local;
            }

            public void ApplyFromPointer()
            {
                if (mSettings == null || mHandle == null)
                {
                    return;
                }

                if (!WorldPointerUtility.TryGetPointerWorld(null, out var world))
                {
                    return;
                }

                var local = transform.InverseTransformPoint(world);
                var x = Mathf.Clamp(local.x, mMinX, mMaxX);
                var normalized = Mathf.Approximately(mMaxX, mMinX)
                    ? 0f
                    : Mathf.InverseLerp(mMinX, mMaxX, x);
                if (!mQuiet)
                {
                    InteractionAudioCues.Pulse(
                        InteractionAudioCues.UiPress,
                        "PlayerAudioSettingsPanel.VolumeSlider",
                        mContentId);
                    mQuiet = true;
                }

                mSettings.SetVolume(mBus, normalized);
            }

            public void EndGesture()
            {
                mQuiet = false;
            }

            private void OnDisable()
            {
                mQuiet = false;
            }
        }
    }
}
