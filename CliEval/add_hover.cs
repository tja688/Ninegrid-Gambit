var start = GameObject.Find("StartRun");
if (start == null) return "FAIL:no StartRun";
var existing = start.GetComponent<NineGrid.TemporaryTest.StartRunHoverScale>();
if (existing != null) UnityEngine.Object.Destroy(existing);
var c = start.AddComponent<NineGrid.TemporaryTest.StartRunHoverScale>();
c.hoverScale = 1.2f;
c.baseScale = start.transform.localScale;
return "added=True scale=" + c.hoverScale + " base=" + c.baseScale + " type=" + c.GetType().Assembly.GetName().Name;
