var sb = new System.Text.StringBuilder();
UnityEditor.EditorApplication.LockReloadAssemblies();
UnityEditor.AssetDatabase.DisallowAutoRefresh();
sb.Append("locked=True disallowAutoRefresh=True");
sb.Append(" isCompiling=").Append(UnityEditor.EditorApplication.isCompiling);
sb.Append(" isUpdating=").Append(UnityEditor.EditorApplication.isUpdating);
sb.Append(" playMode=").Append(UnityEditor.EditorApplication.isPlaying);
return sb.ToString();
