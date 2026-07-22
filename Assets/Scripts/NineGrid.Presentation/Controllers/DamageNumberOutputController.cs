using NineGrid.Cards;
using NineGrid.Flow.Presentation;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Controllers
{
    /// <summary>
    /// 伤害飘字 Controller：Cards Hook → QF Event；FX 只订阅事件。
    /// </summary>
    public sealed class DamageNumberOutputController : PresentationController
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterInstallHook()
        {
            DamageNumberHook.EnsureWired = () => EnsureInstalled();
            DamageNumberHook.Spawn = null;
        }

        public static DamageNumberOutputController EnsureInstalled()
        {
            var existing = UnityEngine.Object.FindObjectOfType<DamageNumberOutputController>();
            if (existing != null)
            {
                existing.InstallHookHandlers();
                return existing;
            }

            var host = new GameObject(nameof(DamageNumberOutputController));
            return host.AddComponent<DamageNumberOutputController>();
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
            DamageNumberHook.Spawn = HandleSpawn;
        }

        private void ClearHookHandlers()
        {
            if (DamageNumberHook.Spawn == HandleSpawn)
            {
                DamageNumberHook.Spawn = null;
            }
        }

        private void HandleSpawn(Vector3 worldPosition, int amount)
        {
            this.SendEvent(new DamageNumberRequested
            {
                WorldPosition = worldPosition,
                Amount = amount
            });
        }
    }
}
