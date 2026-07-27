using NineGrid.Cards;
using UnityEngine;

namespace NineGrid.Presentation.Controllers
{
    /// <summary>
    /// 描述输出 Controller（已退役）：不再 Hook→Command→Event 写动态 HUD TMP。
    /// 场景装配可保留空壳；卡面静态描述权威在 Basic_Description Commit。
    /// </summary>
    public sealed class DescriptionOutputController : PresentationController
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterInstallHook()
        {
            DescriptionDisplayHook.EnsureWired = null;
            DescriptionDisplayHook.Show = null;
            DescriptionDisplayHook.ShowText = null;
            DescriptionDisplayHook.Clear = null;
        }

        /// <summary>退役：不再 FindObjectOfType / 接线；返回 null。</summary>
        public static DescriptionOutputController EnsureInstalled()
        {
            RegisterInstallHook();
            return null;
        }

        protected override void OnBind()
        {
        }

        protected override void OnUnbind()
        {
        }
    }
}
