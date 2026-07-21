var sb = new System.Text.StringBuilder();
void Dump(string path) {
  var go = UnityEngine.GameObject.Find(path);
  if (go == null) { sb.AppendLine(path+" missing"); return; }
  foreach (var r in go.GetComponentsInChildren<UnityEngine.Renderer>(true)) {
    sb.Append(r.name).Append(" type=").Append(r.GetType().Name)
      .Append(" layer=").Append(UnityEngine.SortingLayer.IDToName(r.sortingLayerID))
      .Append(" order=").Append(r.sortingOrder)
      .AppendLine();
  }
}
Dump("玩家信息");
return sb.ToString();
