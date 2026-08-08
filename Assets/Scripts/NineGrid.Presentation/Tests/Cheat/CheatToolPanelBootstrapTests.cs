using NUnit.Framework;
using NineGrid.Presentation.Cheat;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NineGrid.Presentation.Tests.Cheat
{
    /// <summary>
    /// 作弊面板接线契约：对齐 MainScene「作弊工具BG」
    /// （Sprite + BoxCollider2D 一级按钮；二级 WorldSpace Canvas + TMP_InputField + ScrollRect）。
    /// </summary>
    public sealed class CheatToolPanelBootstrapTests
    {
        private const string FirstLayerName = "第一层主面板";
        private const string SecondLayerName = "第二层_添加卡菜单";

        private GameObject _panel;
        private EventSystem _preExistingEventSystem;

        [SetUp]
        public void SetUp()
        {
            _preExistingEventSystem = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
        }

        [TearDown]
        public void TearDown()
        {
            if (_panel != null)
            {
                Object.DestroyImmediate(_panel);
                _panel = null;
            }

            if (_preExistingEventSystem == null)
            {
                var created = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
                if (created != null)
                {
                    Object.DestroyImmediate(created.gameObject);
                }
            }
        }

        private CheatToolPanelController CreateEmptyPanelAndOpen()
        {
            _panel = new GameObject(CheatToolPanelController.PanelRootName);
            var controller = _panel.AddComponent<CheatToolPanelController>();
            controller.OpenPanel();
            return controller;
        }

        private CheatToolPanelController CreateSceneLikePanelAndOpen()
        {
            _panel = new GameObject(CheatToolPanelController.PanelRootName);
            _panel.AddComponent<SpriteRenderer>();
            _panel.SetActive(false);

            var first = new GameObject(FirstLayerName);
            first.transform.SetParent(_panel.transform, false);
            CreateHitStub("关闭按钮", first.transform);
            CreateHitStub("一键清关选项", first.transform);
            CreateHitStub("战斗加卡选项", first.transform);
            CreateHitStub("无限金币选项", first.transform);
            CreateHitStub("回复满血选项", first.transform);
            CreateHitStub("记录log选项", first.transform);

            var second = new GameObject(SecondLayerName, typeof(RectTransform), typeof(Canvas));
            second.transform.SetParent(_panel.transform, false);
            second.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            second.SetActive(false);

            var inputGo = new GameObject("InputField (TMP)", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            inputGo.transform.SetParent(second.transform, false);
            var input = inputGo.GetComponent<TMP_InputField>();
            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(inputGo.transform, false);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            input.textComponent = text;
            input.targetGraphic = inputGo.GetComponent<Image>();

            var scrollGo = new GameObject("Scroll View", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(second.transform, false);
            var scroll = scrollGo.GetComponent<ScrollRect>();
            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(scrollGo.transform, false);
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = content.GetComponent<RectTransform>();

            var logLayer = new GameObject("第二层_log记录面板", typeof(RectTransform), typeof(Canvas));
            logLayer.transform.SetParent(_panel.transform, false);
            logLayer.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            logLayer.SetActive(false);

            var noticeGo = new GameObject("notice text", typeof(RectTransform));
            noticeGo.transform.SetParent(logLayer.transform, false);
            noticeGo.AddComponent<TextMeshProUGUI>().text = "提醒log成功保存的消息框";

            var logInputGo = new GameObject("InputField (TMP)", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            logInputGo.transform.SetParent(logLayer.transform, false);
            var logInput = logInputGo.GetComponent<TMP_InputField>();
            var logTextGo = new GameObject("Text", typeof(RectTransform));
            logTextGo.transform.SetParent(logInputGo.transform, false);
            logInput.textComponent = logTextGo.AddComponent<TextMeshProUGUI>();
            logInput.targetGraphic = logInputGo.GetComponent<Image>();

            var saveGo = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            saveGo.transform.SetParent(logLayer.transform, false);

            var controller = _panel.AddComponent<CheatToolPanelController>();
            controller.OpenPanel();
            return controller;
        }

        private static void CreateHitStub(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<BoxCollider2D>().size = new Vector2(2.4f, 0.56f);
        }

        private Transform FindChild(string name)
        {
            return FindChildRecursive(_panel.transform, name);
        }

        private static Transform FindChildRecursive(Transform root, string name)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == name)
                {
                    return child;
                }

                var found = FindChildRecursive(child, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        [Test]
        public void OpenPanel_BootstrapsBothLayers_WhenSceneHasNothing()
        {
            CreateEmptyPanelAndOpen();

            Assert.IsNotNull(FindChild(FirstLayerName), "一级菜单须被自举");
            Assert.IsNotNull(FindChild(SecondLayerName), "二级加卡菜单须被自举");
            Assert.IsNotNull(FindChild("第二层_log记录面板"), "log 记录层须被自举");
            Assert.IsNotNull(FindChild("记录log选项"), "记录log 一级按钮须被自举");
            Assert.IsNotNull(FindChild("回复满血选项"), "回复满血一级按钮须被自举");
        }

        [Test]
        public void Bootstrap_FirstLayerButtons_UsePointerHitProxy()
        {
            CreateSceneLikePanelAndOpen();
            var firstLayer = FindChild(FirstLayerName);
            Assert.IsNotNull(firstLayer);

            var names = new[]
            {
                "关闭按钮",
                "一键清关选项",
                "战斗加卡选项",
                "无限金币选项",
                "回复满血选项",
                "记录log选项",
            };

            for (var i = 0; i < names.Length; i++)
            {
                var button = FindChildRecursive(firstLayer, names[i]);
                Assert.IsNotNull(button, "缺少按钮「" + names[i] + "」");
                Assert.IsNotNull(
                    button.GetComponent<BoxCollider2D>(),
                    "按钮「" + names[i] + "」须有 BoxCollider2D");
                Assert.IsNotNull(
                    button.GetComponent<CheatToolPanelButton>(),
                    "按钮「" + names[i] + "」须挂 CheatToolPanelButton（物理命中）");
                Assert.IsNull(
                    button.GetComponent<Button>(),
                    "场景一级按钮不应被改成 uGUI Button");
            }
        }

        [Test]
        public void Bootstrap_EnsuresEventSystemWhenMissing()
        {
            Assume.That(_preExistingEventSystem == null, "本用例要求环境中原本无 EventSystem");
            CreateEmptyPanelAndOpen();
            Assert.IsNotNull(
                Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include),
                "缺 EventSystem 时须运行时创建");
        }

        [Test]
        public void Bootstrap_SecondLayerGetsGraphicRaycaster_AndStartsInactive()
        {
            CreateSceneLikePanelAndOpen();
            var secondLayer = FindChild(SecondLayerName);
            Assert.IsNotNull(secondLayer);

            Assert.IsFalse(secondLayer.gameObject.activeSelf, "打开面板后二级菜单应保持关闭");

            var canvas = secondLayer.GetComponent<Canvas>();
            Assert.IsNotNull(canvas, "二级菜单须有 Canvas");
            Assert.IsNotNull(
                canvas.GetComponent<GraphicRaycaster>(),
                "二级 Canvas 须补 GraphicRaycaster，否则 InputField/ScrollView 点不中");

            var input = secondLayer.GetComponentInChildren<TMP_InputField>(true);
            Assert.IsNotNull(input, "二级菜单须有 TMP_InputField");

            var scroll = secondLayer.GetComponentInChildren<ScrollRect>(true);
            Assert.IsNotNull(scroll, "二级菜单须有 ScrollRect");
            Assert.IsNotNull(scroll.content, "滚动列表须绑定 Content");
            Assert.IsNotNull(scroll.viewport, "滚动列表须绑定 Viewport");
        }

        [Test]
        public void Bootstrap_LogLayerGetsGraphicRaycaster_AndStartsInactive()
        {
            CreateSceneLikePanelAndOpen();
            var logLayer = FindChild("第二层_log记录面板");
            Assert.IsNotNull(logLayer);

            Assert.IsFalse(logLayer.gameObject.activeSelf, "打开面板后 log 记录层应保持关闭");

            var canvas = logLayer.GetComponent<Canvas>();
            Assert.IsNotNull(canvas, "log 记录层须有 Canvas");
            Assert.IsNotNull(
                canvas.GetComponent<GraphicRaycaster>(),
                "log Canvas 须补 GraphicRaycaster，否则 InputField/Button 点不中");

            Assert.IsNotNull(
                FindChildRecursive(logLayer, "InputField (TMP)"),
                "log 层须有 Tag 输入框");
            Assert.IsNotNull(
                FindChildRecursive(logLayer, "notice text"),
                "log 层须有成功提醒文本");
            Assert.IsNotNull(
                FindChildRecursive(logLayer, "Button")?.GetComponent<Button>(),
                "log 层须有保存 Button");
        }

        [Test]
        public void OpenLogRecordMenu_ShowsLogLayer_AndCloseRestoresFirst()
        {
            var controller = CreateSceneLikePanelAndOpen();
            controller.OpenLogRecordMenu();

            var first = FindChild(FirstLayerName);
            var log = FindChild("第二层_log记录面板");
            Assert.IsNotNull(first);
            Assert.IsNotNull(log);
            Assert.IsFalse(first.gameObject.activeSelf);
            Assert.IsTrue(log.gameObject.activeSelf);

            controller.CloseLogRecordMenu();
            Assert.IsFalse(log.gameObject.activeSelf);
            Assert.IsTrue(first.gameObject.activeSelf);
        }

        [Test]
        public void OpenAddCardMenu_ClearsInput_AndCloseCleansUp()
        {
            var controller = CreateSceneLikePanelAndOpen();
            controller.OpenAddCardMenu();

            var second = FindChild(SecondLayerName);
            Assert.IsTrue(second.gameObject.activeSelf);
            var input = second.GetComponentInChildren<TMP_InputField>(true);
            Assert.IsNotNull(input);
            input.text = "残留搜索";

            controller.CloseAddCardMenu();
            Assert.IsFalse(second.gameObject.activeSelf);
            Assert.AreEqual(string.Empty, input.text, "关闭二级菜单须清空输入");
        }

        [Test]
        public void TryToggle_FindsInactiveScenePanel_ByName()
        {
            _panel = new GameObject(CheatToolPanelController.PanelRootName);
            _panel.SetActive(false);
            // 模拟场景失活预置：尚无 Controller。
            Assume.That(
                Object.FindFirstObjectByType<CheatToolPanelController>(FindObjectsInactive.Include) == null);

            CheatToolPanelController.TryToggle();

            Assert.IsTrue(_panel.activeSelf, "TryToggle 应打开场景失活面板");
            Assert.IsNotNull(_panel.GetComponent<CheatToolPanelController>());
        }
    }
}
