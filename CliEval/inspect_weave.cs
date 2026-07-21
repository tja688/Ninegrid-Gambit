var t = typeof(NineGrid.TemporaryTest.StartRunHoverScale);
var mEnter = t.GetMethod("OnMouseEnter");
var mExit = t.GetMethod("OnMouseExit");
var sb = new System.Text.StringBuilder();
sb.Append("asm=").Append(t.Assembly.Location).Append(';');
sb.Append("enterNull=").Append(mEnter==null).Append(';');
// Dump IL byte length as crude weave signal
var body = mEnter.GetMethodBody();
sb.Append("enterIL=").Append(body != null ? body.GetILAsByteArray().Length : -1).Append(';');
sb.Append("exitIL=").Append(mExit.GetMethodBody().GetILAsByteArray().Length).Append(';');
// List custom attrs on method
foreach (var a in mEnter.GetCustomAttributes(false)) sb.Append("attr=").Append(a.GetType().Name).Append('|');
return sb.ToString();
