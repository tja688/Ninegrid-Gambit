using System;
using System.Collections.Generic;
using System.Text;
using NineGrid.Core;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation;
using QFramework;
using UnityEngine;

namespace NineGrid.Flow.Diagnostics
{
    /// <summary>
    /// 输入门禁拒绝时的 Core/相位快照，便于 PerfLog/CoreLog 事后对齐「notLegal / CoreReject」根因。
    /// </summary>
    public static class BoardIntentGateDiagnostics
    {
        public static Dictionary<string, string> Collect(
            IArchitecture arch,
            GameCommandKind? probeCommand = null)
        {
            var fields = new Dictionary<string, string>();
            if (arch == null)
            {
                fields["arch"] = "null";
                return fields;
            }

            var phase = arch.GetSystem<IPhaseSystem>();
            var sync = arch.GetSystem<IPresentationSyncSystem>();
            var pending = arch.GetModel<PendingChoiceModel>();
            var board = arch.GetModel<BoardModel>();
            var player = arch.GetModel<PlayerModel>();
            var deck = arch.GetModel<DeckModel>();

            fields["phase"] = phase != null ? phase.CurrentPhase.ToString() : "?";
            fields["pendingChoice"] = pending != null ? pending.Kind.Value.ToString() : "?";
            fields["inputLocked"] = sync != null && sync.IsInputLocked ? "1" : "0";
            fields["activeBatchId"] = sync != null ? sync.ActiveBatchId.ToString() : "0";

            fields["choiceOverlay"] = PresentationInputGates.ChoiceOverlayActive ? "1" : "0";
            fields["mainlineBusy"] = PresentationInputGates.MainlineBusy ? "1" : "0";
            fields["boardSelect"] = PresentationInputGates.BoardSelectModeActive ? "1" : "0";
            fields["owner"] = PresentationInputGates.CurrentOwner.ToString();

            var walk = arch.GetSystem<IAvatarWalkSystem>();
            fields["avatarWalk"] = walk != null && walk.IsEnabled ? "1" : "0";

            var avatarUid = board != null ? board.AvatarUid.Value : 0;
            fields["avatarUid"] = avatarUid.ToString();
            fields["avatarSlot"] = board != null ? board.AvatarSlot.Value.ToString() : "?";
            fields["avatarDefeated"] = ComputeAvatarDefeated(arch, avatarUid) ? "1" : "0";

            if (avatarUid > 0)
            {
                CardInstance avatar;
                if (arch.GetModel<CardRegistry>().TryGet(avatarUid, out avatar) && avatar != null)
                {
                    fields["avatarZone"] = avatar.Zone.Value.ToString();
                    var statSystem = arch.GetSystem<IStatSystem>();
                    if (statSystem != null)
                    {
                        fields["hpBase"] = RoundHp(avatar.Stats.GetBase(StatId.Hp)).ToString();
                        fields["hpEff"] = statSystem.GetEffectiveInt(avatar, StatId.Hp).ToString();
                        fields["armorEff"] = statSystem.GetEffectiveInt(avatar, StatId.Armor).ToString();
                    }
                }
            }

            if (player != null && deck != null)
            {
                fields["itemSlots"] = deck.ItemSlotUids.Count + "/" + player.ItemSlotsCapacity;
                fields["itemSlotsFull"] = player.IsItemSlotsFull(deck) ? "1" : "0";
            }

            if (phase != null)
            {
                fields["legal"] = FormatLegalCommands(phase.LegalCommands);
                fields["canExecAttack"] = phase.CanExecute(GameCommandKind.Attack) ? "1" : "0";
                fields["canExecUseItem"] = phase.CanExecute(GameCommandKind.UseItem) ? "1" : "0";
                fields["canExecPickup"] = phase.CanExecute(GameCommandKind.PickupItem) ? "1" : "0";
                fields["canExecRecycle"] = phase.CanExecute(GameCommandKind.RecycleItemSlot) ? "1" : "0";

                if (probeCommand.HasValue)
                {
                    fields["canExecProbe"] = phase.CanExecute(probeCommand.Value) ? "1" : "0";
                    fields["probeCmd"] = probeCommand.Value.ToString();
                }
            }

            return fields;
        }

