var sb = new System.Text.StringBuilder();
void DumpSr(string path) {
  var t = UnityEngine.GameObject.Find(path)?.transform;
  if (t == null) { sb.AppendLine(path+" MISSING"); return; }
  var sr = t.GetComponent<UnityEngine.SpriteRenderer>();
  if (sr == null) { sb.AppendLine(path+" no SR"); return; }
  var sp = sr.sprite;
  sb.Append(path)
    .Append(" size=").Append(sr.size)
    .Append(" drawMode=").Append(sr.drawMode)
    .Append(" localPos=").Append(t.localPosition)
    .Append(" lossyScale=").Append(t.lossyScale)
    .Append(" bounds=").Append(sr.bounds)
    .Append(" pivot=").Append(sp!=null ? sp.pivot.ToString() : "null")
    .Append(" ppu=").Append(sp!=null ? sp.pixelsPerUnit.ToString() : "?")
    .Append(" rect=").Append(sp!=null ? sp.rect.ToString() : "?")
    .Append(" border=").Append(sp!=null ? sp.border.ToString() : "?")
    .AppendLine();
}
DumpSr("玩家信息/血条/血槽");
DumpSr("玩家信息/血条/血槽/真实血量条");
DumpSr("玩家信息/血条");
// all 金币图标
foreach (var t in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.Transform>()) {
  if (t == null || t.name != "金币图标" || !t.gameObject.scene.IsValid()) continue;
  string GetPath(UnityEngine.Transform x){ var p=x.name; while(x.parent!=null){x=x.parent;p=x.name+"/"+p;} return p; }
  sb.Append("icon ").Append(GetPath(t)).Append(" active=").Append(t.gameObject.activeInHierarchy).AppendLine();
}
// player base max hp from content
var catalogType = System.Type.GetType("NineGrid.Content.Catalog.TableNineContentCatalog, NineGrid.Content");
sb.Append("catalogType=").Append(catalogType!=null).AppendLine();
return sb.ToString();
