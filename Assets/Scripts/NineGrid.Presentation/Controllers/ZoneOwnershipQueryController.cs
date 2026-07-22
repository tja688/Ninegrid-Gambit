using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Presentation.Queries;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Controllers
{
    /// <summary>
    /// 将 <see cref="CardZoneOwnershipHook"/> 接到区域归属 Query（Cards 生产路径只读桥）。
    /// </summary>
    public sealed class ZoneOwnershipQueryController : PresentationController
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterInstallHook()
        {
            EnsureWired();
        }

        protected override void OnBind()
        {
            EnsureWired();
        }

        protected override void OnUnbind()
        {
            // 静态 Hook 由下一局 EnsureWired / SubsystemRegistration 重置；此处不强制清空以免测试夹具误伤。
        }

        /// <summary>将 Hook 接到 Query（EditMode 与生产装配共用）。</summary>
        public static void EnsureWired()
        {
            CardZoneOwnershipHook.IsCoreItemSlots = QueryItemSlots;
            CardZoneOwnershipHook.IsCoreDrawPile = QueryDrawPile;
        }

        private static bool QueryItemSlots(int uid)
        {
            var arch = NineGridArchitecture.Interface;
            return arch != null && arch.SendQuery(new IsCardInItemSlotsQuery(uid));
        }

        private static bool QueryDrawPile(int uid)
        {
            var arch = NineGridArchitecture.Interface;
            return arch != null && arch.SendQuery(new IsCardInDrawPileQuery(uid));
        }
    }
}