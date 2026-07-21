// Direct Core AttackCommand — no IsCommandLegal helper.
var arch = NineGrid.Core.NineGridArchitecture.Current;
var phase = arch.GetSystem<NineGrid.Core.Systems.IPhaseSystem>();
var board = arch.GetModel<NineGrid.Core.BoardModel>();
var reg = arch.GetModel<NineGrid.Core.CardRegistry>();
var stats = arch.GetSystem<NineGrid.Core.Systems.IStatSystem>();
var targetSlot = -1;
var targetUid = 0;
for (var i = NineGrid.Core.SlotId.MinBoardIndex; i <= NineGrid.Core.SlotId.MaxBoardIndex; i++)
{
    var uid = board.GetCardUid(NineGrid.Core.SlotId.Board(i));
    if (uid <= 0 || !reg.TryGet(uid, out var card) || card.Kind != NineGrid.Core.CardKind.Monster)
        continue;
    targetSlot = i;
    targetUid = uid;
    break;
}
if (targetSlot < 0) return "FAIL:no monster phase=" + phase.CurrentPhase;

var avatarUid = board.AvatarUid.Value;
reg.TryGet(avatarUid, out var avatar);
var avatarAtk = avatar != null ? stats.GetEffectiveInt(avatar, NineGrid.Core.StatId.Attack) : -1;
reg.TryGet(targetUid, out var mon);
var monHpBefore = mon != null ? stats.GetEffectiveInt(mon, NineGrid.Core.StatId.Hp) : -1;

var before = phase.CurrentPhase.ToString();
var result = arch.SendCommand(new NineGrid.Core.Commands.AttackCommand(NineGrid.Core.SlotId.Board(targetSlot)));
var monHpAfter = -1;
if (reg.TryGet(targetUid, out mon))
    monHpAfter = stats.GetEffectiveInt(mon, NineGrid.Core.StatId.Hp);
else
    monHpAfter = -999; // removed

return "slot=" + targetSlot
    + " uid=" + targetUid
    + " avatarAtk=" + avatarAtk
    + " monHp " + monHpBefore + "->" + monHpAfter
    + " accepted=" + (result != null && result.Accepted)
    + " reason=" + (result != null ? result.Reason : "null")
    + " resolved=" + (result != null ? result.ResolvedActions : -1)
    + " before=" + before
    + " after=" + phase.CurrentPhase;
