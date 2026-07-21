var sb = new System.Text.StringBuilder();

// 1) Ensure presenter lives on 玩家信息
var root = UnityEngine.GameObject.Find("玩家信息");
if (root == null) return "FAIL:no 玩家信息";

var oldPresenter = UnityEngine.Object.FindFirstObjectByType<NineGrid.Flow.PlayerInfoHudPresenter>(UnityEngine.FindObjectsInactive.Include);
if (oldPresenter != null && oldPresenter.gameObject != root)
{
  UnityEngine.Object.DestroyImmediate(oldPresenter, true);
  sb.Append("removedOldPresenter;");
}

var presenter = root.GetComponent<NineGrid.Flow.PlayerInfoHudPresenter>();
if (presenter == null)
{
  presenter = root.AddComponent<NineGrid.Flow.PlayerInfoHudPresenter>();
  sb.Append("addedPresenter;");
}

// 2) Resolve refs
UnityEngine.Transform FindChild(UnityEngine.Transform r, string name) {
  var trim = name.Trim();
  foreach (var t in r.GetComponentsInChildren<UnityEngine.Transform>(true))
    if (t != null && t.name.Trim() == trim) return t;
  return null;
}

var bloodBar = FindChild(root.transform, "血条");
var bloodSlot = FindChild(root.transform, "血槽");
var bloodFill = FindChild(root.transform, "真实血量条");
var curHp = FindChild(root.transform, "血量数值（当前）");
var maxHp = FindChild(root.transform, "血量数值（最大）");
var armor = FindChild(root.transform, "防御数值");
var gold = FindChild(root.transform, "金币数值");
var goldIcon = FindChild(root.transform, "金币图标");

var so = new UnityEditor.SerializedObject(presenter);
void SetObj(string prop, UnityEngine.Object obj) {
  var p = so.FindProperty(prop);
  if (p == null) { sb.Append("missingProp="+prop+";"); return; }
  p.objectReferenceValue = obj;
  sb.Append(prop+"="+(obj!=null?obj.name:"null")+";");
}
SetObj("playerInfoRoot", root.transform);
SetObj("currentHpText", curHp != null ? curHp.GetComponent<TMPro.TMP_Text>() : null);
SetObj("maxHpText", maxHp != null ? maxHp.GetComponent<TMPro.TMP_Text>() : null);
SetObj("armorText", armor != null ? armor.GetComponent<TMPro.TMP_Text>() : null);
SetObj("goldText", gold != null ? gold.GetComponent<TMPro.TMP_Text>() : null);
SetObj("bloodSlot", bloodSlot != null ? bloodSlot.GetComponent<UnityEngine.SpriteRenderer>() : null);
SetObj("bloodFill", bloodFill != null ? bloodFill.GetComponent<UnityEngine.SpriteRenderer>() : null);
SetObj("bloodBarRoot", bloodBar);
so.FindProperty("baseMaxHp").floatValue = 10f;

// collider on blood slot
UnityEngine.Collider2D col = null;
if (bloodSlot != null)
{
  col = bloodSlot.GetComponent<UnityEngine.Collider2D>();
  if (col == null)
  {
    var box = bloodSlot.gameObject.AddComponent<UnityEngine.BoxCollider2D>();
    box.isTrigger = true;
    var sr = bloodSlot.GetComponent<UnityEngine.SpriteRenderer>();
    var w = sr.size.x; var h = UnityEngine.Mathf.Max(0.2f, sr.size.y);
    box.size = new UnityEngine.Vector2(w, h);
    box.offset = new UnityEngine.Vector2(w * 0.5f, 0f);
    col = box;
    sb.Append("addedBoxCollider;");
  }
}
SetObj("bloodSlotCollider", col);
so.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.EditorUtility.SetDirty(presenter);

// hide max hp text initially
if (maxHp != null)
{
  var tmp = maxHp.GetComponent<TMPro.TMP_Text>();
  if (tmp != null)
  {
    var c = tmp.color; c.a = 0f; tmp.color = c; tmp.enabled = false;
    UnityEditor.EditorUtility.SetDirty(tmp);
    sb.Append("maxHpHidden;");
  }
}

// 3) Gold FX rebind
var goldFx = UnityEngine.Object.FindFirstObjectByType<NineGrid.Flow.GoldGainFxManagerSingleton>(UnityEngine.FindObjectsInactive.Include);
if (goldFx != null)
{
  var gso = new UnityEditor.SerializedObject(goldFx);
  var gt = gso.FindProperty("goldText");
  var si = gso.FindProperty("sinkIcon");
  if (gt != null) gt.objectReferenceValue = gold != null ? gold.GetComponent<TMPro.TMP_Text>() : null;
  if (si != null) si.objectReferenceValue = goldIcon;
  gso.ApplyModifiedPropertiesWithoutUndo();
  UnityEditor.EditorUtility.SetDirty(goldFx);
  sb.Append("goldFxRebound;");
}
else sb.Append("goldFxMissing;");

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
var saved = UnityEditor.SceneManagement.EditorSceneManager.SaveScene(root.scene);
sb.Append("saved="+saved);
return sb.ToString();
