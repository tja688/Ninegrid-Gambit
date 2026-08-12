// LiveLab marker: LiveLab_BorrowSync_detour
// 意图：拦截 SyncAdjacentBorrowedArmorAction.Apply，打印 target/source/邻接/追踪/护甲前后，定位护甲图腾错误叠甲。
// 落地目标：仅取证，不落地。
// 状态：不落地（诊断探针）
string key = "LiveLab_BorrowSync_detour";

var reg = AppDomain.CurrentDomain.GetData("LiveLab.Registry") as System.Collections.Generic.Dictionary<string, object>;
if (reg == null)
{
    reg = new System.Collections.Generic.Dictionary<string, object>();
    AppDomain.CurrentDomain.SetData("LiveLab.Registry", reg);
}
object old;
if (reg.TryGetValue(key, out old))
{
    var d = old as IDisposable;
    if (d != null) d.Dispose();
    reg.Remove(key);
}

var target = typeof(NineGrid.Core.SyncAdjacentBorrowedArmorAction).GetMethod(
    "Apply", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
if (target == null) return "[LiveLab] Apply 未找到";

NineGrid.Core.GameActionResult Replacement(
    Func<NineGrid.Core.SyncAdjacentBorrowedArmorAction, NineGrid.Core.GameActionContext, NineGrid.Core.GameActionResult> orig,
    NineGrid.Core.SyncAdjacentBorrowedArmorAction self,
    NineGrid.Core.GameActionContext ctx)
{
    var registry = ctx.GetModel<NineGrid.Core.CardRegistry>();
    NineGrid.Core.CardInstance t, s;
    registry.TryGet(self.TargetUid, out t);
    registry.TryGet(self.SourceUid, out s);
    var boardSys = ctx.GetSystem<NineGrid.Core.Systems.IBoardSystem>();
    var adj = t != null && s != null && boardSys.AreAdjacent(s, t);
    var trk = NineGrid.Core.BorrowedArmorAuraKeys.IsTracking(t, self.SourceUid);
    var baseline = t == null ? -1 : t.Counters.Get(NineGrid.Core.BorrowedArmorAuraKeys.BaselineKey(self.SourceUid));
    var before = NineGrid.Core.Stats.StatArmorUtility.GetCurrentArmor(t);
    var r = orig(self, ctx);
    var after = NineGrid.Core.Stats.StatArmorUtility.GetCurrentArmor(t);
    if (before != after || adj != trk)
    {
        Debug.Log("[LiveLab][BorrowSync] tgt=" + (t == null ? "?" : t.DefId) + "#" + self.TargetUid
            + "@" + (t == null ? "?" : t.Slot.Value.ToString())
            + " src=#" + self.SourceUid + "@" + (s == null ? "?" : s.Slot.Value.ToString())
            + " adj=" + adj + " trk=" + trk + " base=" + baseline
            + " armor " + before + "->" + after);
    }
    return r;
}

var replacement = new Func<
    Func<NineGrid.Core.SyncAdjacentBorrowedArmorAction, NineGrid.Core.GameActionContext, NineGrid.Core.GameActionResult>,
    NineGrid.Core.SyncAdjacentBorrowedArmorAction,
    NineGrid.Core.GameActionContext,
    NineGrid.Core.GameActionResult>(Replacement);

var hook = new MonoMod.RuntimeDetour.Hook(target, replacement);
reg[key] = hook;
return "[LiveLab] detour installed: " + key;
