#if UNITY_EDITOR || DEVELOPMENT_BUILD

using NineGrid.Core;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Cheat
{
    /// <summary>
    /// 作弊面板「无敌模式」：开启后每 3 秒回满血、+999 金币、攻击 +10。
    /// 由常驻 <see cref="CheatToolHotkeyHost"/> 驱动 Tick，面板关闭后仍生效。
    /// </summary>
    public static class CheatToolGodMode
    {
        private const float TickIntervalSeconds = 3f;
        private const int CoinsPerTick = 999;
        private const int AttackPerTick = 10;

        public static bool IsActive { get; private set; }

        private static float _nextTickUnscaledTime;

        public static void Toggle()
        {
            IsActive = !IsActive;
            if (IsActive)
            {
                ApplyTick();
                _nextTickUnscaledTime = Time.unscaledTime + TickIntervalSeconds;
                Debug.Log(
                    "[CheatTool] 无敌模式已开启（每"
                    + TickIntervalSeconds
                    + "秒回满血 / +"
                    + CoinsPerTick
                    + "金币 / 攻击+"
                    + AttackPerTick
                    + "）。");
                return;
            }

            Debug.Log("[CheatTool] 无敌模式已关闭。");
        }

        public static void Tick()
        {
            if (!IsActive || Time.unscaledTime < _nextTickUnscaledTime)
            {
                return;
            }

            ApplyTick();
            _nextTickUnscaledTime = Time.unscaledTime + TickIntervalSeconds;
        }

        private static void ApplyTick()
        {
            TryAddCoins();
            TryHealFull();
            TryAddAttack(AttackPerTick);
        }

        private static void TryAddCoins()
        {
            var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            var player = arch?.GetModel<PlayerModel>();
            if (arch == null || player == null)
            {
                return;
            }

            player.AddCoins(CoinsPerTick);
        }

        private static void TryHealFull()
        {
            var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            if (arch == null)
            {
                return;
            }

            var board = arch.GetModel<BoardModel>();
            var avatarUid = board != null ? board.AvatarUid.Value : 0;
            if (avatarUid <= 0
                || !arch.GetModel<CardRegistry>().TryGet(avatarUid, out var avatar))
            {
                return;
            }

            var maxHp = (int)avatar.Stats.GetBase(StatId.MaxHp);
            if (maxHp <= 0 || !BattleSessionCheat.TrySetAvatarHp(maxHp))
            {
                return;
            }

            PlayerInfoHudPresenter.TryGetInstance()?.SyncFromCore(animate: false);
        }

        private static void TryAddAttack(int delta)
        {
            if (delta <= 0)
            {
                return;
            }

            var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            if (arch == null)
            {
                return;
            }

            var board = arch.GetModel<BoardModel>();
            var avatarUid = board != null ? board.AvatarUid.Value : 0;
            if (avatarUid <= 0
                || !arch.GetModel<CardRegistry>().TryGet(avatarUid, out var avatar))
            {
                return;
            }

            var current = (int)avatar.Stats.GetBase(StatId.Attack);
            BattleSessionCheat.TrySetAvatarAttack(current + delta);
        }
    }
}

#endif
