// Idempotent cleanup: unlock + allow refresh a few times in case of nested locks
for (var i = 0; i < 3; i++) {
  try { UnityEditor.EditorApplication.UnlockReloadAssemblies(); } catch {}
  try { UnityEditor.AssetDatabase.AllowAutoRefresh(); } catch {}
}
return "cleanup done isCompiling=" + UnityEditor.EditorApplication.isCompiling
  + " statusReady=" + (!UnityEditor.EditorApplication.isCompiling);
