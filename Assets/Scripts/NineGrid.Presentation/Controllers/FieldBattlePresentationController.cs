using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Controllers
{
    /// <summary>
    /// 场地战斗表现 Controller：将场景 Battle 登记到 QF System，并接线 Hook。
    /// </summary>
    public sealed class FieldBattlePresentationController : PresentationController
    {
        private FieldBattleManagerSingleton mBattle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterInstallHook()
        {
            FieldBattlePresentationHook.Wire = WireBattle;
        }

        protected override void OnBind()
        {
            if (mBattle != null)
            {
                ApplyBind(mBattle);
            }
        }

        protected override void OnUnbind()
        {
            ClearBind();
        }

        /// <summary>绑定 Battle（Hook 与 EditMode 直驱共用）。</summary>
        public void BindBattle(FieldBattleManagerSingleton battle)
        {
            mBattle = battle;
            ApplyBind(battle);
        }

        private void ClearBind()
        {
            var system = TryGetBattleSystem();
            if (system != null && ReferenceEquals(system.Battle, mBattle))
            {
                system.Unbind();
            }

            if (ReferenceEquals(FieldBattlePresentationHook.ResolveBattle?.Invoke(), mBattle))
            {
                FieldBattlePresentationHook.ResolveBattle = null;
            }

            mBattle = null;
        }

        private static void WireBattle(FieldBattleManagerSingleton battle)
        {
            var existing = Object.FindObjectOfType<FieldBattlePresentationController>();
            if (existing == null)
            {
                var host = new GameObject(nameof(FieldBattlePresentationController));
                existing = host.AddComponent<FieldBattlePresentationController>();
            }

            existing.BindBattle(battle);
        }

        private static void ApplyBind(FieldBattleManagerSingleton battle)
        {
            var system = EnsureBattleSystem();
            system.Bind(battle);
            FieldBattlePresentationHook.ResolveBattle = battle != null ? () => battle : null;
        }

        private static IFieldBattlePresentationSystem EnsureBattleSystem()
        {
            var architecture = NineGridArchitecture.Interface;
            var existing = architecture.GetSystem<IFieldBattlePresentationSystem>();
            if (existing != null)
            {
                return existing;
            }

            var created = new FieldBattlePresentationSystem();
            architecture.RegisterSystem<IFieldBattlePresentationSystem>(created);
            return created;
        }

        private static IFieldBattlePresentationSystem TryGetBattleSystem()
        {
            var architecture = NineGridArchitecture.Interface;
            return architecture?.GetSystem<IFieldBattlePresentationSystem>();
        }
    }
}
