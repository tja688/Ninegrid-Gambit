using NUnit.Framework;
using NineGrid.Presentation.Cheat;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NineGrid.Presentation.Tests.Cheat
{
    /// <summary>
    /// 作弊面板运行时自举结构契约：
    /// 场景完全没有预置结构时，OpenPanel / TryToggle 须生成完整面板层级
    /// （根 Overlay Canvas + GraphicRaycaster + EventSystem + 一层五按钮 + 二层输入框/滚动列表），
    /// 且二层初始关闭；按钮一律走 uGUI Button（物理命中路径不适用于 Overlay UI）。
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
            }

            // 自举可能在测试中新建了 EventSystem（独立根物体，不随面板销毁）。
            if (_preExistingEventSystem == null)
            {
                var created = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
                if (created != null)
                {
                    Object.DestroyImmediate(created.gameObject);
                }
            }
        }

        private CheatToolPanelController CreatePanel()
        {
            _panel = new GameObject("作弊工具BG");
            var controller = _panel.AddComponent<CheatToolPanelController>();
            controller.OpenPanel();
            return controller;
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
            CreatePanel();

            Assert.IsNotNull(FindChild(FirstLayerName), "一级菜单须被自举");
            Assert.IsNotNull(FindChild(SecondLayerName), "二级菜单须被自举");
        }

        [Test]
        public void Bootstrap_RootIsOverlayCanvasWithGraphicRaycaster()
        {
            CreatePanel();

            var canvas = _panel.GetComponent<Canvas>();
            Assert.IsNotNull(canvas, "根须有 Canvas");
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            Assert.IsNotNull(_panel.GetComponent<GraphicRaycaster>(), "根 Canvas 须有 GraphicRaycaster");
            Assert.IsNotNull(_panel.GetComponent<CanvasScaler>(), "根 Canvas 须有 CanvasScaler");
        }

        [Test]
        public void Bootstrap_EnsuresEventSystemWhenMissing()
        {
            Assume.That(_preExistingEventSystem == null, "本用例要求环境中原本无 EventSystem");
            CreatePanel();
            Assert.IsNotNull(
                Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include),
                "缺 EventSystem 时须运行时创建");
        }

        [Test]
        public void Bootstrap_FirstLayerHasFiveNamedButtons_AsUGuiButtons()
        {
            CreatePanel();
            var firstLayer = FindChild(FirstLayerName);
            Assert.IsNotNull(firstLayer);

            var names = new[]
            {
                "关闭按钮",
                "一键清关选项",
                "战斗加卡选项",
                "无限金币选项",
                "作弊选项模板 (3)",
            };

            for (var i = 0; i < names.Length; i++)
            {
                var button = FindChildRecursive(firstLayer, names[i]);
                Assert.IsNotNull(button, "缺少按钮「" + names[i] + "」");
                Assert.IsNotNull(button.GetComponent<Button>(), "按钮「" + names[i] + "」须是 uGUI Button");
                Assert.IsNotNull(button.GetComponent<Image>(), "按钮「" + names[i] + "」须有 Image 作 targetGraphic");
            }
        }

        [Test]
        public void Bootstrap_SecondLayerHasInputFieldAndScrollRect_AndStartsInactive()
        {
            CreatePanel();
            var secondLayer = FindChild(SecondLayerName);
            Assert.IsNotNull(secondLayer);

            Assert.IsFalse(secondLayer.gameObject.activeSelf, "打开面板后二级菜单应保持关闭");

            var input = secondLayer.GetComponentInChildren<TMP_InputField>(true);
            Assert.IsNotNull(input, "二级菜单须有 TMP_InputField");
            Assert.IsNotNull(input.textComponent, "输入框须绑定文本组件");

            var scroll = secondLayer.GetComponentInChildren<ScrollRect>(true);
            Assert.IsNotNull(scroll, "二级菜单须有 ScrollRect");
            Assert.IsNotNull(scroll.content, "滚动列表须绑定 Content");
            Assert.IsNotNull(scroll.viewport, "滚动列表须绑定 Viewport");
        }

        [Test]
        public void TryToggle_CreatesPanelAtRuntime_WhenSceneHasNone()
        {
            var existing = Object.FindFirstObjectByType<CheatToolPanelController>();
            Assume.That(existing == null, "本用例要求场景中原本无面板");

            CheatToolPanelController.TryToggle();

            _panel = GameObject.Find("作弊工具BG");
            Assert.IsNotNull(_panel, "TryToggle 应运行时创建面板");
            Assert.IsTrue(_panel.activeSelf, "TryToggle 创建后应直接打开面板");
            Assert.IsNotNull(FindChild(FirstLayerName), "创建的面板须完成自举");
        }
    }
}
