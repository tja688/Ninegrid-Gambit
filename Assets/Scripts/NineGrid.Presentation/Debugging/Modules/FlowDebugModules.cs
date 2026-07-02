using System;
using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Presentation.Debugging;
using NineGrid.Presentation.Flow.Battle;
using NineGrid.Presentation.Flow.Board;
using NineGrid.Presentation.Flow.Deck;
using NineGrid.Presentation.Flow.Item;
using NineGrid.Presentation.Orchestration;
using NineGrid.Presentation.Shell;
using NineGrid.Presentation.Shared;
using UnityEngine;

namespace NineGrid.Presentation.Debugging.Modules
{
    internal static class PerformanceDebugSchemaFactory
    {
        public static readonly string[] BoardSlotOptions = { "1", "2", "3", "4", "5", "6", "7", "8", "9" };
        public static readonly string[] BoardSlotOptionsWithAuto = { "Auto", "1", "2", "3", "4", "5", "6", "7", "8", "9" };

        public static PerformanceDebugSchema BattleEventSchema(
            PerformanceDebugContextPreset preset = PerformanceDebugContextPreset.BattlePair)
        {
            return CreateSchema()
                .Add(PerformanceDebugPayloadKeys.ContextPreset, "Context", PerformanceDebugParamKind.ContextPreset,
                    preset.ToString(), EnumNames<PerformanceDebugContextPreset>())
                .Add(PerformanceDebugPayloadKeys.ActorUid, "Actor Uid", PerformanceDebugParamKind.Int, "1")
                .Add(PerformanceDebugPayloadKeys.TargetUid, "Target Uid", PerformanceDebugParamKind.Int, "2")
                .Add(PerformanceDebugPayloadKeys.PlayerSlot, "Player Slot", PerformanceDebugParamKind.Enum, "5", BoardSlotOptions)
                .Add(PerformanceDebugPayloadKeys.TargetSlot, "Target Slot", PerformanceDebugParamKind.Enum, "Auto", BoardSlotOptionsWithAuto)
                .Add(PerformanceDebugPayloadKeys.Direction, "Direction", PerformanceDebugParamKind.Enum,
                    CardBattleDirection.Right.ToString(), "Right", "Up", "Left", "Down")
                .Add(PerformanceDebugPayloadKeys.Amount, "Amount", PerformanceDebugParamKind.Int, "3")
                .Add(PerformanceDebugPayloadKeys.ForceDirectionOverride, "Force Dir Override", PerformanceDebugParamKind.Bool, "false")
                .Add("derivedDirection", "Derived Direction", PerformanceDebugParamKind.Derived, CardBattleDirection.Right.ToString());
        }

        public static PerformanceDebugSchema BattleSchema() => BattleEventSchema();

        public static PerformanceDebugSchema MoveCardSchema()
        {
            return CreateSchema()
                .Add(PerformanceDebugPayloadKeys.ContextPreset, "Context", PerformanceDebugParamKind.ContextPreset,
                    PerformanceDebugContextPreset.Board9.ToString(), EnumNames<PerformanceDebugContextPreset>())
                .Add(PerformanceDebugPayloadKeys.FromSlot, "From Slot", PerformanceDebugParamKind.Enum, "1", BoardSlotOptions)
                .Add(PerformanceDebugPayloadKeys.ToSlot, "To Slot", PerformanceDebugParamKind.Enum, "2", BoardSlotOptions);
        }

        public static PerformanceDebugSchema BoardRotateSchema()
        {
            return CreateSchema()
                .Add(PerformanceDebugPayloadKeys.ContextPreset, "Context", PerformanceDebugParamKind.ContextPreset,
                    PerformanceDebugContextPreset.Board9.ToString(), EnumNames<PerformanceDebugContextPreset>())
                .Add(PerformanceDebugPayloadKeys.RotateAmount, "Rotate Amount", PerformanceDebugParamKind.Int, "1");
        }

