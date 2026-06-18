using System.Linq;
using NUnit.Framework;

namespace NineGrid.Core.Tests
{
    public sealed class P0ArchitectureGuardTests
    {
        [Test]
        public void CoreAsmdef_DisallowsEngineReferences()
        {
            var failures = CoreArchitectureGuard.Validate(CoreArchitectureGuard.ResolveCoreDirectory());
            var asmdefFailures = failures
                .Where(message => message.Contains("noEngineReferences"))
                .ToList();

            if (asmdefFailures.Count > 0)
            {
                Assert.Fail(string.Join("\n", asmdefFailures));
            }
        }

        [Test]
        public void CoreSources_DoNotReferenceUnityEngine()
        {
            var failures = CoreArchitectureGuard.Validate(CoreArchitectureGuard.ResolveCoreDirectory());
            var unityFailures = failures
                .Where(message => message.StartsWith("UnityEngine reference in Core"))
                .ToList();

            if (unityFailures.Count > 0)
            {
                Assert.Fail(string.Join("\n", unityFailures));
            }
        }

        [Test]
        public void GameSystems_DoNotBypassActionPipelineForModelMutation()
        {
            var failures = CoreArchitectureGuard.Validate(CoreArchitectureGuard.ResolveCoreDirectory());
            var bypassFailures = failures
                .Where(message => message.StartsWith("System bypasses Action pipeline")
                    || message.StartsWith("System clears/resets Model outside Action"))
                .ToList();

            if (bypassFailures.Count > 0)
            {
                Assert.Fail(string.Join("\n", bypassFailures));
            }
        }
    }
}
