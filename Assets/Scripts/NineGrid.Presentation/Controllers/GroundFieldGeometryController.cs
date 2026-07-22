using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Controllers
{
    /// <summary>
    /// 场地几何 / 收敛宿主 Controller：将场景 View 登记到 QF System，并接线 Hook。
    /// </summary>
    public sealed class GroundFieldGeometryController : PresentationController
    {
        private GroundFieldManagerSingleton mField;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterInstallHook()
        {
            GroundFieldGeometryHook.Wire = WireField;
        }

        protected override void OnBind()
        {
            if (mField != null)
            {
                ApplyBind(mField);
            }
        }

        protected override void OnUnbind()
        {
            ClearBind();
        }

        /// <summary>绑定 Field View（Hook 与 EditMode 直驱共用）。</summary>
        public void BindField(GroundFieldManagerSingleton field)
        {
            mField = field;
            ApplyBind(field);
        }

        private void ClearBind()
        {
            var system = TryGetGeometrySystem();
            if (system != null && mField != null)
            {
                system.UnbindIfView(mField);
            }

            if (ReferenceEquals(GroundFieldGeometryHook.ResolveField?.Invoke(), mField))
            {
                GroundFieldGeometryHook.ResolveField = null;
            }

            mField = null;
        }

        private static void WireField(GroundFieldManagerSingleton field)
        {
            var existing = Object.FindObjectOfType<GroundFieldGeometryController>();
            if (existing == null)
            {
                var host = new GameObject(nameof(GroundFieldGeometryController));
                existing = host.AddComponent<GroundFieldGeometryController>();
            }

            existing.BindField(field);
        }

        private static void ApplyBind(GroundFieldManagerSingleton field)
        {
            var system = EnsureGeometrySystem();
            system.Bind(field);
            GroundFieldGeometryHook.ResolveField = field != null ? () => field : null;
        }

        private static IGroundFieldGeometrySystem EnsureGeometrySystem()
        {
            var architecture = NineGridArchitecture.Interface;
            var existing = architecture.GetSystem<IGroundFieldGeometrySystem>();
            if (existing != null)
            {
                return existing;
            }

            var created = new GroundFieldGeometrySystem();
            architecture.RegisterSystem<IGroundFieldGeometrySystem>(created);
            return created;
        }

        private static IGroundFieldGeometrySystem TryGetGeometrySystem()
        {
            var architecture = NineGridArchitecture.Interface;
            return architecture?.GetSystem<IGroundFieldGeometrySystem>();
        }
    }
}