        public static PerformanceDebugSchema SelectionConfirmSchema()
        {
            return CreateSchema()
                .Add(PerformanceDebugPayloadKeys.ContextPreset, "Context", PerformanceDebugParamKind.ContextPreset,
                    PerformanceDebugContextPreset.Selection3.ToString(), EnumNames<PerformanceDebugContextPreset>())
                .Add(PerformanceDebugPayloadKeys.SelectedIndex, "Selected Index", PerformanceDebugParamKind.Int, "1")
                .Add(PerformanceDebugPayloadKeys.Lift, "Lift", PerformanceDebugParamKind.Int, "10")
                .Add(PerformanceDebugPayloadKeys.Extra, "Extra", PerformanceDebugParamKind.Int, "3");
        }

        public static PerformanceDebugSchema ContextOnlySchema(PerformanceDebugContextPreset preset)
        {
            return CreateSchema()
                .Add("contextPreset", "Context", PerformanceDebugParamKind.ContextPreset, preset.ToString(), EnumNames<PerformanceDebugContextPreset>());
        }

        public static string[] EnumNames<T>() where T : Enum
        {
            return Enum.GetNames(typeof(T));
        }

        private static PerformanceDebugSchema CreateSchema() => new PerformanceDebugSchema();

        private static string DefaultContextField() => PerformanceDebugContextPreset.BattlePair.ToString();
    }

    internal static class FlowBindingPlayHelper
    {
        public static PerformanceDebugPlayResult PlayFlow(
            PerformanceDebugContext context,
            FlowId flowId,
            FlowPayload payload)
        {
            IFlowBinding binding = context?.Harness?.GetFlowBinding(flowId);
            if (binding == null)
            {
                return PerformanceDebugPlayResult.Fail($"Missing flow binding: {flowId}");
            }

            FlowHandle handle = binding.Play(context.Registry, payload);
            return PerformanceDebugPlayResult.Ok(handle.ExpectedDuration);
        }
    }

    public sealed class CardAttackDebugModule : PerformanceDebugModuleBase<CardAttackFlow>
    {
        public override string Id => "flow.card-attack";
        public override string DisplayName => "攻击";
        public override PerformanceDebugCategory Category => PerformanceDebugCategory.Flow;
        public override PerformanceDebugSchema Schema => PerformanceDebugSchemaFactory.BattleSchema();

        protected override PerformanceDebugPlayResult PlayModule(PerformanceDebugContext context, CardAttackFlow module, PerformanceDebugPayload payload)
        {
            return FlowBindingPlayHelper.PlayFlow(
                context,
                FlowId.CardAttack,
                PerformanceDebugFlowPayloadBuilder.BuildBattleFlowPayload(payload));
        }
    }

    public sealed class CounterattackDebugModule : PerformanceDebugModuleBase<CounterattackFlow>
    {
        public override string Id => "flow.counterattack";
        public override string DisplayName => "反击";
        public override PerformanceDebugCategory Category => PerformanceDebugCategory.Flow;
        public override PerformanceDebugSchema Schema => PerformanceDebugSchemaFactory.BattleSchema();

        protected override PerformanceDebugPlayResult PlayModule(PerformanceDebugContext context, CounterattackFlow module, PerformanceDebugPayload payload)
        {
            return FlowBindingPlayHelper.PlayFlow(
                context,
                FlowId.Counterattack,
                PerformanceDebugFlowPayloadBuilder.BuildBattleFlowPayload(payload));
        }
    }

    public sealed class CardKillDebugModule : PerformanceDebugModuleBase<CardKillFlow>
    {
        public override string Id => "flow.card-kill";
        public override string DisplayName => "击杀";
        public override PerformanceDebugCategory Category => PerformanceDebugCategory.Flow;
        public override PerformanceDebugSchema Schema => PerformanceDebugSchemaFactory.BattleSchema();

