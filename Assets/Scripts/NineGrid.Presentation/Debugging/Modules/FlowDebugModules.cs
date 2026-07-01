using System;
using System.Collections.Generic;
using NineGrid.Presentation.Flow.Battle;
using NineGrid.Presentation.Flow.Board;
using NineGrid.Presentation.Flow.Deck;
using NineGrid.Presentation.Flow.Item;
using NineGrid.Presentation.Flow.Selection;
using NineGrid.Presentation.Orchestration;
using NineGrid.Presentation.Shared;
using UnityEngine;

namespace NineGrid.Presentation.Debugging.Modules
{
    internal static class PerformanceDebugSchemaFactory
    {
        public static PerformanceDebugSchema BattleSchema()
        {
            return CreateSchema()
                .Add("contextPreset", "Context", PerformanceDebugParamKind.ContextPreset, DefaultContextField(), EnumNames<PerformanceDebugContextPreset>())
                .Add("direction", "Direction", PerformanceDebugParamKind.Enum, CardBattleDirection.Right.ToString(),
                    "Right", "Up", "Left", "Down");
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
            var flowPayload = new FlowPayload
            {
                ActorUid = PerformanceDebugActorUids.Player,
                TargetUid = PerformanceDebugActorUids.Enemy,
                Direction = CardBattleDirectionUtil.ToVector2(payload.GetDirection("direction")),
            };
            return FlowBindingPlayHelper.PlayFlow(context, FlowId.CardAttack, flowPayload);
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
            Transform player = context.ResolveActor("player");
            Transform enemy = context.ResolveActor("enemy");
            if (player == null || enemy == null)
            {
                return PerformanceDebugPlayResult.Fail("Missing player/enemy actors.");
            }

            module.Play(enemy, player, payload.GetDirection("direction"));
            return PerformanceDebugPlayResult.Ok(module.ExpectedDuration);
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
            var flowPayload = new FlowPayload
            {
                ActorUid = PerformanceDebugActorUids.Player,
                CardUid = PerformanceDebugActorUids.Enemy,
                Direction = CardBattleDirectionUtil.ToVector2(payload.GetDirection("direction")),
            };
            return FlowBindingPlayHelper.PlayFlow(context, FlowId.CardKill, flowPayload);
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
            Transform player = context.ResolveActor("player");
            Transform enemy = context.ResolveActor("enemy");
            if (player == null || enemy == null)
            {
                return PerformanceDebugPlayResult.Fail("Missing player/enemy actors.");
            }

            module.Play(enemy, player, payload.GetDirection("direction"));
            return PerformanceDebugPlayResult.Ok(module.ExpectedDuration);
        }
    }

    public sealed class BoardRotateDebugModule : PerformanceDebugModuleBase<BoardRotateFlow>
    {
        public override string Id => "flow.board-rotate";
        public override string DisplayName => "棋盘旋转";
        public override PerformanceDebugCategory Category => PerformanceDebugCategory.Flow;
        public override PerformanceDebugSchema Schema => PerformanceDebugSchemaFactory.ContextOnlySchema(PerformanceDebugContextPreset.Board9);

        protected override PerformanceDebugPlayResult PlayModule(PerformanceDebugContext context, BoardRotateFlow module, PerformanceDebugPayload payload)
        {
            return FlowBindingPlayHelper.PlayFlow(context, FlowId.BoardRotate, new FlowPayload { Amount = 1 });
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
        public override PerformanceDebugSchema Schema => PerformanceDebugSchemaFactory.ContextOnlySchema(PerformanceDebugContextPreset.Board9);

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
        public override PerformanceDebugSchema Schema => PerformanceDebugSchemaFactory.ContextOnlySchema(PerformanceDebugContextPreset.Board9);

        protected override PerformanceDebugPlayResult PlayModule(PerformanceDebugContext context, CardDeckSubstituteFlow module, PerformanceDebugPayload payload)
        {
            return FlowBindingPlayHelper.PlayFlow(context, FlowId.MoveCard, new FlowPayload());
        }
    }

    public sealed class SelectionEntranceDebugModule : PerformanceDebugModuleBase<SelectionEntranceFlow>
    {
        public override string Id => "flow.selection-entrance";
        public override string DisplayName => "选择入场";
        public override PerformanceDebugCategory Category => PerformanceDebugCategory.Flow;
        public override PerformanceDebugSchema Schema => PerformanceDebugSchemaFactory.ContextOnlySchema(PerformanceDebugContextPreset.Selection3);

        protected override PerformanceDebugPlayResult PlayModule(PerformanceDebugContext context, SelectionEntranceFlow module, PerformanceDebugPayload payload)
        {
            var options = new List<Transform>
            {
                context.ResolveActor("option1"),
                context.ResolveActor("option2"),
                context.ResolveActor("option3"),
            };
            module.Play(options);
            return PerformanceDebugPlayResult.Ok(module.ExpectedDuration);
        }
    }

    public sealed class SelectionConfirmDebugModule : PerformanceDebugModuleBase<SelectionConfirmFlow>
    {
        public override string Id => "flow.selection-confirm";
        public override string DisplayName => "选择确认";
        public override PerformanceDebugCategory Category => PerformanceDebugCategory.Flow;
        public override PerformanceDebugSchema Schema => PerformanceDebugSchemaFactory.ContextOnlySchema(PerformanceDebugContextPreset.Selection3);

        protected override PerformanceDebugPlayResult PlayModule(PerformanceDebugContext context, SelectionConfirmFlow module, PerformanceDebugPayload payload)
        {
            Transform selected = context.ResolveActor("option2");
            if (selected == null)
            {
                return PerformanceDebugPlayResult.Fail("Missing selected option actor.");
            }

            module.PlayLift(selected, 10, 3);
            return PerformanceDebugPlayResult.Ok(module.ExpectedDuration);
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
