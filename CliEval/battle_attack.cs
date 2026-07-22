// Attack weakest orthogonal monster via AttackInputHook (QF presentation path).
var arch = NineGrid.Core.NineGridArchitecture.Current;
var board = arch.GetModel<NineGrid.Core.BoardModel>();
var reg = arch.GetModel<NineGrid.Core.CardRegistry>();
var targetSlot = -1;
var targetUid = 0;
var targetHp = int.MaxValue;
for (var i = NineGrid.Core.SlotId.MinBoardIndex; i <= NineGrid.Core.SlotId.MaxBoardIndex; i++)
{
    var uid = board.GetCardUid(NineGrid.Core.SlotId.Board(i));
    if (uid <= 0 || !reg.TryGet(uid, out var card) || card.Kind != NineGrid.Core.CardKind.Monster)
        continue;
    var hp = (int)card.Stats.GetBase(NineGrid.Core.StatId.Hp);
    if (hp < targetHp)
    {
        targetHp = hp;
        targetSlot = i;
        targetUid = uid;
    }
}
if (targetSlot < 0) return "FAIL:no monster";
if (NineGrid.Cards.AttackInputHook.TrySubmitAttack == null)
    return "FAIL:AttackInputHook not wired";
var ok = NineGrid.Cards.AttackInputHook.TrySubmitAttack(targetSlot);
return "attackSlot=" + targetSlot + " uid=" + targetUid + " hpBase=" + targetHp + " submitted=" + ok;