        protected override PerformanceDebugPlayResult PlayModule(PerformanceDebugContext context, CardKillFlow module, PerformanceDebugPayload payload)
        {
            return FlowBindingPlayHelper.PlayFlow(
                context,
                FlowId.CardKill,
                PerformanceDebugFlowPayloadBuilder.BuildBattleFlowPayload(payload));
        }
    }

    public sealed class CounterattackKillDebugModule : PerformanceDebugModuleBase<CounterattackKillFlow>
    {
        public override string Id => "flow.counterattack-kill";
        public override string DisplayName => "反击击杀";
        public override PerformanceDebugCategory Category => PerformanceDebugCategory.Flow;
        public override PerformanceDebugSchema Schema => PerformanceDebugSchemaFactory.BattleSchema();

        protected override PerformanceDebugPlayResult PlayModule(PerformanceDebugContext context, CounterattackKillFlow module, PerformanceDebugPayload payload)
        {
            return FlowBindingPlayHelper.PlayFlow(
                context,
                FlowId.CounterattackKill,
                PerformanceDebugFlowPayloadBuilder.BuildBattleFlowPayload(payload));
        }
    }

    public sealed class BoardRotateDebugModule : PerformanceDebugModuleBase<BoardRotateFlow>
    {
        public override string Id => "flow.board-rotate";
        public override string DisplayName => "棋盘旋转";
        public override PerformanceDebugCategory Category => PerformanceDebugCategory.Flow;
        public override PerformanceDebugSchema Schema => PerformanceDebugSchemaFactory.BoardRotateSchema();

        protected override PerformanceDebugPlayResult PlayModule(PerformanceDebugContext context, BoardRotateFlow module, PerformanceDebugPayload payload)
        {
            int amount = payload.GetInt(PerformanceDebugPayloadKeys.RotateAmount, 1);
            return FlowBindingPlayHelper.PlayFlow(context, FlowId.BoardRotate, new FlowPayload { Amount = amount });
        }
    }

    public sealed class CardDeckEntryDebugModule : PerformanceDebugModuleBase<CardDeckEntryFlow>
    {
        public override string Id => "flow.deck-entry";
        public override string DisplayName => "牌堆入场";
        public override PerformanceDebugCategory Category => PerformanceDebugCategory.Flow;
        public override PerformanceDebugSchema Schema => PerformanceDebugSchemaFactory.ContextOnlySchema(PerformanceDebugContextPreset.Deck20);

        protected override PerformanceDebugPlayResult PlayModule(PerformanceDebugContext context, CardDeckEntryFlow module, PerformanceDebugPayload payload)
        {
            return FlowBindingPlayHelper.PlayFlow(context, FlowId.CardDeckEntry, new FlowPayload());
        }
    }

    public sealed class CardDeckDealDebugModule : PerformanceDebugModuleBase<CardDeckDealFlow>
    {
        public override string Id => "flow.deck-deal";
        public override string DisplayName => "发牌";
        public override PerformanceDebugCategory Category => PerformanceDebugCategory.Flow;
        public override PerformanceDebugSchema Schema =>
            PerformanceDebugSchemaFactory.ContextOnlySchema(PerformanceDebugContextPreset.None);

        protected override PerformanceDebugPlayResult PlayModule(PerformanceDebugContext context, CardDeckDealFlow module, PerformanceDebugPayload payload)
        {
            return FlowBindingPlayHelper.PlayFlow(context, FlowId.CardDeal, new FlowPayload());
        }
    }

    public sealed class CardDeckSubstituteDebugModule : PerformanceDebugModuleBase<CardDeckSubstituteFlow>
    {
        public override string Id => "flow.deck-substitute";
        public override string DisplayName => "替换";
        public override PerformanceDebugCategory Category => PerformanceDebugCategory.Flow;
        public override PerformanceDebugSchema Schema => PerformanceDebugSchemaFactory.MoveCardSchema();

