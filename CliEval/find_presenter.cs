var p = UnityEngine.Object.FindFirstObjectByType<NineGrid.Flow.PlayerInfoHudPresenter>(UnityEngine.FindObjectsInactive.Include);
string GetPath(UnityEngine.Transform t){ var s=t.name; while(t.parent!=null){t=t.parent;s=t.name+"/"+s;} return s; }
return p==null ? "null" : GetPath(p.transform)+" iid="+p.gameObject.GetInstanceID();
