// unity command eval_file — NOT a Unity-compiled script (keep outside asmdef / Scripts).
var loop = UnityEngine.Object.FindFirstObjectByType<NineGrid.Flow.GameFlowController>();
var arch = NineGrid.Core.NineGridArchitecture.Current;
var sb = new System.Text.StringBuilder();
sb.Append("loop=").Append(loop != null ? loop.State.ToString() : "null").Append(';');
if (arch == null) return sb.Append("archNull=True").ToString();

var phase = arch.GetSystem<NineGrid.Core.Systems.IPhaseSystem>();
sb.Append("phase=").Append(phase.CurrentPhase).Append(';');
sb.Append("legal=");
foreach (var c in phase.LegalCommands) sb.Append(c).Append('|');
sb.Append(';');

var board = arch.GetModel<NineGrid.Core.BoardModel>();
var reg = arch.GetModel<NineGrid.Core.CardRegistry>();
var stats = arch.GetSystem<NineGrid.Core.Systems.IStatSystem>();
sb.Append("avatarUid=").Append(board.AvatarUid.Value).Append(';');
sb.Append("board=");
for (var i = NineGrid.Core.SlotId.MinBoardIndex; i <= NineGrid.Core.SlotId.MaxBoardIndex; i++)
{
    var uid = board.GetCardUid(NineGrid.Core.SlotId.Board(i));
    if (uid <= 0) continue;
    var kind = "?";
    var atk = 0;
    var hp = 0;
    if (reg.TryGet(uid, out var card))
    {
        kind = card.Kind.ToString();
        atk = stats.GetEffectiveInt(card, NineGrid.Core.StatId.Attack);
        hp = stats.GetEffectiveInt(card, NineGrid.Core.StatId.Hp);
    }
    sb.Append('[').Append(i).Append(':').Append(kind).Append('#').Append(uid)
        .Append(" a").Append(atk).Append(" h").Append(hp).Append(']');
}

var sync = arch.GetSystem<NineGrid.Core.IPresentationSyncSystem>();
sb.Append(";inputLocked=").Append(sync.IsInputLocked);
return sb.ToString();
