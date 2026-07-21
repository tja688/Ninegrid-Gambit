var root = UnityEngine.GameObject.Find("玩家信息");
var p = root.GetComponent<NineGrid.Flow.PlayerInfoHudPresenter>();
var comps = root.GetComponents<UnityEngine.Component>();
var sb = new System.Text.StringBuilder();
sb.Append("comps=");
foreach (var c in comps) if (c!=null) sb.Append(c.GetType().Name).Append(',');
var slot = root.transform.Find("血条/血槽");
sb.Append(" boxCol=").Append(slot!=null && slot.GetComponent<UnityEngine.BoxCollider2D>()!=null);
return sb.ToString();
