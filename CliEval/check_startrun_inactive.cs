var all = UnityEngine.Resources.FindObjectsOfTypeAll<Transform>();
foreach (var t in all) {
  if (t.name != "StartRun") continue;
  // skip prefab assets
  if (t.gameObject.scene.name == null || t.gameObject.scene.name == "") continue;
  var comps = t.GetComponents<Component>();
  var sb = new System.Text.StringBuilder();
  sb.Append("scene=").Append(t.gameObject.scene.name);
  sb.Append(" activeSelf=").Append(t.gameObject.activeSelf);
  sb.Append(" comps=");
  foreach (var c in comps) sb.Append(c.GetType().Name).Append('|');
  var hover = t.GetComponent<NineGrid.TemporaryTest.StartRunHoverScale>();
  sb.Append(" hasHover=").Append(hover != null);
  return sb.ToString();
}
return "StartRun not in loaded scenes";
