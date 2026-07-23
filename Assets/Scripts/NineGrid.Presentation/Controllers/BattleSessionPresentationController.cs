using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Controllers
{
    /// <summary>
    /// 局内会话 Controller：将场景 View 登记到 QF System。
    /// </summary>
    public sealed class BattleSessionPresentationController : PresentationController
    {
        private BattleSessionController mSession;

        protected override void OnBind()
        {
            if (mSession != null)
            {
                ApplyBind(mSession);
            }
        }

        protected override void OnUnbind()
        {
            ClearBind();
        }

        public void BindSession(BattleSessionController session)
        {
            mSession = session;
            ApplyBind(session);
        }

        private void ClearBind()
        {
            var system = TryGetSessionSystem();
            system?.UnbindIfView(mSession);
            mSession = null;
        }

        public static void WireSession(BattleSessionController session)
        {
            var existing = Object.FindObjectOfType<BattleSessionPresentationController>();
            if (existing == null)
            {
                var host = new GameObject(nameof(BattleSessionPresentationController));
                existing = host.AddComponent<BattleSessionPresentationController>();
            }

            existing.BindSession(session);
        }

        private static void ApplyBind(BattleSessionController session)
        {
            var system = BattleSessionSystem.EnsureRegistered();
            system.Bind(session);
        }

        private static IBattleSessionSystem TryGetSessionSystem()
        {
            var architecture = NineGridArchitecture.Interface;
            return architecture?.GetSystem<IBattleSessionSystem>();
        }
    }
}
