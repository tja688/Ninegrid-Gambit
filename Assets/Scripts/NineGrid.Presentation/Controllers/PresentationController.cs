using System.Collections.Generic;
using NineGrid.Core;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Controllers
{
    /// <summary>
    /// 场景表现层 Controller 统一基类：绑定 Core Architecture，并提供
    /// Awake/OnDestroy 生命周期模板——子类用 OnBind/OnUnbind 接线，注册的
    /// QFramework 事件经 UnregisterList 在销毁时自动注销，避免悬挂订阅。
    /// </summary>
    public abstract class PresentationController : MonoBehaviour, IController, IUnRegisterList
    {
        public List<IUnRegister> UnregisterList { get; } = new List<IUnRegister>();

        public IArchitecture GetArchitecture()
        {
            return NineGridArchitecture.Interface;
        }

        protected virtual void Awake()
        {
            OnBind();
        }

        protected virtual void OnDestroy()
        {
            this.UnRegisterAll();
            OnUnbind();
        }

        protected virtual void OnBind() { }

        protected virtual void OnUnbind() { }
    }
}
