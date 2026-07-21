var arch = NineGrid.Core.NineGridArchitecture.Current;
var board = arch.GetModel<NineGrid.Core.BoardModel>();
var reg = arch.GetModel<NineGrid.Core.CardRegistry>();
var stats = arch.GetSystem<NineGrid.Core.Systems.IStatSystem>();
NineGrid.Core.CardInstance shrimp = null;
var slot = -1;
for (var i = NineGrid.Core.SlotId.MinBoardIndex; i <= NineGrid.Core.SlotId.MaxBoardIndex; i++) {
  var uid = board.GetCardUid(NineGrid.Core.SlotId.Board(i));
  if (uid <= 0 || !reg.TryGet(uid, out var card)) continue;
  if (card.DefId == "monster.stone_shrimp") { shrimp = card; slot = i; break; }
}
if (shrimp == null) return "FAIL:石虾不在场上";
var before = stats.GetEffectiveInt(shrimp, NineGrid.Core.StatId.Attack);
var mods = new System.Collections.Generic.List<NineGrid.Core.Stats.StatModifier>(shrimp.Stats.Modifiers);
foreach (var m in mods) shrimp.Stats.RemoveModifier(m);
shrimp.Stats.SetBase(NineGrid.Core.StatId.Attack, 0);
var after = stats.GetEffectiveInt(shrimp, NineGrid.Core.StatId.Attack);
return "slot=" + slot + " uid=" + shrimp.Uid + " atk " + before + "->" + after
  + " base=" + (int)shrimp.Stats.GetBase(NineGrid.Core.StatId.Attack)
  + " modsLeft=" + shrimp.Stats.Modifiers.Count;
