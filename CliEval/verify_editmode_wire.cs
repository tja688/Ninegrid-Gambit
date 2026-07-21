var root = UnityEngine.GameObject.Find("玩家信息");
var p = root.GetComponent<NineGrid.Flow.PlayerInfoHudPresenter>();
var so = new UnityEditor.SerializedObject(p);
var sb = new System.Text.StringBuilder();
sb.Append("presenterOnRoot=").Append(p!=null);
sb.Append(" cur=").Append(so.FindProperty("currentHpText").objectReferenceValue!=null);
sb.Append(" slot=").Append(so.FindProperty("bloodSlot").objectReferenceValue!=null);
sb.Append(" col=").Append(so.FindProperty("bloodSlotCollider").objectReferenceValue!=null);
var goldFx = UnityEngine.Object.FindFirstObjectByType<NineGrid.Flow.GoldGainFxManagerSingleton>(UnityEngine.FindObjectsInactive.Include);
var gso = new UnityEditor.SerializedObject(goldFx);
sb.Append(" goldTxt=").Append((gso.FindProperty("goldText").objectReferenceValue as TMPro.TMP_Text)?.name);
sb.Append(" sinkPath=");
var sink = gso.FindProperty("sinkIcon").objectReferenceValue as UnityEngine.Transform;
if (sink != null) {
  var t=sink; var path=t.name; while(t.parent!=null){t=t.parent;path=t.name+"/"+path;}
  sb.Append(path);
}
return sb.ToString();
