using NineGrid.GameFlow;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NineGrid.Audio
{
    /// <summary>
    /// 主音量 / 音乐 / 音效 三通道滑条 UI。支持 Inspector 手动接线；未接线时运行时自动创建。
    /// 主菜单常显；局内按 F2 切换。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameAudioVolumeUi : MonoBehaviour
    {
        const int CanvasSortOrder = 95;

        [Header("可选：手动接线的 Slider")]
        [SerializeField] Slider masterSlider;
        [SerializeField] Slider musicSlider;
        [SerializeField] Slider sfxSlider;

        [Header("行为")]
        [SerializeField] bool showOnMainMenu = true;
        [SerializeField] bool allowToggleInRun = true;

        GameObject _panelRoot;
        bool _built;
        bool _runPanelOpen;
        bool _flowHooked;

        void OnEnable()
        {
            GameAudioVolumeSettings.Changed += SyncSlidersFromSettings;
        }

        void OnDisable()
        {
            GameAudioVolumeSettings.Changed -= SyncSlidersFromSettings;
            UnhookFlow();
        }

        void Start()
        {
            GameAudioVolumeSettings.EnsureLoaded();
            EnsureUi();
            BindSliders();
            SyncSlidersFromSettings();
            TryHookFlow();
            RefreshVisibility();
        }

        void Update()
        {
            if (!_flowHooked)
            {
                TryHookFlow();
            }

            if (allowToggleInRun && WasF2PressedThisFrame())
            {
                _runPanelOpen = !_runPanelOpen;
                RefreshVisibility();
            }
        }

        void TryHookFlow()
        {
            var flow = GameFlowController.Instance;
            if (flow == null || !flow.IsBooted)
            {
                return;
            }

            if (!_flowHooked)
            {
                flow.StateChanged += OnFlowStateChanged;
                _flowHooked = true;
            }

            RefreshVisibility();
        }

        void UnhookFlow()
        {
            if (!_flowHooked)
            {
                return;
            }

            var flow = GameFlowController.Instance;
            if (flow != null)
            {
                flow.StateChanged -= OnFlowStateChanged;
            }

            _flowHooked = false;
        }

        void OnFlowStateChanged(GameFlowState previous, GameFlowState next)
        {
            RefreshVisibility();
        }

        void RefreshVisibility()
        {
            if (_panelRoot == null)
            {
                return;
            }

            bool onMainMenu = GameFlowController.Instance != null
                && GameFlowController.Instance.IsBooted
                && GameFlowController.Instance.CurrentState == GameFlowState.MainMenu;

            bool visible = (showOnMainMenu && onMainMenu) || (allowToggleInRun && _runPanelOpen && !onMainMenu);
            _panelRoot.SetActive(visible);
        }

        void EnsureUi()
        {
            if (_built && masterSlider != null && musicSlider != null && sfxSlider != null)
            {
                return;
            }

            if (masterSlider != null && musicSlider != null && sfxSlider != null)
            {
                _built = true;
                return;
            }

            BuildRuntimePanel();
            _built = true;
        }

        void BindSliders()
        {
            WireSlider(masterSlider, GameAudioVolumeSettings.Master, v => GameAudioVolumeSettings.Master = v);
            WireSlider(musicSlider, GameAudioVolumeSettings.Music, v => GameAudioVolumeSettings.Music = v);
            WireSlider(sfxSlider, GameAudioVolumeSettings.Sfx, v => GameAudioVolumeSettings.Sfx = v);
        }

        void WireSlider(Slider slider, float initial, System.Action<float> setter)
        {
            if (slider == null)
            {
                return;
            }

            slider.SetValueWithoutNotify(initial);
            slider.onValueChanged.RemoveAllListeners();
            slider.onValueChanged.AddListener(value => setter(value));
        }

        void SyncSlidersFromSettings()
        {
            SetSlider(masterSlider, GameAudioVolumeSettings.Master);
            SetSlider(musicSlider, GameAudioVolumeSettings.Music);
            SetSlider(sfxSlider, GameAudioVolumeSettings.Sfx);
        }

        static void SetSlider(Slider slider, float value)
        {
            if (slider == null)
            {
                return;
            }

            slider.SetValueWithoutNotify(value);
        }

        void BuildRuntimePanel()
        {
            var canvasGo = new GameObject("AudioVolumeCanvas");
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortOrder;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(480f, 270f);
            scaler.matchWidthOrHeight = 0f;

            canvasGo.AddComponent<GraphicRaycaster>();

            _panelRoot = new GameObject("AudioVolumePanel");
            _panelRoot.transform.SetParent(canvasGo.transform, false);

            var panelRect = _panelRoot.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(0f, 0f);
            panelRect.pivot = new Vector2(0f, 0f);
            panelRect.anchoredPosition = new Vector2(12f, 12f);
            panelRect.sizeDelta = new Vector2(220f, 118f);

            var panelImage = _panelRoot.AddComponent<Image>();
            panelImage.color = new Color(0.08f, 0.07f, 0.06f, 0.82f);

            var layout = _panelRoot.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            CreateHeader(_panelRoot.transform, "音量");
            masterSlider = CreateSliderRow(_panelRoot.transform, "主音量");
            musicSlider = CreateSliderRow(_panelRoot.transform, "音乐");
            sfxSlider = CreateSliderRow(_panelRoot.transform, "音效");
        }

        static void CreateHeader(Transform parent, string text)
        {
            var resources = new DefaultControls.Resources();
            var go = DefaultControls.CreateText(resources);
            go.name = "Header";
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.text = text;
            label.fontSize = 14;
            label.color = new Color(0.95f, 0.88f, 0.72f);
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = 18f;
        }

        static Slider CreateSliderRow(Transform parent, string labelText)
        {
            var row = new GameObject(labelText);
            row.transform.SetParent(parent, false);

            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 8f;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            var rowElement = row.AddComponent<LayoutElement>();
            rowElement.preferredHeight = 22f;

            var resources = new DefaultControls.Resources();
            var labelGo = DefaultControls.CreateText(resources);
            labelGo.name = "Label";
            labelGo.transform.SetParent(row.transform, false);
            var label = labelGo.GetComponent<Text>();
            label.text = labelText;
            label.fontSize = 12;
            label.color = new Color(0.88f, 0.86f, 0.8f);
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            var labelLayout = labelGo.AddComponent<LayoutElement>();
            labelLayout.preferredWidth = 52f;

            var sliderGo = DefaultControls.CreateSlider(resources);
            sliderGo.name = "Slider";
            sliderGo.transform.SetParent(row.transform, false);
            var sliderLayout = sliderGo.GetComponent<LayoutElement>() ?? sliderGo.AddComponent<LayoutElement>();
            sliderLayout.flexibleWidth = 1f;
            sliderLayout.preferredHeight = 18f;

            var slider = sliderGo.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            return slider;
        }

        static bool WasF2PressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
            {
                return true;
            }
#endif
            return Input.GetKeyDown(KeyCode.F2);
        }
    }
}
