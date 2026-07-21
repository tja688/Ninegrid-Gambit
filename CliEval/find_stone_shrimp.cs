var arch = NineGrid.Core.NineGridArchitecture.Current;
if (arch == null) return "FAIL:no arch";
var phase = arch.GetSystem<NineGrid.Core.Systems.IPhaseSystem>();
var board = arch.GetModel<NineGrid.Core.BoardModel>();
var reg = arch.GetModel<NineGrid.Core.CardRegistry>();
var stats = arch.GetSystem<NineGrid.Core.Systems.IStatSystem>();
var sb = new System.Text.StringBuilder();
sb.Append("phase=").Append(phase.CurrentPhase).Append(';');
NineGrid.Core.CardInstance shrimp = null;
var shrimpSlot = -1;
for (var i = NineGrid.Core.SlotId.MinBoardIndex; i <= NineGrid.Core.SlotId.MaxBoardIndex; i++) {
  var uid = board.GetCardUid(NineGrid.Core.SlotId.Board(i));
  if (uid <= 0 || !reg.TryGet(uid, out var card)) continue;
  var defId = card.DefId ?? "";
  var atk = stats.GetEffectiveInt(card, NineGrid.Core.StatId.Attack);
  var hp = stats.GetEffectiveInt(card, NineGrid.Core.StatId.Hp);
  var armor = stats.GetEffectiveInt(card, NineGrid.Core.StatId.Armor);
  var isShrimp = defId == "monster.stone_shrimp" || defId.IndexOf("stone_shrimp", System.StringComparison.OrdinalIgnoreCase) >= 0;
  sb.Append('[').Append(i).Append(':').Append(card.Kind).Append(' ').Append(defId)
    .Append(" atk=").Append(atk).Append(" hp=").Append(hp).Append(" armor=").Append(armor)
    .Append(isShrimp ? " <<石虾>>" : "").Append(']');
  if (isShrimp) { shrimp = card; shrimpSlot = i; }
}
if (shrimp == null) {
  sb.Append(" RESULT=NOT_ON_BOARD");
  return sb.ToString();
}
var atkEff = stats.GetEffectiveInt(shrimp, NineGrid.Core.StatId.Attack);
var atkBase = (int)shrimp.Stats.GetBase(NineGrid.Core.StatId.Attack);
sb.Append(" RESULT slot=").Append(shrimpSlot)
  .Append(" uid=").Append(shrimp.Uid)
  .Append(" def=").Append(shrimp.DefId)
  .Append(" atkEffective=").Append(atkEff)
  .Append(" atkBase=").Append(atkBase);
return sb.ToString();
