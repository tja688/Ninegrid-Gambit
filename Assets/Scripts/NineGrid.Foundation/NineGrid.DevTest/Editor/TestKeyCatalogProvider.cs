#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NineGrid.DevTest.Editor
{
    /// <summary>
    /// 从 Layer Profile SO 构建完整测试目录；Play Mode 时合并运行时挂载状态。
    /// </summary>
    public static class TestKeyCatalogProvider
    {
        private const string DevTestResourcesFolder = "Assets/Resources/DevTest";
        private const string StackResourcePath = "DevTest/TestKeyStack";

        public static IReadOnlyList<TestKeyCatalogEntry> GetCatalogEntries()
        {
            var entries = new List<TestKeyCatalogEntry>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            AppendDeclaredProfiles(entries, seen, LoadStackProfiles());
            AppendDeclaredProfiles(entries, seen, FindAllLayerProfiles());

            if (EditorApplication.isPlaying)
            {
                MergeRuntimeEntries(entries, seen);
            }

            entries.Sort(CompareEntries);
            return entries;
        }

        private static int CompareEntries(TestKeyCatalogEntry left, TestKeyCatalogEntry right)
        {
            var layerCompare = string.Compare(
                left.LayerDisplayName,
                right.LayerDisplayName,
                StringComparison.OrdinalIgnoreCase);
            if (layerCompare != 0)
            {
                return layerCompare;
            }

            var labelCompare = string.Compare(left.Label, right.Label, StringComparison.OrdinalIgnoreCase);
            if (labelCompare != 0)
            {
                return labelCompare;
            }

            return left.Key.CompareTo(right.Key);
        }

        private static void MergeRuntimeEntries(List<TestKeyCatalogEntry> entries, HashSet<string> seen)
        {
            var runtimeByKey = new Dictionary<string, TestKeyRegisteredAction>(StringComparer.Ordinal);
            var runtimeActions = TestKeyManager.Instance.GetAllRegisteredActions();
            for (var i = 0; i < runtimeActions.Count; i++)
            {
                var action = runtimeActions[i];
                runtimeByKey[BuildEntryKey(action.LayerId, action.Key)] = action;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var entryKey = BuildEntryKey(entry.LayerId, entry.Key);
                if (!runtimeByKey.TryGetValue(entryKey, out var runtime))
                {
                    continue;
                }

                entries[i] = new TestKeyCatalogEntry(
                    entry.LayerId,
                    entry.LayerDisplayName,
                    entry.Key,
                    entry.Label,
                    hasLiveCallback: true,
                    runtime.IsKeypadActive);
                runtimeByKey.Remove(entryKey);
            }

            foreach (var pair in runtimeByKey)
            {
                var action = pair.Value;
                var entryKey = BuildEntryKey(action.LayerId, action.Key);
                if (!seen.Add(entryKey))
                {
                    continue;
                }

                entries.Add(new TestKeyCatalogEntry(
                    action.LayerId,
                    action.LayerDisplayName,
                    action.Key,
                    action.Label,
                    hasLiveCallback: true,
                    action.IsKeypadActive));
            }
        }

        private static void AppendDeclaredProfiles(
            List<TestKeyCatalogEntry> entries,
            HashSet<string> seen,
            IReadOnlyList<TestKeyLayerProfileSO> profiles)
        {
            for (var i = 0; i < profiles.Count; i++)
            {
                var profile = profiles[i];
                if (profile == null || string.IsNullOrWhiteSpace(profile.LayerId))
                {
                    continue;
                }

                var declaredBindings = profile.DeclaredBindings;
                for (var j = 0; j < declaredBindings.Count; j++)
                {
                    var binding = declaredBindings[j];
                    if (binding.key == KeyCode.None)
                    {
                        continue;
                    }

                    var entryKey = BuildEntryKey(profile.LayerId, binding.key);
                    if (!seen.Add(entryKey))
                    {
                        continue;
                    }

                    var label = string.IsNullOrWhiteSpace(binding.label)
                        ? binding.key.ToString()
                        : binding.label;

                    entries.Add(new TestKeyCatalogEntry(
                        profile.LayerId,
                        profile.DisplayName,
                        binding.key,
                        label,
                        hasLiveCallback: false,
                        isKeypadActive: false));
                }
            }
        }

        private static List<TestKeyLayerProfileSO> LoadStackProfiles()
        {
            var result = new List<TestKeyLayerProfileSO>();
            var stack = Resources.Load<TestKeyStackConfigSO>(StackResourcePath);
            if (stack?.Layers == null)
            {
                return result;
            }

            for (var i = 0; i < stack.Layers.Count; i++)
            {
                if (stack.Layers[i] != null)
                {
                    result.Add(stack.Layers[i]);
                }
            }

            return result;
        }

        private static List<TestKeyLayerProfileSO> FindAllLayerProfiles()
        {
            var result = new List<TestKeyLayerProfileSO>();
            var guids = AssetDatabase.FindAssets("t:TestKeyLayerProfileSO", new[] { DevTestResourcesFolder });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var profile = AssetDatabase.LoadAssetAtPath<TestKeyLayerProfileSO>(path);
                if (profile != null)
                {
                    result.Add(profile);
                }
            }

            return result;
        }

        private static string BuildEntryKey(string layerId, KeyCode key) => $"{layerId}::{key}";
    }
}

#endif
