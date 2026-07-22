using System;

namespace NineGrid.Cards
{
    /// <summary>
    /// 交战/盘面结算桥：由场景组合根或 InBattle 宿主显式注册，替代 CombatHitSink 业务静态委托。
    /// 门禁标志仍暂留 CombatHitSink（A1 后只读投影化）。
    /// </summary>
    public static class CombatHitBridgeHook
    {
        public static Func<int, int, CombatHitPresentationResult> ApplyCombatHit;
        public static Func<PostKillBoardPresentationResult> ResolvePostKillBoard;
        public static Action<ManagedCard> SyncCardPresentation;
        public static Action SyncBoardFromCore;
        public static Action<bool> NotifyBattleEnded;
        public static Action NotifyNodeSettlementReady;

        public static Func<string, bool> BeginDirectorExternalHold;
        public static Action<string> EndDirectorExternalHold;
        public static Action<string> ForceEndDirectorExternalHold;

        public static void Reset()
        {
            ApplyCombatHit = null;
            ResolvePostKillBoard = null;
            SyncCardPresentation = null;
            SyncBoardFromCore = null;
            NotifyBattleEnded = null;
            NotifyNodeSettlementReady = null;
            BeginDirectorExternalHold = null;
            EndDirectorExternalHold = null;
            ForceEndDirectorExternalHold = null;
        }

        public static CombatHitPresentationResult RequestCombatHit(int attackerUid, int targetUid)
        {
            if (ApplyCombatHit == null)
            {
                UnityEngine.Debug.LogWarning("[CombatHitBridgeHook] ApplyCombatHit 未注册。");
                return default;
            }

            return ApplyCombatHit(attackerUid, targetUid);
        }

        public static PostKillBoardPresentationResult RequestPostKillBoard()
        {
            if (ResolvePostKillBoard == null)
            {
                UnityEngine.Debug.LogWarning("[CombatHitBridgeHook] ResolvePostKillBoard 未注册。");
                return default;
            }

            return ResolvePostKillBoard();
        }

        public static void RequestSyncCard(ManagedCard card)
        {
            SyncCardPresentation?.Invoke(card);
        }

        public static void RequestSyncBoardFromCore()
        {
            SyncBoardFromCore?.Invoke();
        }

        public static void RequestBattleEnded(bool victory)
        {
            NotifyBattleEnded?.Invoke(victory);
        }

        public static void RequestNodeSettlement()
        {
            NotifyNodeSettlementReady?.Invoke();
        }
    }
}
