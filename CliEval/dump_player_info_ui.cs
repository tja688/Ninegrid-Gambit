var root = UnityEngine.GameObject.Find("玩家信息");
if (root == null) return "NOT_FOUND";
var sb = new System.Text.StringBuilder();
void Dump(UnityEngine.Transform t, int d) {
  var pad = new string(' ', d*2);
  sb.Append(pad).Append(t.name)
    .Append(" iid=").Append(t.gameObject.GetInstanceID())
    .Append(" active=").Append(t.gameObject.activeSelf);
  var rt = t as UnityEngine.RectTransform;
  if (rt != null) {
    sb.Append(" sizeDelta=").Append(rt.sizeDelta)
      .Append(" anchored=").Append(rt.anchoredPosition)
      .Append(" scale=").Append(rt.localScale)
      .Append(" rectW=").Append(rt.rect.width)
      .Append(" rectH=").Append(rt.rect.height);
  } else {
    sb.Append(" localScale=").Append(t.localScale)
      .Append(" localPos=").Append(t.localPosition);
  }
  sb.AppendLine();
  foreach (var c in t.GetComponents<UnityEngine.Component>()) {
    if (c == null || c is UnityEngine.Transform) continue;
    sb.Append(pad).Append("  [").Append(c.GetType().Name).Append("]");
    var tmp = c as TMPro.TMP_Text;
    if (tmp != null) {
      sb.Append(" text=\"").Append(tmp.text).Append("\" fontSize=").Append(tmp.fontSize)
        .Append(" color=#").Append(UnityEngine.ColorUtility.ToHtmlStringRGBA(tmp.color))
        .Append(" enabled=").Append(tmp.enabled)
        .Append(" alpha=").Append(tmp.alpha);
    }
    var img = c as UnityEngine.UI.Image;
    if (img != null) {
      sb.Append(" color=#").Append(UnityEngine.ColorUtility.ToHtmlStringRGBA(img.color))
        .Append(" type=").Append(img.type)
        .Append(" fill=").Append(img.fillAmount)
        .Append(" sprite=").Append(img.sprite != null ? img.sprite.name : "null")
        .Append(" raycast=").Append(img.raycastTarget);
    }
    var sr = c as UnityEngine.SpriteRenderer;
    if (sr != null) {
      sb.Append(" color=#").Append(UnityEngine.ColorUtility.ToHtmlStringRGBA(sr.color))
        .Append(" sprite=").Append(sr.sprite != null ? sr.sprite.name : "null")
        .Append(" size=").Append(sr.size)
        .Append(" drawMode=").Append(sr.drawMode);
    }
    var col = c as UnityEngine.Collider2D;
    if (col != null) {
      sb.Append(" enabled=").Append(col.enabled).Append(" isTrigger=").Append(col.isTrigger);
    }
    sb.AppendLine();
  }
  for (int i=0;i<t.childCount;i++) Dump(t.GetChild(i), d+1);
}
Dump(root.transform, 0);
var presenter = UnityEngine.Object.FindFirstObjectByType<NineGrid.Flow.PlayerInfoHudPresenter>(UnityEngine.FindObjectsInactive.Include);
sb.Append("presenter=").Append(presenter != null ? presenter.name : "null");
if (presenter != null) {
  var so = new UnityEditor.SerializedObject(presenter);
  var props = new[]{"hpText","attackText","armorText","goldText","nameText"};
  foreach (var p in props) {
    var sp = so.FindProperty(p);
    sb.Append(" ").Append(p).Append("=").Append(sp!=null && sp.objectReferenceValue!=null ? sp.objectReferenceValue.name : "null");
  }
}
var goldFx = UnityEngine.Object.FindFirstObjectByType<NineGrid.Flow.GoldGainFxManagerSingleton>(UnityEngine.FindObjectsInactive.Include);
sb.AppendLine();
sb.Append("goldFx=").Append(goldFx!=null ? goldFx.name : "null");
if (goldFx != null) {
  var so = new UnityEditor.SerializedObject(goldFx);
  foreach (var p in new[]{"goldText","sinkIcon","coinPrefab"}) {
    var sp = so.FindProperty(p);
    sb.Append(" ").Append(p).Append("=").Append(sp!=null && sp.objectReferenceValue!=null ? sp.objectReferenceValue.name : "null");
  }
}
return sb.ToString();
