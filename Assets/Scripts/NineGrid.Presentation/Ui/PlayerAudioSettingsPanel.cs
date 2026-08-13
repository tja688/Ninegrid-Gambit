using System;
using NineGrid.Content.Audio;
using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Commands;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Ui
{
    /// <summary>
    /// 局内功能菜单场景接线：主菜单「菜单按钮」或局内 Esc（无半黑屏时）打开，半黑屏 / 关闭钮 / Esc 关闭；
    /// 音量模块与「回到主菜单」「退出游戏」走既有流程命令，不生成运行时 ugui。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class PlayerAudioSettingsPanel : MonoBehaviour
    {
        public const string PanelRootName = "局内功能菜单BG";
        public const string MenuButtonName = "菜单按钮";
        public const string VolumeModuleName = "音量模块";
        public const string CloseButtonPath = "功能模块/关闭面板";
        public const string ReturnToMainMenuButtonPath = "功能模块/回到主菜单";
        public const string QuitGameButtonPath = "功能模块/退出游戏";
        public const string ConfirmPromptPath = "功能模块/提示框";
        private const string DimmerReason = "in-run-function-menu";
        private const float SliderHandleHalfPad = 0.28f;

        private static PlayerAudioSettingsPanel sInstance;
        private static EscapeInputRelay sEscapeRelay;

        private IPlayerAudioSettingsSystem mSettings;
        private GameObject mPanelRoot;
        private GameObject mMenuButton;
        private BoxCollider2D mMenuButtonCollider;
        private Transform mMasterMuteRoot;
        private Transform mMasterMuteOn;
        private Transform mBgmMuteRoot;
        private Transform mBgmMuteOff;
        private VolumeSliderBinder mBgmSlider;
        private VolumeSliderBinder mSfxSlider;
        private UiConfirmPrompt mPrompt;
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
            // 面板默认失活：场景序列化组件不会立刻 Awake，须在此强制接线，
            // 否则主菜单「菜单按钮」永远挂不上 FunctionMenuHitProxy。
            var live = FindSceneInstance() ?? EnsureFromScene();
            if (live == null)
            {
                return;
            }

            live.EnsureInstanceState();
            if (live.mPanelRoot != null && live.mPanelRoot.activeSelf)
            {
                live.mPanelRoot.SetActive(false);
            }

            if (Application.isPlaying)
            {
                live.EnsureBound();
                EnsureEscapeRelay();
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
            EnsureInstanceState();
            // 勿在此强制 SetActive(false)：面板默认失活，Awake 会推迟到首次打开；
            // 若此处再关一次，会把 SetOpen(true) 刚打开的面板立刻关掉。
            // 开局误激活的收口只放在 Install。
            if (Application.isPlaying)
            {
                EnsureBound();
            }
        }

        private void EnsureInstanceState()
        {
            sInstance = this;
            if (mPanelRoot == null)
            {
                mPanelRoot = gameObject;
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

            if (TryHandleEscapeClose())
            {
                return;
            }
        }

        private static void EnsureEscapeRelay()
        {
            if (sEscapeRelay != null)
            {
                return;
            }

            var relayObject = new GameObject(nameof(PlayerAudioSettingsPanel) + ".EscapeRelay");
            sEscapeRelay = relayObject.AddComponent<EscapeInputRelay>();
        }

        private static bool TryHandleEscapeClose()
        {
            if (!KeyboardUtility.GetKeyDown(KeyCode.Escape))
            {
                return false;
            }

            // 提示框开着（或本帧刚被它消费）时 Esc 让位给提示框，不连带关整个功能菜单。
            if (UiConfirmPrompt.IsAnyOpen || UiConfirmPrompt.EscapeHandledThisFrame)
            {
                return true;
            }

            TriggerPulseHub.PulseAudio(AudioCueRequest.Simple(
                InteractionAudioCues.PlayerAudioEscape,
                "PlayerAudioSettingsPanel.Update.Escape"));
            if (sInstance != null)
            {
                sInstance.SetOpen(false);
            }

            return true;
        }

        private static bool CanOpenFromEscape()
        {
            if (IsOpen || BattleUiDimmerOverlay.IsActive)
            {
                return false;
            }

            var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            var shell = arch?.GetSystem<IGameFlowShellSystem>();
            return shell != null && shell.State.Value != GameFlowShellState.MainMenu;
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
            WireFlowMenuButton(
                mPanelRoot.transform.Find(ReturnToMainMenuButtonPath),
                RequestReturnToMainMenu);
            WireFlowMenuButton(
                mPanelRoot.transform.Find(QuitGameButtonPath),
                RequestQuitGame);

            // 危险操作二次确认提示框（覆盖存档 / 退出 / 回主菜单共用）。
            mPrompt = UiConfirmPrompt.Attach(mPanelRoot.transform.Find(ConfirmPromptPath));

            // 存档/读档模块（名字含 '/'，不能走 transform.Find 路径语义）。
            RunSaveLoadPanel.EnsureBound(
                FindDirectChildNamed(mPanelRoot.transform, RunSaveLoadPanel.ModuleName));

            var volume = mPanelRoot.transform.Find(VolumeModuleName);
            if (volume != null)
            {
                mMasterMuteRoot = volume.Find("总音量关");
                mMasterMuteOn = volume.Find("总音量关/总音量开");
                mBgmMuteRoot = volume.Find("音乐开");
                mBgmMuteOff = volume.Find("音乐开/音乐关");
                WireMuteControl(
                    mMasterMuteRoot,
                    mMasterMuteOn,
                    null,
                    PlayerAudioBus.Master,
                    "player_audio.master.mute");
                WireMuteControl(
                    mBgmMuteRoot,
                    null,
                    mBgmMuteOff,
                    PlayerAudioBus.Bgm,
                    "player_audio.bgm.mute");
                mBgmSlider = WireSlider(volume.Find("bgm音量滑条"), PlayerAudioBus.Bgm, "player_audio.bgm.volume");
                mSfxSlider = WireSlider(volume.Find("SFX音量滑条 (1)"), PlayerAudioBus.Sfx, "player_audio.sfx.volume");
            }

            RefreshVisuals(mSettings.Current);
            if (mMenuButtonCollider != null)
            {
                mMenuButtonCollider.enabled = !IsOpen;
            }

            mBound = true;
        }

        private void SetOpen(bool open)
        {
            // 域重载后静态实例可能带着未赋值的 mPanelRoot 直接被 RequestOpen；先归位再绑定。
            EnsureInstanceState();
            EnsureBound();
            if (mPanelRoot == null)
            {
                return;
            }

            if (open == mPanelRoot.activeSelf)
            {
                // 即使开关态未变，也同步菜单按钮可点状态（避免失活面板旁路关面板后 collider 卡死）。
                if (mMenuButtonCollider != null)
                {
                    mMenuButtonCollider.enabled = !open;
                }

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
            ApplyMuteVisuals(
                mMasterMuteRoot,
                mMasterMuteOn,
                null,
                snapshot.MasterMuted);
            ApplyMuteVisuals(
                mBgmMuteRoot,
                null,
                mBgmMuteOff,
                snapshot.BgmMuted);

            mBgmSlider?.SetNormalized(snapshot.BgmVolume);
            mSfxSlider?.SetNormalized(snapshot.SfxVolume);
        }

        private static void ApplyMuteVisuals(
            Transform root,
            Transform onIndicator,
            Transform offIndicator,
            bool muted)
        {
            if (onIndicator != null)
            {
                onIndicator.gameObject.SetActive(!muted);
            }

            if (offIndicator != null)
            {
                offIndicator.gameObject.SetActive(muted);
            }

            // 父节点与指示态子节点常叠在同一位置；只显示当前态，避免排序盖住反馈。
            if (root != null)
            {
                var showRoot = muted ? offIndicator == null : onIndicator == null;
                SetSpriteRendererVisible(root, showRoot);
            }
        }

        private static void SetSpriteRendererVisible(Transform target, bool visible)
        {
            if (target == null)
            {
                return;
            }

            var renderer = target.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }

        private void WireMenuButton(GameObject button)
        {
            if (button == null)
            {
                return;
            }

            // 优先沿用场景预置碰撞体尺寸；缺失时按精灵包围盒，避免硬编码 0.5 点不中。
            var preferred = PreferSpriteSize(button.transform);
            var existing = button.GetComponent<BoxCollider2D>();
            if (existing != null && existing.size.x > 0.01f && existing.size.y > 0.01f)
            {
                preferred = existing.size;
            }

            mMenuButtonCollider = EnsureCollider(button, preferred);
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

        private void WireFlowMenuButton(Transform root, Action onClick)
        {
            if (root == null || onClick == null)
            {
                return;
            }

            WireFlowMenuClickTarget(root, onClick);
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (IsDecoratorNode(child))
                {
                    continue;
                }

                WireFlowMenuClickTarget(child, onClick);
            }
        }

        private static void WireFlowMenuClickTarget(Transform target, Action onClick)
        {
            if (target == null)
            {
                return;
            }

            var preferred = PreferSpriteSize(target);
            var existing = target.GetComponent<BoxCollider2D>();
            if (existing != null && existing.size.x > 0.01f && existing.size.y > 0.01f)
            {
                preferred = existing.size;
            }

            EnsureCollider(target.gameObject, preferred);
            BindClick(target.gameObject, onClick, BattleUiDimmerOverlay.CloseHitSort);
        }

        private void RequestReturnToMainMenu()
        {
            InteractionAudioCues.Pulse(
                InteractionAudioCues.UiPress,
                "PlayerAudioSettingsPanel.RequestReturnToMainMenu",
                "in_run_function_menu.return_main_menu");
            if (mPrompt != null)
            {
                mPrompt.Show(UiConfirmPrompt.ReturnMainMenuMessage, DoReturnToMainMenu);
                return;
            }

            DoReturnToMainMenu();
        }

        private void DoReturnToMainMenu()
        {
            InteractionAudioCues.Pulse(
                InteractionAudioCues.UiConfirm,
                "PlayerAudioSettingsPanel.DoReturnToMainMenu",
                "in_run_function_menu.return_main_menu");
            SetOpen(false);

            var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            if (arch != null)
            {
                arch.SendCommand(new ReturnToMainMenuCommand());
                return;
            }

            ResolveFlowController()?.ReturnToMainMenu();
        }

        private void RequestQuitGame()
        {
            InteractionAudioCues.Pulse(
                InteractionAudioCues.UiPress,
                "PlayerAudioSettingsPanel.RequestQuitGame",
                "main_menu.quit");
            if (mPrompt != null)
            {
                mPrompt.Show(UiConfirmPrompt.QuitGameMessage, DoQuitGame);
                return;
            }

            DoQuitGame();
        }

        private void DoQuitGame()
        {
            InteractionAudioCues.Pulse(
                InteractionAudioCues.MainMenuPress,
                "PlayerAudioSettingsPanel.DoQuitGame",
                "main_menu.quit");
            InteractionAudioCues.Pulse(
                InteractionAudioCues.MainMenuCancel,
                "PlayerAudioSettingsPanel.DoQuitGame",
                "main_menu.quit");
            SetOpen(false);
            ResolveFlowController()?.QuitGame();
        }

        private static GameFlowController ResolveFlowController()
        {
            return UnityEngine.Object.FindFirstObjectByType<GameFlowController>();
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

        private void WireMuteControl(
            Transform root,
            Transform onIndicator,
            Transform offIndicator,
            PlayerAudioBus bus,
            string contentId)
        {
            if (root == null)
            {
                return;
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

            WireMuteClickTarget(root, Toggle);
            WireMuteClickTarget(onIndicator, Toggle);
            WireMuteClickTarget(offIndicator, Toggle);

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child == onIndicator || child == offIndicator || IsDecoratorNode(child))
                {
                    continue;
                }

                WireMuteClickTarget(child, Toggle);
            }
        }

        private static void WireMuteClickTarget(Transform target, Action onClick)
        {
            if (target == null)
            {
                return;
            }

            EnsureCollider(target.gameObject, PreferSpriteSize(target));
            BindClick(target.gameObject, onClick, BattleUiDimmerOverlay.CloseHitSort);
        }

        private static bool IsDecoratorNode(Transform node)
        {
            return node != null && node.name.StartsWith("__", StringComparison.Ordinal);
        }

        private static Transform FindDirectChildNamed(Transform parent, string childName)
        {
            if (parent == null)
            {
                return null;
            }

            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child != null && string.Equals(child.name, childName, StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
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

            if (!TryGetSliderRange(track, handle, out var minX, out var maxX))
            {
                return null;
            }

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

        private static bool TryGetSliderRange(Transform track, Transform handle, out float minX, out float maxX)
        {
            minX = 0f;
            maxX = 1f;
            var trackSr = track != null ? track.GetComponent<SpriteRenderer>() : null;
            if (trackSr == null || trackSr.sprite == null)
            {
                return false;
            }

            var handleHalf = SliderHandleHalfPad;
            var handleSr = handle != null ? handle.GetComponent<SpriteRenderer>() : null;
            if (handleSr != null && handleSr.sprite != null)
            {
                handleHalf = handleSr.drawMode == SpriteDrawMode.Simple
                    ? handleSr.sprite.bounds.extents.x
                    : handleSr.size.x * 0.5f;
                handleHalf = Mathf.Max(handleHalf * 0.85f, 0.04f);
            }

            var bounds = trackSr.localBounds;
            minX = bounds.min.x + handleHalf;
            maxX = bounds.max.x - handleHalf;
            if (maxX < minX)
            {
                var center = (bounds.min.x + bounds.max.x) * 0.5f;
                minX = center;
                maxX = center;
            }

            return true;
        }

        private static Vector2 PreferSpriteSize(Transform t)
        {
            var sr = t.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                // Sliced/Tiled 以 Renderer.size 为准；Simple 用精灵包围盒。
                var size = sr.drawMode == SpriteDrawMode.Simple
                    ? (Vector2)sr.sprite.bounds.size
                    : sr.size;
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

        /// <summary>面板根失活时仍监听 Esc 打开；关闭仍由激活中的面板 Update 处理。</summary>
        [DisallowMultipleComponent]
        [DefaultExecutionOrder(99)]
        private sealed class EscapeInputRelay : MonoBehaviour
        {
            private void Update()
            {
                if (!KeyboardUtility.GetKeyDown(KeyCode.Escape) || !CanOpenFromEscape())
                {
                    return;
                }

                RequestOpen();
            }

            private void OnDestroy()
            {
                if (sEscapeRelay == this)
                {
                    sEscapeRelay = null;
                }
            }
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
