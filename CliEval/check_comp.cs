var start = GameObject.Find("StartRun");
var c = start != null ? start.GetComponent<NineGrid.TemporaryTest.StartRunHoverScale>() : null;
return "playing=" + Application.isPlaying + " hasComp=" + (c != null) + " scale=" + (start != null ? start.transform.localScale.ToString() : "?");
