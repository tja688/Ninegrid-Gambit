var start = GameObject.Find("StartRun");
if (start == null) return "StartRun not found in edit mode";
var comps = start.GetComponents<Component>();
var sb = new System.Text.StringBuilder("editMode comps=");
foreach (var c in comps) sb.Append(c.GetType().Name).Append('|');
var hover = start.GetComponent("NineGrid.TemporaryTest.StartRunHoverScale");
sb.Append(" hasHoverType=").Append(hover != null);
sb.Append(" scale=").Append(start.transform.localScale);
sb.Append(" sceneDirty=");
#if UNITY_EDITOR
sb.Append(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().isDirty);
#endif
return sb.ToString();
