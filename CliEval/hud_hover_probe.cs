var hud = NineGrid.Flow.PlayerInfoHudPresenter.TryGetInstance();
var so = new UnityEditor.SerializedObject(hud);
var col = so.FindProperty("bloodSlotCollider").objectReferenceValue as UnityEngine.Collider2D;
var maxTmp = so.FindProperty("maxHpText").objectReferenceValue as TMPro.TMP_Text;
var center = col.bounds.center;
var hit = col.OverlapPoint(new UnityEngine.Vector2(center.x, center.y));
// Force hover visibility by temporarily enabling via reflection-less public path: call Sync then manually mimic SetMaxHpVisible through enabling
// Use Serialized? Instead move mouse via Input is hard. Directly verify collider works:
maxTmp.enabled = true;
var c = maxTmp.color; c.a = 0.75f; maxTmp.color = c;
return "colliderOk="+hit+" bounds="+col.bounds+" maxForcedShow text="+maxTmp.text+" en="+maxTmp.enabled;
