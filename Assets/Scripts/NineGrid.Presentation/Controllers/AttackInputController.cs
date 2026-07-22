using System;
using NineGrid.Cards;
using NineGrid.Presentation.Commands;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Controllers
{
    /// <summary>
    /// 怪物格攻击输入 Controller：经 <see cref="AttackInputHook"/> 承接场地交战点击，发 QF Command。
    /// </summary>
    public sealed class AttackInputController : PresentationController
    {
        private Func<int, bool> mSubmitHandler;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterInstallHook()
        {
            AttackInputHook.WireController = WireToBattle;
        }

        protected override void OnBind()
        {
            InstallSubmitHandler();
        }

        protected override void OnUnbind()
        {
            ClearSubmitHandler();
        }

        /// <summary>怪物格点击入口（Hook 与 EditMode 直驱共用）。</summary>
        public bool HandleMonsterSlotClicked(int groundSlot)
        {
            return this.SendCommand(new SubmitAttackIntentCommand(groundSlot));
        }

        private void InstallSubmitHandler()
        {
            mSubmitHandler = HandleMonsterSlotClicked;
            AttackInputHook.TrySubmitAttack = mSubmitHandler;
        }

        private void ClearSubmitHandler()
        {
            if (mSubmitHandler != null && AttackInputHook.TrySubmitAttack == mSubmitHandler)
            {
                AttackInputHook.TrySubmitAttack = null;
            }

            mSubmitHandler = null;
        }

        private static void WireToBattle(FieldBattleManagerSingleton battle)
        {
            if (battle == null)
            {
                return;
            }

            var existing = battle.GetComponent<AttackInputController>();
            if (existing == null)
            {
                existing = UnityEngine.Object.FindObjectOfType<AttackInputController>();
            }

            if (existing == null)
            {
                var host = new GameObject(nameof(AttackInputController));
                host.transform.SetParent(battle.transform, false);
                existing = host.AddComponent<AttackInputController>();
            }

            existing.InstallSubmitHandler();
        }
    }
}
