using NineGrid.Core;
using NineGrid.Presentation.Controllers;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    public sealed class PresentationControllerTests
    {
        [Test]
        public void Controller_BindsToCoreArchitecture()
        {
            NineGridArchitecture.ResetForTests();
            var gameObject = new GameObject("PresentationControllerTest");
            try
            {
                var controller = gameObject.AddComponent<TestPresentationController>();
                Assert.AreSame(NineGridArchitecture.Interface,
                    ((IBelongToArchitecture)controller).GetArchitecture());
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                NineGridArchitecture.ResetForTests();
            }
        }

        [Test]
        public void Controller_RunsLifecycleTemplate_AndUnregistersOnDestroy()
        {
            NineGridArchitecture.ResetForTests();
            var gameObject = new GameObject("PresentationControllerLifecycle");
            try
            {
                var controller = gameObject.AddComponent<TestPresentationController>();

                // EditMode 下 AddComponent 不触发 Unity 消息，显式驱动模板钩子，
                // 断言的是 PresentationController 的生命周期编排本身。
                controller.DriveAwake();
                Assert.AreEqual(1, controller.BindCount);
                Assert.AreEqual(0, controller.UnbindCount);
                Assert.IsFalse(controller.EventUnregistered);

                controller.DriveOnDestroy();
                Assert.AreEqual(1, controller.UnbindCount);
                Assert.IsTrue(controller.EventUnregistered);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                NineGridArchitecture.ResetForTests();
            }
        }
    }

    public sealed class TestPresentationController : PresentationController
    {
        public int BindCount { get; private set; }
        public int UnbindCount { get; private set; }
        public bool EventUnregistered { get; private set; }

        public void DriveAwake() { Awake(); }

        public void DriveOnDestroy() { OnDestroy(); }

        protected override void OnBind()
        {
            BindCount++;
            new StubUnRegister(() => EventUnregistered = true).AddToUnregisterList(this);
        }

        protected override void OnUnbind()
        {
            UnbindCount++;
        }

        private sealed class StubUnRegister : IUnRegister
        {
            private readonly System.Action mOnUnRegister;

            public StubUnRegister(System.Action onUnRegister)
            {
                mOnUnRegister = onUnRegister;
            }

            public void UnRegister()
            {
                mOnUnRegister();
            }
        }
    }
}
