using NineGrid.Cards;
using NineGrid.Presentation.Commands;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Controllers
{
    /// <summary>
    /// 伤害飘字 Controller：Cards Hook → Command → Event；FX 只订阅事件。
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
            DamageNumberHook.EnsureWired = () => EnsureInstalled();

            var existing = UnityEngine.Object.FindObjectOfType<DamageNumberOutputController>();
            if (existing != null)
            {
                existing.InstallHookHandlers();
                return existing;
            }

            var host = new GameObject(nameof(DamageNumberOutputController));
            var created = host.AddComponent<DamageNumberOutputController>();
            // EditMode 下 AddComponent 不一定触发 Awake，显式接线。
            created.InstallHookHandlers();
            return created;
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
            this.SendCommand(new RequestDamageNumberCommand(worldPosition, amount));
        }
    }
}
