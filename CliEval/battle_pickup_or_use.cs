// PickupItem or UseItem on first help card / item.
var arch = NineGrid.Core.NineGridArchitecture.Current;
var phase = arch.GetSystem<NineGrid.Core.Systems.IPhaseSystem>();
var board = arch.GetModel<NineGrid.Core.BoardModel>();
var reg = arch.GetModel<NineGrid.Core.CardRegistry>();
var sb = new System.Text.StringBuilder();
sb.Append("phase=").Append(phase.CurrentPhase).Append(" legal=");
foreach (var c in phase.LegalCommands) sb.Append(c).Append('|');

if (phase.CanExecute(NineGrid.Core.GameCommandKind.PickupItem))
{
    for (var i = NineGrid.Core.SlotId.MinBoardIndex; i <= NineGrid.Core.SlotId.MaxBoardIndex; i++)
    {
        var uid = board.GetCardUid(NineGrid.Core.SlotId.Board(i));
        if (uid <= 0 || !reg.TryGet(uid, out var card)) continue;
        if (card.Kind != NineGrid.Core.CardKind.HelpCard && card.Kind != NineGrid.Core.CardKind.Item)
            continue;
        var result = arch.SendCommand(new NineGrid.Core.Commands.PickupItemCommand(NineGrid.Core.SlotId.Board(i)));
        sb.Append(" pickupSlot=").Append(i).Append(" uid=").Append(uid);
        sb.Append(" accepted=").Append(result != null && result.Accepted);
        sb.Append(" reason=").Append(result != null ? result.Reason : "");
        sb.Append(" after=").Append(phase.CurrentPhase);
        return sb.ToString();
    }
    sb.Append(" no pickup target");
}

if (phase.CanExecute(NineGrid.Core.GameCommandKind.UseItem))
{
    // Prefer hand/item slots if model exposes — fall back: scan board help cards as item uid attempt.
    for (var i = NineGrid.Core.SlotId.MinBoardIndex; i <= NineGrid.Core.SlotId.MaxBoardIndex; i++)
    {
        var uid = board.GetCardUid(NineGrid.Core.SlotId.Board(i));
        if (uid <= 0 || !reg.TryGet(uid, out var card)) continue;
        if (card.Kind != NineGrid.Core.CardKind.HelpCard) continue;
        var result = arch.SendCommand(new NineGrid.Core.Commands.UseItemCommand(uid));
        sb.Append(" useUid=").Append(uid).Append(" accepted=").Append(result != null && result.Accepted);
        sb.Append(" reason=").Append(result != null ? result.Reason : "");
        return sb.ToString();
    }
}

return sb.Append(" nothing to do").ToString();
