using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace NineGrid.Core.Tests
{
    internal static class CoreArchitectureGuard
    {
        private static readonly string[] sSystemBypassAllowlist =
        {
            "ActionPipelineSystem.cs",
            "TriggerSystem.cs",
            "StatSystem.cs"
        };

        private static readonly Regex sUnityEnginePattern = new Regex(
            @"using\s+UnityEngine\b|global::UnityEngine\b|(?<![A-Za-z0-9_])UnityEngine\.",
            RegexOptions.Compiled);

        private static readonly Regex sSystemBypassPattern = new Regex(
            @"\.(PlaceCard|RemoveCard|ClearSlot|ClearBoardCards|SetAvatar|SetBlessed|AddToDrawPile|AddToPlayerCardPool|AddToEnemyCardPool|AddToItemSlots|RemoveUid|ReorderDrawPile|AddCoins|AddInteractionCount|AddRelic|AddSkill|SetPhase|MoveCard)\("
            + @"|\.Stats\.SetBase\("
            + @"|\.Counters\.(Set|Add)\("
            + @"|\.(Zone|Slot)\.Value\s*="
            + @"|registry\.(Create|Remove|MoveCard)\(",
            RegexOptions.Compiled);

        private static readonly Regex sModelClearPattern = new Regex(
            @"(deck|board|registry|player|run)\.(Clear|Reset)\(",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static string ResolveCoreDirectory()
        {
            var assetsPath = UnityEngine.Application.dataPath;
            return Path.GetFullPath(Path.Combine(assetsPath, "Scripts", "NineGrid.Core"));
        }

        public static IReadOnlyList<string> Validate(string coreDirectory)
        {
            var failures = new List<string>();
            if (!Directory.Exists(coreDirectory))
            {
                failures.Add("Core directory not found: " + coreDirectory);
                return failures;
            }

            var asmdefPath = Path.Combine(coreDirectory, "NineGrid.Core.asmdef");
            if (!File.Exists(asmdefPath))
            {
                failures.Add("Missing asmdef: " + asmdefPath);
            }
            else
            {
                var asmdef = File.ReadAllText(asmdefPath);
                if (asmdef.IndexOf("\"noEngineReferences\": true", StringComparison.Ordinal) < 0)
                {
                    failures.Add("NineGrid.Core.asmdef must set \"noEngineReferences\": true");
                }
            }

            ScanDirectory(coreDirectory, "*.cs", (relativePath, lineNumber, line) =>
            {
                if (sUnityEnginePattern.IsMatch(line))
                {
                    failures.Add(FormatHit("UnityEngine reference in Core", relativePath, lineNumber, line));
                }
            });

            var systemsDirectory = Path.Combine(coreDirectory, "Systems");
            if (Directory.Exists(systemsDirectory))
            {
                ScanDirectory(systemsDirectory, "*.cs", (relativePath, lineNumber, line) =>
                {
                    var fileName = Path.GetFileName(relativePath);
                    if (IsAllowlistedSystem(fileName))
                    {
                        return;
                    }

                    if (sSystemBypassPattern.IsMatch(line))
                    {
                        failures.Add(FormatHit("System bypasses Action pipeline", relativePath, lineNumber, line));
                    }

                    if (sModelClearPattern.IsMatch(line))
                    {
                        failures.Add(FormatHit("System clears/resets Model outside Action", relativePath, lineNumber, line));
                    }
                });
            }

            return failures;
        }

        private static bool IsAllowlistedSystem(string fileName)
        {
            for (var i = 0; i < sSystemBypassAllowlist.Length; i++)
            {
                if (string.Equals(fileName, sSystemBypassAllowlist[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ScanDirectory(
            string directory,
            string searchPattern,
            Action<string, int, string> onMatch)
        {
            var files = Directory.GetFiles(directory, searchPattern, SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < files.Length; i++)
            {
                var filePath = files[i];
                var relativePath = filePath.Substring(directory.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var lines = File.ReadAllLines(filePath);
                for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    onMatch(relativePath, lineIndex + 1, lines[lineIndex]);
                }
            }
        }

        private static string FormatHit(string category, string relativePath, int lineNumber, string line)
        {
            return category + ": " + relativePath + ":" + lineNumber + " -> " + line.Trim();
        }
    }
}
