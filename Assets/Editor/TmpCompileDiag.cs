using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

public static class TmpCompileDiag
{
    public static string Run()
    {
        var sb = new StringBuilder();
        void OnFinished(object ctx)
        {
            // no-op
        }

        var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);
        var tests = assemblies.FirstOrDefault(a => a.name == "NineGrid.Presentation.Tests");
        if (tests == null)
        {
            return "assembly_missing";
        }

        sb.Append("tests_asm=").Append(tests.name);
        sb.Append(" sources=").Append(tests.sourceFiles.Length);
        var has = tests.sourceFiles.Any(f => f.Replace('\\', '/').EndsWith("VisualEffectCatalogTests.cs"));
        sb.Append(" hasVfxTests=").Append(has);
        return sb.ToString();
    }
}
