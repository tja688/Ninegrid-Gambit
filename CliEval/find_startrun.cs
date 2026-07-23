var start = GameObject.Find("StartRun");
if (start == null) {
  var transforms = UnityEngine.Object.FindObjectsByType<Transform>(UnityEngine.FindObjectsSortMode.None);
  var hits = new System.Text.StringBuilder("candidates=");
  foreach (var t in transforms) {
    if (t.name.IndexOf("Start", System.StringComparison.OrdinalIgnoreCase) >= 0
        || t.name.IndexOf("Run", System.StringComparison.OrdinalIgnoreCase) >= 0)
      hits.Append(t.name).Append(';');
  }
  return hits.ToString();
}
var comps = start.GetComponents<Component>();
var sb = new System.Text.StringBuilder();
sb.Append("path=");
var p = start.transform;
var path = p.name;
while (p.parent != null) { p = p.parent; path = p.name + "/" + path; }
sb.Append(path).Append(" active=").Append(start.activeInHierarchy).Append(" comps=");
foreach (var c in comps) sb.Append(c.GetType().FullName).Append('|');
var loop = UnityEngine.Object.FindFirstObjectByType<NineGrid.Flow.GameFlowController>();
sb.Append(" loop=").Append(loop != null ? loop.State.ToString() : "null");
return sb.ToString();
