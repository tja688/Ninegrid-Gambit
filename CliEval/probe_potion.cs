var arch = NineGrid.Presentation.NineGridArchitecture.Current;
if (arch == null) return "no-arch";
var deck = arch.GetModel<NineGrid.Core.DeckModel>();
var board = arch.GetModel<NineGrid.Core.BoardModel>();
var reg = arch.GetModel<NineGrid.Core.CardRegistry>();
var run = arch.GetModel<NineGrid.Core.RunModel>();
var player = arch.GetModel<NineGrid.Core.PlayerModel>();
var sb = new System.Text.StringBuilder();
sb.Append("phase=").Append(run != null ? run.Phase.Value.ToString() : "null");
int drawPotions = 0;
if (deck != null && reg != null)
{
    sb.Append(" draw=").Append(deck.DrawPileUids.Count);
    for (var i = 0; i < deck.DrawPileUids.Count; i++)
    {
        NineGrid.Core.CardInstance c;
        if (reg.TryGet(deck.DrawPileUids[i], out c) && c.DefId == "help.healing_potion")
            drawPotions++;
    }
}
sb.Append(" drawPotions=").Append(drawPotions);
int boardPotions = 0;
if (board != null && reg != null)
{
    for (var s = NineGrid.Core.SlotId.MinBoardIndex; s <= NineGrid.Core.SlotId.MaxBoardIndex; s++)
    {
        var uid = board.GetCardUid(NineGrid.Core.SlotId.Board(s));
        NineGrid.Core.CardInstance c;
        if (uid > 0 && reg.TryGet(uid, out c) && c.DefId == "help.healing_potion")
            boardPotions++;
    }
}
sb.Append(" boardPotions=").Append(boardPotions);
if (player != null)
{
    sb.Append(" relics=[");
    var r = player.RelicDefIds;
    for (var i = 0; i < r.Count; i++)
    {
        if (i > 0) sb.Append(',');
        sb.Append(r[i]);
    }
    sb.Append(']');
}
return sb.ToString();