        protected override PerformanceDebugPlayResult PlayModule(PerformanceDebugContext context, CardDeckSubstituteFlow module, PerformanceDebugPayload payload)
        {
            int fromSlot = payload.GetBoardSlot(PerformanceDebugPayloadKeys.FromSlot, 1);
            int toSlot = payload.GetBoardSlot(PerformanceDebugPayloadKeys.ToSlot, 2);
            return FlowBindingPlayHelper.PlayFlow(context, FlowId.MoveCard, new FlowPayload
            {
                CardUid = PerformanceDebugActorUids.BoardCard(fromSlot),
                FromSlot = SlotId.Board(fromSlot),
                ToSlot = SlotId.Board(toSlot),
            });
        }
    }

    public sealed class SelectionEntranceDebugModule : PerformanceDebugModuleBase<SelectionPresentation>
    {
        public override string Id => "flow.selection-entrance";
        public override string DisplayName => "选择入场";
        public override PerformanceDebugCategory Category => PerformanceDebugCategory.Flow;
        public override PerformanceDebugSchema Schema => PerformanceDebugSchemaFactory.ContextOnlySchema(PerformanceDebugContextPreset.Selection3);

        protected override PerformanceDebugPlayResult PlayModule(PerformanceDebugContext context, SelectionPresentation module, PerformanceDebugPayload payload)
        {
            var options = new List<Transform>
            {
                context.ResolveActor("option1"),
                context.ResolveActor("option2"),
                context.ResolveActor("option3"),
            };
            module.PlayGeneralEntrance(options);
            return PerformanceDebugPlayResult.Ok(module.ExpectedDuration);
        }
    }

    public sealed class SelectionConfirmDebugModule : PerformanceDebugModuleBase<SelectionPresentation>
    {
        public override string Id => "flow.selection-confirm";
        public override string DisplayName => "选择确认";
        public override PerformanceDebugCategory Category => PerformanceDebugCategory.Flow;
        public override PerformanceDebugSchema Schema => PerformanceDebugSchemaFactory.SelectionConfirmSchema();

        protected override PerformanceDebugPlayResult PlayModule(PerformanceDebugContext context, SelectionPresentation module, PerformanceDebugPayload payload)
        {
            int selectedIndex = payload.GetInt(PerformanceDebugPayloadKeys.SelectedIndex, 1);
            string actorId = $"option{Mathf.Clamp(selectedIndex + 1, 1, 3)}";
            Transform selected = context.ResolveActor(actorId);
            if (selected == null)
            {
                return PerformanceDebugPlayResult.Fail("Missing selected option actor.");
            }

            module.PlayGeneralConfirm(
                selected,
                null,
                payload.GetInt(PerformanceDebugPayloadKeys.Lift, 10),
                payload.GetInt(PerformanceDebugPayloadKeys.Extra, 3));
            return PerformanceDebugPlayResult.Ok(1.12f);
        }
    }

    public sealed class ItemUseDebugModule : PerformanceDebugModuleBase<ItemUseFlow>
    {
        public override string Id => "flow.item-use";
        public override string DisplayName => "道具使用";
        public override PerformanceDebugCategory Category => PerformanceDebugCategory.Flow;
        public override PerformanceDebugSchema Schema => CreateSchema()
            .Add("itemCardUid", "Item Card Uid", PerformanceDebugParamKind.Int, "1");

        protected override PerformanceDebugPlayResult PlayModule(PerformanceDebugContext context, ItemUseFlow module, PerformanceDebugPayload payload)
        {
            var flowPayload = new FlowPayload
            {
                CardUid = payload.GetInt("itemCardUid", PerformanceDebugActorUids.Player),
            };
            var result = FlowBindingPlayHelper.PlayFlow(context, FlowId.UseItem, flowPayload);
            context.Log.Info("ItemUseFlow is a placeholder (0s).");
            return result;
        }
    }
}
