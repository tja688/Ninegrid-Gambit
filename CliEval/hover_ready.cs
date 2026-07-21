var start = GameObject.Find("StartRun");
var c = start != null ? start.GetComponent<NineGrid.TemporaryTest.StartRunHoverScale>() : null;
var loop = UnityEngine.Object.FindFirstObjectByType<NineGrid.Flow.MainGameLoopManagerSingleton>();
return "playing=" + Application.isPlaying
  + " hasStart=" + (start != null)
  + " hasHover=" + (c != null)
  + " loop=" + (loop != null ? loop.State.ToString() : "null");
