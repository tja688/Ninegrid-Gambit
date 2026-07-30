using System;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Presentation.Commands;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Controllers
{
    /// <summary>
    /// 怪物格攻击输入 Controller：经 <see cref="AttackInputHook"/> 承接场地交战点击，
    /// 发 QF Command（门禁由 IntentIntake 裁决）。
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

        /// <summary>怪物格点击入口（Hook 与 EditMode 直驱共用）。背面卡改走主动翻开。</summary>
        public bool HandleMonsterSlotClicked(int groundSlot)
        {
            if (TrySubmitRevealFaceIfNeeded(groundSlot))
            {
                return true;
            }

            return this.SendCommand(new SubmitAttackIntentCommand(groundSlot));
        }

        private bool TrySubmitRevealFaceIfNeeded(int groundSlot)
        {
            var arch = NineGridArchitecture.Interface;
            if (arch == null)
            {
                return false;
            }

            var board = arch.GetModel<BoardModel>();
            if (board == null)
            {
                return false;
            }

            var slot = SlotId.Board(groundSlot);
            var uid = board.GetCardUid(slot);
            if (uid <= 0)
            {
                return false;
            }

            var registry = arch.GetModel<CardRegistry>();
            if (!registry.TryGet(uid, out var card) || card == null || card.FaceUp)
            {
                return false;
            }

            return this.SendCommand(new SubmitRevealFaceIntentCommand(groundSlot));
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

        private static void WireToBattle(FieldBattleView battle)
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
