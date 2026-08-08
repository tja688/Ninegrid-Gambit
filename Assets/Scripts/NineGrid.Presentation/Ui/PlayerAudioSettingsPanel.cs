using NineGrid.Presentation.Systems;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NineGrid.Presentation.Ui
{
    /// <summary>
    /// Player-facing audio menu. It only binds <see cref="IPlayerAudioSettingsSystem"/>;
    /// authoring remains exclusively in the Editor tuning workbench.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class PlayerAudioSettingsPanel : MonoBehaviour
    {
        private const string RootName = "PlayerAudioSettings";

        private IPlayerAudioSettingsSystem mSettings;
        private GameObject mPanel;
        private Button mOpenButton;
        private bool mBuilt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<PlayerAudioSettingsPanel>() != null)
            {
                return;
            }

            var host = new GameObject(RootName);
            DontDestroyOnLoad(host);
            host.AddComponent<PlayerAudioSettingsPanel>();
        }

        private void Awake()
        {
            mSettings = PlayerAudioSettingsSystem.EnsureRegistered();
            mSettings.Changed += Refresh;
            Build();
        }

        private void OnDestroy()
        {
            if (mSettings != null)
            {
                mSettings.Changed -= Refresh;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && mPanel != null && mPanel.activeSelf)
            {
                SetOpen(false);
            }
        }

        private void Build()
        {
            if (mBuilt)
            {
                return;
            }

            EnsureEventSystem();
            var canvasObject = new GameObject(
                RootName + "Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2000;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            mOpenButton = CreateButton(canvasObject.transform, "音频设置", new Vector2(-120f, -70f), new Vector2(180f, 52f));
            mOpenButton.onClick.AddListener(() => SetOpen(true));

            mPanel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            mPanel.transform.SetParent(canvasObject.transform, false);
            var panelRect = mPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(680f, 500f);
            mPanel.GetComponent<Image>().color = new Color(0.11f, 0.08f, 0.05f, 0.96f);

            CreateLabel(mPanel.transform, "标题", "声音设置", new Vector2(0f, 205f), 30, TextAnchor.MiddleCenter, new Vector2(600f, 48f));
            CreateLabel(mPanel.transform, "说明", "玩家偏好仅保存于本机；作者调音工作台不会被修改。", new Vector2(0f, 160f), 16, TextAnchor.MiddleCenter, new Vector2(600f, 32f));
            CreateBusRow(PlayerAudioBus.Master, "总音量", 80f);
            CreateBusRow(PlayerAudioBus.Bgm, "BGM", -15f);
            CreateBusRow(PlayerAudioBus.Sfx, "音效", -110f);

            var reset = CreateButton(mPanel.transform, "恢复作者默认", new Vector2(-120f, -190f), new Vector2(210f, 46f));
            reset.onClick.AddListener(() => mSettings.ResetToAuthorDefaults());
            var close = CreateButton(mPanel.transform, "关闭", new Vector2(120f, -190f), new Vector2(150f, 46f));
            close.onClick.AddListener(() => SetOpen(false));

            mPanel.SetActive(false);
            mBuilt = true;
        }

        private void CreateBusRow(PlayerAudioBus bus, string label, float y)
        {
            CreateLabel(mPanel.transform, label, label, new Vector2(-238f, y), 21, TextAnchor.MiddleLeft, new Vector2(150f, 42f));
            var slider = CreateSlider(mPanel.transform, label + "音量", new Vector2(35f, y), new Vector2(330f, 36f));
            slider.onValueChanged.AddListener(value => mSettings.SetVolume(bus, value));
            var mute = CreateToggle(mPanel.transform, label + "静音", "静音", new Vector2(245f, y), new Vector2(130f, 36f));
            mute.onValueChanged.AddListener(value => mSettings.SetMuted(bus, value));
            RefreshBus(bus, slider, mute);
        }

        private void Refresh(PlayerAudioSettingsSnapshot snapshot)
        {
            if (mPanel == null)
            {
                return;
            }

            RefreshBus(PlayerAudioBus.Master, "总音量");
            RefreshBus(PlayerAudioBus.Bgm, "BGM");
            RefreshBus(PlayerAudioBus.Sfx, "音效");
        }

        private void RefreshBus(PlayerAudioBus bus, string label)
        {
            var slider = mPanel.transform.Find(label + "音量")?.GetComponent<Slider>();
            var mute = mPanel.transform.Find(label + "静音")?.GetComponent<Toggle>();
            if (slider != null && mute != null)
            {
                RefreshBus(bus, slider, mute);
            }
        }

        private void RefreshBus(PlayerAudioBus bus, Slider slider, Toggle mute)
        {
            var current = mSettings.Current;
            slider.SetValueWithoutNotify(current.Volume(bus));
            mute.SetIsOnWithoutNotify(current.IsMuted(bus));
        }

        private void SetOpen(bool open)
        {
            if (mPanel != null)
            {
                mPanel.SetActive(open);
            }

            if (mOpenButton != null)
            {
                mOpenButton.gameObject.SetActive(!open);
            }
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var host = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(host);
        }

        private static Button CreateButton(Transform parent, string text, Vector2 anchoredPosition, Vector2 size)
        {
            var root = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            SetRect(root.GetComponent<RectTransform>(), anchoredPosition, size);
            root.GetComponent<Image>().color = new Color(0.44f, 0.25f, 0.11f, 1f);
            var button = root.GetComponent<Button>();
            button.targetGraphic = root.GetComponent<Image>();
            CreateLabel(root.transform, "Text", text, Vector2.zero, 18, TextAnchor.MiddleCenter, size);
            return button;
        }

        private static Slider CreateSlider(Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Slider));
            root.transform.SetParent(parent, false);
            SetRect(root.GetComponent<RectTransform>(), anchoredPosition, size);
            root.GetComponent<Image>().color = new Color(0.23f, 0.17f, 0.12f, 1f);
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(root.transform, false);
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = new Vector2(4f, 4f);
            fillRect.offsetMax = new Vector2(-4f, -4f);
            fill.GetComponent<Image>().color = new Color(0.83f, 0.55f, 0.22f, 1f);
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(root.transform, false);
            var handleRect = handle.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0f, 0.5f);
            handleRect.anchorMax = new Vector2(0f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(20f, 44f);
            handle.GetComponent<Image>().color = new Color(0.95f, 0.86f, 0.72f, 1f);
            var slider = root.GetComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            return slider;
        }

        private static Toggle CreateToggle(Transform parent, string name, string label, Vector2 anchoredPosition, Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Toggle));
            root.transform.SetParent(parent, false);
            SetRect(root.GetComponent<RectTransform>(), anchoredPosition, size);
            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(root.transform, false);
            SetRect(background.GetComponent<RectTransform>(), new Vector2(-48f, 0f), new Vector2(28f, 28f));
            background.GetComponent<Image>().color = new Color(0.23f, 0.17f, 0.12f, 1f);
            var checkmark = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            checkmark.transform.SetParent(background.transform, false);
            SetRect(checkmark.GetComponent<RectTransform>(), Vector2.zero, new Vector2(18f, 18f));
            checkmark.GetComponent<Image>().color = new Color(0.83f, 0.55f, 0.22f, 1f);
            CreateLabel(root.transform, "Label", label, new Vector2(25f, 0f), 17, TextAnchor.MiddleLeft, new Vector2(84f, 32f));
            var toggle = root.GetComponent<Toggle>();
            toggle.targetGraphic = background.GetComponent<Image>();
            toggle.graphic = checkmark.GetComponent<Image>();
            return toggle;
        }

        private static Text CreateLabel(Transform parent, string name, string text, Vector2 anchoredPosition, int fontSize, TextAnchor alignment, Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Text));
            root.transform.SetParent(parent, false);
            SetRect(root.GetComponent<RectTransform>(), anchoredPosition, size);
            var label = root.GetComponent<Text>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = new Color(0.95f, 0.86f, 0.72f, 1f);
            return label;
        }

        private static void SetRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }
    }
}
