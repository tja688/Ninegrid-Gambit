using System.IO;
using System.Linq;
using System.Text;
using NineGrid.Content;
using NineGrid.Content.Editor;
using NineGrid.Content.Vfx;
using NineGrid.Flow.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
  /// <summary>#193/#200 正式 vfx_bindings.json、声明、播放器注册与素材路径卫生门禁。</summary>
  public sealed class VfxDeliveryHygieneTests
  {
    [Test]
    public void FormalCatalog_HasNoCriticalHygieneFindings()
    {
      var projectRoot = new DirectoryInfo(Application.dataPath).Parent.FullName;
      var catalogPath = Path.Combine(
          projectRoot,
          VfxBindingCatalogPaths.ManifestAssetPath.Replace('/', Path.DirectorySeparatorChar));
      Assert.IsTrue(File.Exists(catalogPath), "缺少正式 vfx_bindings.json");

      var json = File.ReadAllText(catalogPath, Encoding.UTF8);
      var catalog = JsonUtility.FromJson<VfxBindingCatalogDto>(json);
      Assert.IsNotNull(catalog);
      Assert.IsNotNull(catalog.cueBindings);
      Assert.IsNotNull(catalog.stateBindings);

      var materialIds = CollectFormalMaterialIds();
      var declaredCueIds = CollectDeclaredCueIds();
      var declaredStateIds = CollectDeclaredStateIds();

      var cueFindings = VfxBindingCatalogHygieneValidator.ValidateCueBindings(
          catalog,
          declaredCueIds,
          materialIds);
      var stateFindings = VfxBindingCatalogHygieneValidator.ValidateStateBindings(
          catalog,
          declaredStateIds,
          materialIds);

      var critical = cueFindings
          .Concat(stateFindings)
          .Where(f => IsCritical(f.Category))
          .Select(f => f.ToString())
          .ToList();

      Assert.IsEmpty(critical, "正式 VFX Catalog 卫生失败：\n" + string.Join("\n", critical.Take(40)));
    }

    [Test]
    public void Declarations_HaveUniqueIdsAndNonEmptyNotes()
    {
      var catalog = VfxBindingCatalog.LoadFromResources();
      var scan = VfxDeclarationScanner.Scan(catalog, typeof(VfxCueAttribute).Assembly);

      var critical = scan.Findings
          .Where(f =>
              f.Message.IndexOf("重复", System.StringComparison.Ordinal) >= 0
              || f.Message.IndexOf("不能为空", System.StringComparison.Ordinal) >= 0)
          .Select(f => f.IdentityId + ": " + f.Message)
          .ToList();

      Assert.IsEmpty(critical, "VFX 声明卫生失败：\n" + string.Join("\n", critical.Take(40)));
    }

    [Test]
    public void FormalCatalog_PlayersAreRegistered_AndMaterialPathsSane()
    {
      var projectRoot = new DirectoryInfo(Application.dataPath).Parent.FullName;
      var catalogPath = Path.Combine(
          projectRoot,
          VfxBindingCatalogPaths.ManifestAssetPath.Replace('/', Path.DirectorySeparatorChar));
      var json = File.ReadAllText(catalogPath, Encoding.UTF8);
      var catalog = JsonUtility.FromJson<VfxBindingCatalogDto>(json);
      Assert.IsNotNull(catalog);

      var violations = new System.Collections.Generic.List<string>();
      foreach (var row in catalog.cueBindings ?? System.Array.Empty<VfxCueBindingDto>())
      {
        if (row == null)
        {
          continue;
        }

        if (!VfxPlayerRegistry.IsKnownPlayerId(row.playerId))
        {
          violations.Add("cue " + row.cueId + ": 未知 playerId=" + row.playerId);
        }

        if (VfxPlayerRegistry.IsMaterialPlayer(row.playerId)
            && string.IsNullOrWhiteSpace(row.materialKey)
            && (row.variants == null || row.variants.Length == 0))
        {
          violations.Add("cue " + row.cueId + ": 素材型播放器缺少 materialKey/variants");
        }

        if (!string.IsNullOrWhiteSpace(row.materialKey)
            && (row.materialKey.IndexOf("..", System.StringComparison.Ordinal) >= 0
                || row.materialKey.IndexOf('\\') >= 0
                || row.materialKey.StartsWith("/", System.StringComparison.Ordinal)))
        {
          violations.Add("cue " + row.cueId + ": 非法素材路径 " + row.materialKey);
        }
      }

      foreach (var row in catalog.stateBindings ?? System.Array.Empty<VfxStateBindingDto>())
      {
        if (row == null)
        {
          continue;
        }

        if (!VfxPlayerRegistry.IsKnownPlayerId(row.playerId))
        {
          violations.Add("state " + row.stateId + ": 未知 playerId=" + row.playerId);
        }

        if (!VfxPlayerRegistry.SupportsState(row.playerId))
        {
          violations.Add("state " + row.stateId + ": playerId=" + row.playerId + " 不支持 State");
        }

        if (!string.IsNullOrWhiteSpace(row.materialKey)
            && (row.materialKey.IndexOf("..", System.StringComparison.Ordinal) >= 0
                || row.materialKey.IndexOf('\\') >= 0
                || row.materialKey.StartsWith("/", System.StringComparison.Ordinal)))
        {
          violations.Add("state " + row.stateId + ": 非法素材路径 " + row.materialKey);
        }
      }

      Assert.IsEmpty(violations, "播放器注册/素材路径卫生失败：\n" + string.Join("\n", violations.Take(40)));
    }

    private static bool IsCritical(string category)
    {
      switch (category)
      {
        case "duplicate-binding-key":
        case "coverage-conflict":
        case "orphan-binding":
        case "missing-material":
        case "missing-player":
        case "empty-cue":
        case "empty-state":
        case "empty-note":
        case "forbidden-overrides":
        case "param-range":
        case "authoring-status":
        case "catalog-null":
        case "null-row":
        case "empty-pool":
          return true;
        default:
          return category != null
              && (category.IndexOf("forbidden", System.StringComparison.OrdinalIgnoreCase) >= 0);
      }
    }

    private static System.Collections.Generic.HashSet<string> CollectFormalMaterialIds()
    {
      var ids = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
      foreach (var entry in VisualEffectCatalog.All)
      {
        if (!string.IsNullOrWhiteSpace(entry?.id))
        {
          ids.Add(entry.id.Trim());
        }
      }

      return ids;
    }

    private static System.Collections.Generic.HashSet<string> CollectDeclaredCueIds()
    {
      var ids = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
      var scan = VfxDeclarationScanner.Scan(typeof(VfxCueAttribute).Assembly);
      foreach (var declaration in scan.CueDeclarations)
      {
        var cueId = declaration?.Attribute?.CueId;
        if (!string.IsNullOrWhiteSpace(cueId))
        {
          ids.Add(cueId.Trim());
        }
      }

      return ids;
    }

    private static System.Collections.Generic.HashSet<string> CollectDeclaredStateIds()
    {
      var ids = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
      var scan = VfxDeclarationScanner.Scan(typeof(VfxCueAttribute).Assembly);
      foreach (var declaration in scan.StateDeclarations)
      {
        var stateId = declaration?.Attribute?.StateId;
        if (!string.IsNullOrWhiteSpace(stateId))
        {
          ids.Add(stateId.Trim());
        }
      }

      return ids;
    }
  }
}
