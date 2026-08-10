using System.Linq;
using NineGrid.Content.Editor;
using NineGrid.Content.Vfx;
using NineGrid.Flow.Presentation;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
  public sealed class VfxBindingCatalogTests
  {
    private const string PriorityCatalogJson =
        "{\"schemaVersion\":1,\"ticket\":\"#193\",\"cueBindings\":["
        + "{\"cueId\":\"vfx.test.pulse\",\"enabled\":true,\"playerId\":\"sprite-sheet\",\"note\":\"base\","
        + "\"materialKey\":\"vfx.explosions.epic_explosion_001.large_orange\",\"spatialOwnership\":\"independent\"},"
        + "{\"cueId\":\"vfx.test.pulse\",\"enabled\":true,\"playerId\":\"sprite-sheet\",\"note\":\"card\","
        + "\"materialKey\":\"vfx.explosions.epic_explosion_001.small_orange\",\"selectorCardDefId\":\"monster.demo\","
        + "\"spatialOwnership\":\"attached\"},"
        + "{\"cueId\":\"vfx.test.pulse\",\"enabled\":true,\"playerId\":\"sprite-sheet\",\"note\":\"skill\","
        + "\"materialKey\":\"vfx.explosions.epic_explosion_002.large_yellow\",\"selectorSkillId\":\"skill.flame\","
        + "\"spatialOwnership\":\"independent\"},"
        + "{\"cueId\":\"vfx.test.pulse\",\"enabled\":true,\"playerId\":\"sprite-sheet\",\"note\":\"joint\","
        + "\"materialKey\":\"vfx.explosions.epic_explosion_002.small_yellow\","
        + "\"selectorCardDefId\":\"monster.demo\",\"selectorSkillId\":\"skill.flame\","
        + "\"bindingDelaySeconds\":0.6,\"spatialOwnership\":\"independent\"}"
        + "],\"stateBindings\":[]}";

    [Test]
    public void BindingResolve_PrefersJointThenSkillThenCardThenBase()
    {
      var catalog = VfxBindingCatalog.FromJson(PriorityCatalogJson);
      var request = new VfxCueRequest(
          "vfx.test.pulse",
          "test",
          "monster.demo",
          "skill.flame",
          string.Empty,
          string.Empty,
          string.Empty,
          42);

      Assert.IsTrue(catalog.TryResolveCue(request, out var joint));
      Assert.AreEqual("joint", joint.Note);
      Assert.AreEqual(0.6f, joint.BindingDelaySeconds, 0.001f);
      Assert.AreEqual(VfxSpatialOwnership.Independent, joint.SpatialOwnership);

      Assert.IsTrue(catalog.TryResolveCue(
          new VfxCueRequest("vfx.test.pulse", "test", "monster.other", "skill.flame", string.Empty, string.Empty, string.Empty),
          out var skillOnly));
      Assert.AreEqual("skill", skillOnly.Note);

      Assert.IsTrue(catalog.TryResolveCue(
          new VfxCueRequest("vfx.test.pulse", "test", "monster.demo", "skill.other", string.Empty, string.Empty, string.Empty),
          out var cardOnly));
      Assert.AreEqual("card", cardOnly.Note);
      Assert.AreEqual(VfxSpatialOwnership.Attached, cardOnly.SpatialOwnership);

      Assert.IsTrue(catalog.TryResolveCue(
          new VfxCueRequest("vfx.test.pulse", "test", "monster.other", "skill.other", string.Empty, string.Empty, string.Empty),
          out var baser));
      Assert.AreEqual("base", baser.Note);
    }

    [Test]
    public void TryFromJson_RejectsInvalidOrNullCollections()
    {
      Assert.IsFalse(VfxBindingCatalog.TryFromJson("{not-json", out _, out _));
      Assert.IsFalse(VfxBindingCatalog.TryFromJson("{\"schemaVersion\":1}", out _, out _));
      Assert.IsFalse(VfxBindingCatalog.TryFromJson(
          "{\"schemaVersion\":1,\"cueBindings\":[],\"stateBindings\":null}",
          out _,
          out _));

      Assert.IsTrue(VfxBindingCatalog.TryFromJson(
          "{\"schemaVersion\":1,\"cueBindings\":[],\"stateBindings\":[]}",
          out var empty,
          out var error),
          error);
      Assert.AreEqual(0, empty.CueBindings.Count);
      Assert.AreEqual(0, empty.StateBindings.Count);
    }

    [Test]
    public void BindingKey_DoesNotIncludeRuntimeUid()
    {
      var keyWithUid = VfxBindingKey.Compose(
          "vfx.test.pulse",
          "card.a",
          "skill.b",
          "room.c",
          "item.d",
          "content.e");
      var request = new VfxCueRequest(
          "vfx.test.pulse",
          "diag",
          "card.a",
          "skill.b",
          "room.c",
          "item.d",
          "content.e",
          999);
      var catalog = VfxBindingCatalog.FromJson(
          "{\"schemaVersion\":1,\"cueBindings\":[{\"cueId\":\"vfx.test.pulse\",\"enabled\":true,"
          + "\"playerId\":\"gold-flight\",\"note\":\"uid\",\"selectorCardDefId\":\"card.a\","
          + "\"selectorSkillId\":\"skill.b\",\"selectorRoomId\":\"room.c\","
          + "\"selectorItemDefId\":\"item.d\",\"selectorContentId\":\"content.e\"}],\"stateBindings\":[]}");

      Assert.IsTrue(catalog.TryResolveCue(request, out var binding));
      Assert.AreEqual(keyWithUid, binding.BindingKey);
      Assert.That(binding.BindingKey, Does.Not.Contain("999"));
    }

    [Test]
    public void TryResolveCueStrict_ReturnsAmbiguousError_ForSameSpecificity()
    {
      var catalog = VfxBindingCatalog.FromJson(
          "{\"schemaVersion\":1,\"cueBindings\":["
          + "{\"cueId\":\"vfx.ambiguous\",\"enabled\":true,\"playerId\":\"sprite-sheet\",\"note\":\"a\","
          + "\"materialKey\":\"vfx.explosions.epic_explosion_001.large_orange\",\"selectorCardDefId\":\"card.a\"},"
          + "{\"cueId\":\"vfx.ambiguous\",\"enabled\":true,\"playerId\":\"sprite-sheet\",\"note\":\"b\","
          + "\"materialKey\":\"vfx.explosions.epic_explosion_001.small_orange\",\"selectorSkillId\":\"skill.a\"}"
          + "],\"stateBindings\":[]}");

      var request = new VfxCueRequest(
          "vfx.ambiguous",
          "test",
          "card.a",
          "skill.a",
          string.Empty,
          string.Empty,
          string.Empty);

      Assert.IsFalse(catalog.TryResolveCueStrict(request, out _, out var error));
      Assert.AreEqual(VfxBindingResolveCode.Ambiguous, error.Code);
      Assert.IsFalse(string.IsNullOrEmpty(error.BindingKey));
      Assert.IsFalse(string.IsNullOrEmpty(error.ConflictingBindingKey));
    }

    [Test]
    public void DeclarationScan_ReportsDuplicateAndUnbound()
    {
      var catalog = VfxBindingCatalog.FromJson(
          "{\"schemaVersion\":1,\"cueBindings\":[{\"cueId\":\"vfx.scan.bound\",\"enabled\":true,"
          + "\"playerId\":\"gold-flight\",\"note\":\"bound\"}],\"stateBindings\":[]}");

      var result = VfxDeclarationScanner.Scan(catalog, typeof(VfxBindingCatalogTests).Assembly);

      Assert.That(result.Findings, Has.Some.Matches<VfxDeclarationFinding>(f =>
          f.IdentityId == "vfx.scan.duplicate" && f.Message.Contains("重复")));
      Assert.That(result.Findings, Has.Some.Matches<VfxDeclarationFinding>(f =>
          f.IdentityId == "vfx.scan.unbound" && f.Message.Contains("未绑定")));
      Assert.That(result.Findings, Has.None.Matches<VfxDeclarationFinding>(f =>
          f.IdentityId == "vfx.scan.bound" && f.Message.Contains("未绑定")));
    }

    [Test]
    public void EditorSession_TrySaveCue_MarksHumanConfirmed()
    {
      var session = new VfxBindingEditorSession();
      session.LoadFromJson(
          "{\"schemaVersion\":1,\"ticket\":\"#193\",\"cueBindings\":[],\"stateBindings\":[]}");

      var dto = new VfxCueBindingDto
      {
        cueId = "vfx.editor.save",
        note = "保存测试",
        module = "Tests",
        enabled = true,
        playerId = VfxPlayerRegistry.GoldFlight,
        spatialOwnership = "independent",
        authoringStatus = VfxBindingAuthoringStatuses.AiDraft,
      };

      Assert.IsTrue(session.TrySaveCue(dto, out var error), error);
      Assert.AreEqual(VfxBindingAuthoringStatuses.HumanConfirmed, dto.authoringStatus);
      var working = session.BuildWorkingCatalog();
      Assert.AreEqual(1, working.cueBindings.Length);
      Assert.AreEqual(VfxBindingAuthoringStatuses.HumanConfirmed, working.cueBindings[0].authoringStatus);
    }

    [Test]
    public void EditorSession_LoadFromJson_RejectsInvalidWithoutReplacingWorkingCopy()
    {
      var session = new VfxBindingEditorSession();
      Assert.IsTrue(session.LoadFromJson(PriorityCatalogJson));
      Assert.AreEqual(4, session.BuildWorkingCatalog().cueBindings.Length);

      Assert.IsFalse(session.LoadFromJson("{not-json"));
      Assert.AreEqual(4, session.BuildWorkingCatalog().cueBindings.Length);

      Assert.IsFalse(session.LoadFromJson(string.Empty));
      Assert.AreEqual(4, session.BuildWorkingCatalog().cueBindings.Length);
    }

    [Test]
    public void EditorSession_RejectsCoverageConflictOnSave()
    {
      var session = new VfxBindingEditorSession();
      session.LoadFromJson(PriorityCatalogJson);
      var ambiguous = new VfxCueBindingDto
      {
        cueId = "vfx.test.pulse",
        note = "conflict",
        module = "Tests",
        enabled = true,
        playerId = VfxPlayerRegistry.SpriteSheet,
        materialKey = "vfx.explosions.stylized_explosion_001.large_yellow",
        selectorCardDefId = "monster.demo",
        selectorSkillId = "skill.other",
        spatialOwnership = "independent",
      };

      Assert.IsFalse(session.TrySaveCue(ambiguous, out var error));
      StringAssert.Contains("覆盖冲突", error);
    }

    [VfxCue("vfx.scan.bound", "扫描器已绑定测试", "Tests", "VfxBindingCatalogTests", VfxCueContexts.None)]
    private const string ScanBoundCue = "vfx.scan.bound";

    [VfxCue("vfx.scan.unbound", "扫描器未绑定测试", "Tests", "VfxBindingCatalogTests", VfxCueContexts.None)]
    private const string ScanUnboundCue = "vfx.scan.unbound";

    [VfxCue("vfx.scan.duplicate", "扫描器重复测试一", "Tests", "VfxBindingCatalogTests", VfxCueContexts.None)]
    private const string ScanDuplicateCueA = "vfx.scan.duplicate";

    [VfxCue("vfx.scan.duplicate", "扫描器重复测试二", "Tests", "VfxBindingCatalogTests", VfxCueContexts.None)]
    private const string ScanDuplicateCueB = "vfx.scan.duplicate";
  }
}
