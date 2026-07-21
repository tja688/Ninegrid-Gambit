var t = typeof(NineGrid.TemporaryTest.StartRunHoverScale);
var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
  | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly;
var n = 0;
foreach (var method in t.GetMethods(flags)) {
  var inPlace = method.GetCustomAttribute<Unity.Pipeline.HotReload.HotReloadAttribute>();
  if (inPlace == null) continue;
  Unity.Pipeline.HotReload.HotReloadRegistry.RegisterReloadableMethod(
    method,
    new Unity.Pipeline.HotReload.HotReloadWithOverridesAttribute { RequireMainThread = inPlace.RequireMainThread });
  n++;
}
var stats = Unity.Pipeline.HotReload.HotReloadRegistry.GetStats();
return "registered=" + n + " reloadable=" + stats.ReloadableMethodCount + " ids=" + string.Join(",", stats.ReloadableMethodIds);
