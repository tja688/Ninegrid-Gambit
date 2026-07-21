var hud = NineGrid.Flow.PlayerInfoHudPresenter.TryGetInstance();
var so = new UnityEditor.SerializedObject(hud);
var cur = (so.FindProperty("currentHpText").objectReferenceValue as TMPro.TMP_Text).text;
var slot = (so.FindProperty("bloodSlot").objectReferenceValue as UnityEngine.SpriteRenderer).size.x;
var fill = (so.FindProperty("bloodFill").objectReferenceValue as UnityEngine.SpriteRenderer).size.x;
return "midAnim cur="+cur+" slot="+slot.ToString("0.00")+" fill="+fill.ToString("0.00");
