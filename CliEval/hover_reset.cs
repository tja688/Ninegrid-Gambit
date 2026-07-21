var start = GameObject.Find("StartRun");
var c = start.GetComponent<NineGrid.TemporaryTest.StartRunHoverScale>();
c.hovering = false;
start.transform.localScale = c.baseScale;
return "reset scale=" + start.transform.localScale + " base=" + c.baseScale + " fieldHoverScale=" + c.hoverScale;
