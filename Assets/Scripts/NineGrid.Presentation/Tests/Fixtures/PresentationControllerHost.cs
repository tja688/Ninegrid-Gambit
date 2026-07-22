using System;
using NineGrid.Presentation.Controllers;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.Fixtures
{
    /// <summary>
    /// 迁移票复用：EditMode 下创建 Controller 探针并显式驱动 Awake/OnDestroy。
    /// </summary>
    public sealed class PresentationControllerHost : IDisposable
    {
        private GameObject mGameObject;

        public ProbePresentationController Controller { get; private set; }

        public static PresentationControllerHost Create(string name = "PresentationControllerHost")
        {
            var go = new GameObject(name);
            var controller = go.AddComponent<ProbePresentationController>();
            return new PresentationControllerHost
            {
                mGameObject = go,
                Controller = controller
            };
        }

        public void Dispose()
        {
            if (mGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(mGameObject);
                mGameObject = null;
            }

            Controller = null;
        }
    }

    public sealed class ProbePresentationController : PresentationController
    {
        public int BindCount { get; private set; }
        public int UnbindCount { get; private set; }
        public bool EventUnregistered { get; private set; }

        public void DriveAwake()
        {
            Awake();
        }

        public void DriveOnDestroy()
        {
            OnDestroy();
        }

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
            private readonly Action mOnUnRegister;

            public StubUnRegister(Action onUnRegister)
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
