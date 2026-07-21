UnityEditor.EditorApplication.UnlockReloadAssemblies();
UnityEditor.AssetDatabase.AllowAutoRefresh();
return "unlocked isCompiling=" + UnityEditor.EditorApplication.isCompiling
  + " isUpdating=" + UnityEditor.EditorApplication.isUpdating;
