var hud = NineGrid.Flow.PlayerInfoHudPresenter.TryGetInstance();
var so = new UnityEditor.SerializedObject(hud);
var cur = (so.FindProperty("currentHpText").objectReferenceValue as TMPro.TMP_Text).text;
var max = (so.FindProperty("maxHpText").objectReferenceValue as TMPro.TMP_Text).text;
var slot = (so.FindProperty("bloodSlot").objectReferenceValue as UnityEngine.SpriteRenderer).size.x;
var fill = (so.FindProperty("bloodFill").objectReferenceValue as UnityEngine.SpriteRenderer).size.x;
var expectedSlot = 4.06f + (20-10)*0.1f;
var expectedFill = 2.31f + (20-10)*0.1f;
return "afterGrow cur="+cur+" maxTxt="+max+" slot="+slot.ToString("0.00")+" fill="+fill.ToString("0.00")
  +" expectSlot="+expectedSlot.ToString("0.00")+" expectFill="+expectedFill.ToString("0.00");
