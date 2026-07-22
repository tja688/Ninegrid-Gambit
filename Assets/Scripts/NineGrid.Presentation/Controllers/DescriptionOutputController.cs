using NineGrid.Cards;
using NineGrid.Flow.Presentation;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Controllers
{
    /// <summary>
    /// 描述输出 Controller：Cards Hook → QF Event；UI 只订阅事件。
    /// </summary>
    public sealed class DescriptionOutputController : PresentationController
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterInstallHook()
        {
            DescriptionDisplayHook.EnsureWired = () => EnsureInstalled();
            DescriptionDisplayHook.Show = null;
            DescriptionDisplayHook.ShowText = null;
            DescriptionDisplayHook.Clear = null;
        }

        /// <summary>场景或 EditMode 探针确保唯一 Controller 并接线 Hook。</summary>
        public static DescriptionOutputController EnsureInstalled()
        {
            var existing = UnityEngine.Object.FindObjectOfType<DescriptionOutputController>();
            if (existing != null)
            {
                existing.InstallHookHandlers();
                return existing;
            }

            var host = new GameObject(nameof(DescriptionOutputController));
            return host.AddComponent<DescriptionOutputController>();
        }

        protected override void OnBind()
        {
            InstallHookHandlers();
        }

        protected override void OnUnbind()
        {
            ClearHookHandlers();
        }

        private void InstallHookHandlers()
        {
            DescriptionDisplayHook.Show = HandleShow;
            DescriptionDisplayHook.ShowText = HandleShowText;
            DescriptionDisplayHook.Clear = HandleClear;
        }

        private void ClearHookHandlers()
        {
            if (DescriptionDisplayHook.Show == HandleShow)
            {
                DescriptionDisplayHook.Show = null;
            }

            if (DescriptionDisplayHook.ShowText == HandleShowText)
            {
                DescriptionDisplayHook.ShowText = null;
            }

            if (DescriptionDisplayHook.Clear == HandleClear)
            {
                DescriptionDisplayHook.Clear = null;
            }
        }

        private void HandleShow(string defId, DescriptionShowRoute route)
        {
            this.SendEvent(new DescriptionShowRequested
            {
                DefId = defId,
                Route = route
            });
        }

        private void HandleShowText(string text, DescriptionShowRoute route)
        {
            this.SendEvent(new DescriptionShowTextRequested
            {
                Text = text,
                Route = route
            });
        }

        private void HandleClear(DescriptionShowRoute route)
        {
            this.SendEvent(new DescriptionClearRequested
            {
                Route = route
            });
        }
    }
}