        public static string FormatReasonSuffix(IReadOnlyDictionary<string, string> fields)
        {
            if (fields == null || fields.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder(256);
            AppendField(sb, fields, "phase");
            AppendField(sb, fields, "pendingChoice");
            AppendField(sb, fields, "avatarDefeated");
            AppendField(sb, fields, "avatarUid");
            AppendField(sb, fields, "avatarZone");
            AppendField(sb, fields, "hpBase");
            AppendField(sb, fields, "hpEff");
            AppendField(sb, fields, "inputLocked");
            AppendField(sb, fields, "activeBatchId");
            AppendField(sb, fields, "choiceOverlay");
            AppendField(sb, fields, "mainlineBusy");
            AppendField(sb, fields, "boardSelect");
            AppendField(sb, fields, "owner");
            AppendField(sb, fields, "avatarWalk");
            AppendField(sb, fields, "itemSlots");
            AppendField(sb, fields, "itemSlotsFull");
            AppendField(sb, fields, "canExecProbe");
            AppendField(sb, fields, "probeCmd");
            AppendField(sb, fields, "canExecAttack");
            AppendField(sb, fields, "canExecUseItem");
            AppendField(sb, fields, "canExecPickup");
            AppendField(sb, fields, "canExecRecycle");
            AppendField(sb, fields, "legal");
            return sb.ToString();
        }

        public static void MergeInto(
            IDictionary<string, string> target,
            IReadOnlyDictionary<string, string> fields)
        {
            if (target == null || fields == null)
            {
                return;
            }

            foreach (var pair in fields)
            {
                target[pair.Key] = pair.Value;
            }
        }

        public static void AppendPhaseReject(
            ref string rejectReason,
            IArchitecture arch,
            GameCommandKind command)
        {
            var fields = Collect(arch, command);
            var suffix = FormatReasonSuffix(fields);
            if (string.IsNullOrEmpty(suffix))
            {
                return;
            }

            rejectReason = string.IsNullOrEmpty(rejectReason)
                ? suffix
                : rejectReason + " | " + suffix;
        }

        public static void LogConsole(
            string tag,
            string detail,
            IArchitecture arch = null,
            GameCommandKind? probeCommand = null)
        {
            arch = arch ?? NineGridArchitecture.Interface;
            var fields = Collect(arch, probeCommand);
            var suffix = FormatReasonSuffix(fields);
            Debug.LogWarning(
                "[IntentGate] " + (tag ?? "?")
                + " " + (detail ?? string.Empty)
                + (string.IsNullOrEmpty(suffix) ? string.Empty : " | " + suffix));
        }

        public static GameCommandKind? MapIntentKindToCommand(string intentKind)
        {
            if (string.IsNullOrEmpty(intentKind))
            {
                return null;
            }

            if (string.Equals(intentKind, InputIntentKinds.Attack, StringComparison.Ordinal))
            {
                return GameCommandKind.Attack;
            }

            if (string.Equals(intentKind, InputIntentKinds.UseItem, StringComparison.Ordinal))
            {
                return GameCommandKind.UseItem;
            }

            if (string.Equals(intentKind, InputIntentKinds.Pickup, StringComparison.Ordinal))
            {
                return GameCommandKind.PickupItem;
            }

            if (string.Equals(intentKind, InputIntentKinds.RecycleItem, StringComparison.Ordinal))
            {
                return GameCommandKind.RecycleItemSlot;
            }

            if (string.Equals(intentKind, InputIntentKinds.Explore, StringComparison.Ordinal)
                || string.Equals(intentKind, InputIntentKinds.BoardWalk, StringComparison.Ordinal))
            {
                return GameCommandKind.ClickEmpty;
            }

            if (string.Equals(intentKind, InputIntentKinds.RevealFace, StringComparison.Ordinal))
            {
                return GameCommandKind.RevealFace;
            }

            return null;
        }

        private static void AppendField(
            StringBuilder sb,
            IReadOnlyDictionary<string, string> fields,
            string key)
        {
            string value;
            if (!fields.TryGetValue(key, out value) || string.IsNullOrEmpty(value))
            {
                return;
            }

            if (sb.Length > 0)
            {
                sb.Append(' ');
            }

            sb.Append(key).Append('=').Append(value);
        }

        private static string FormatLegalCommands(IReadOnlyList<GameCommandKind> legal)
        {
            if (legal == null || legal.Count == 0)
            {
                return "(none)";
            }

            var sb = new StringBuilder(legal.Count * 8);
            for (var i = 0; i < legal.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append(legal[i]);
            }

            return sb.ToString();
        }

        private static bool ComputeAvatarDefeated(IArchitecture arch, int avatarUid)
        {
            // 与 Core 判死谓词对齐（ADR-0039：uid 缺失 / 未注册 / HP≤0，不看 Zone）；
            // zone 异常单独经 avatarZone 字段暴露，便于定位置死来源。
            if (arch == null || avatarUid <= 0)
            {
                return true;
            }

            CardInstance avatar;
            if (!arch.GetModel<CardRegistry>().TryGet(avatarUid, out avatar) || avatar == null)
            {
                return true;
            }

            return RoundHp(avatar.Stats.GetBase(StatId.Hp)) <= 0;
        }

        private static int RoundHp(float hp)
        {
            return (int)Math.Round(hp);
        }
    }
}
