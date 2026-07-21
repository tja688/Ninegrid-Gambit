var arch = NineGrid.Core.NineGridArchitecture.Current;
var sb = new System.Text.StringBuilder();
var loop = NineGrid.Flow.MainGameLoopManagerSingleton.Instance;
sb.Append("state=").Append(loop.State).Append(';');
var board = arch.GetModel<NineGrid.Core.BoardModel>();
var reg = arch.GetModel<NineGrid.Core.CardRegistry>();
var stats = arch.GetSystem<NineGrid.Core.Systems.IStatSystem>();
var player = arch.GetModel<NineGrid.Core.PlayerModel>();
var uid = board.AvatarUid.Value;
sb.Append("avatar=").Append(uid).Append(';');
if (uid > 0 && reg.TryGet(uid, out var avatar)) {
  sb.Append("hp=").Append(stats.GetEffectiveInt(avatar, NineGrid.Core.StatId.Hp));
  sb.Append(" maxHp=").Append(stats.GetEffectiveInt(avatar, NineGrid.Core.StatId.MaxHp));
  sb.Append(" armor=").Append(NineGrid.Core.Stats.StatArmorUtility.GetEffectiveArmor(stats, avatar));
}
sb.Append(" gold=").Append(player.Coins.Value).Append(';');

var hud = NineGrid.Flow.PlayerInfoHudPresenter.TryGetInstance();
var so = new UnityEditor.SerializedObject(hud);
string ReadTmp(string prop) {
  var p = so.FindProperty(prop);
  var tmp = p != null ? p.objectReferenceValue as TMPro.TMP_Text : null;
  return tmp == null ? "null" : tmp.text + "/en=" + tmp.enabled + "/a=" + tmp.color.a.ToString("0.00");
}
string ReadSr(string prop) {
  var p = so.FindProperty(prop);
  var sr = p != null ? p.objectReferenceValue as UnityEngine.SpriteRenderer : null;
  return sr == null ? "null" : "sizeX=" + sr.size.x.ToString("0.00");
}
sb.Append("UI curHp=").Append(ReadTmp("currentHpText"));
sb.Append(" maxHpTxt=").Append(ReadTmp("maxHpText"));
sb.Append(" armorTxt=").Append(ReadTmp("armorText"));
sb.Append(" goldTxt=").Append(ReadTmp("goldText"));
sb.Append(" slot=").Append(ReadSr("bloodSlot"));
sb.Append(" fill=").Append(ReadSr("bloodFill"));

var goldFx = NineGrid.Flow.GoldGainFxManagerSingleton.Instance;
if (goldFx != null) {
  var gso = new UnityEditor.SerializedObject(goldFx);
  var gt = gso.FindProperty("goldText").objectReferenceValue as TMPro.TMP_Text;
  var si = gso.FindProperty("sinkIcon").objectReferenceValue as UnityEngine.Transform;
  sb.Append(" goldFxText=").Append(gt!=null?gt.name:"null");
  sb.Append(" sink=").Append(si!=null?si.name:"null");
  sb.Append(" displayed=").Append(goldFx.DisplayedGold);
}
return sb.ToString();
