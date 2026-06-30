using NineGrid.Core;
using NineGrid.Presentation.Projection;
using NineGrid.Presentation.Visuals;
using UnityEngine;

namespace NineGrid.Presentation.Debugging.Modules
{
    public sealed class InGameStatusProjectionDebugModule : PerformanceDebugModuleBase<InGameStatusProjection>
    {
        public override string Id => "projection.status-panel";
        public override string DisplayName => "局内状态面板";
        public override PerformanceDebugCategory Category => PerformanceDebugCategory.Projection;
        public override PerformanceDebugSchema Schema => CreateSchema()
            .Add("contextPreset", "Context", PerformanceDebugParamKind.ContextPreset,
                PerformanceDebugContextPreset.StatusPanel.ToString(),
                PerformanceDebugSchemaFactory.EnumNames<PerformanceDebugContextPreset>())
            .Add("stat", "Stat", PerformanceDebugParamKind.Enum, "Attack", "Attack", "Life", "Armor")
            .Add("from", "From", PerformanceDebugParamKind.Int, "1")
            .Add("to", "To", PerformanceDebugParamKind.Int, "7");

        protected override PerformanceDebugPlayResult PlayModule(PerformanceDebugContext context, InGameStatusProjection module, PerformanceDebugPayload payload)
        {
            Transform actor = context.ResolveActor("statusCard") ?? context.ResolveActor("player");
            if (actor == null)
            {
                return PerformanceDebugPlayResult.Fail("Missing status card actor.");
            }

            TableNineCardStatusView view = actor.GetComponent<TableNineCardStatusView>();
            if (view == null)
            {
                view = actor.gameObject.AddComponent<TableNineCardStatusView>();
                view.EnsureBindings();
                view.ConfigureDigitSprites(TableNineDigitSpriteLibrary.LoadDefaultDigits());
            }

            int from = payload.GetInt("from", 1);
            int to = payload.GetInt("to", 7);
            string stat = payload.GetString("stat", "Attack");
            var slot = new BoardSlotView(SlotId.Board(1), 1, "debug", CardKind.Monster, 5, 5, 5, 5, 0, 0, from, from, false);
            view.SnapFromSlot(slot);

            switch (stat)
            {
                case "Life":
                    view.PlayLifeTo(to, animate: true);
                    break;
                case "Armor":
                    view.PlayArmorTo(to, animate: true);
                    break;
                default:
                    view.PlayAttackTo(to, animate: true);
                    break;
            }

            return PerformanceDebugPlayResult.Ok(1f);
        }
    }
}
