var start = GameObject.Find("StartRun");
var c = start.GetComponent<NineGrid.TemporaryTest.StartRunHoverScale>();
var before = start.transform.localScale;
c.OnMouseEnter();
var after = start.transform.localScale;
return "before=" + before + " after=" + after + " hovering=" + c.hovering + " hoverScale=" + c.hoverScale;
