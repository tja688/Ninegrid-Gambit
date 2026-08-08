using System;
using System.Collections.Generic;
using System.Reflection;
using NineGrid.Content.Audio;

using NineGrid.Flow.Presentation;

namespace NineGrid.Content.Editor
{
    public sealed class AudioCueDeclaration
    {
        public AudioCueDeclaration(FieldInfo field, AudioCueAttribute attribute)
        {
            Field = field;
            Attribute = attribute;
        }

        public FieldInfo Field { get; }
        public AudioCueAttribute Attribute { get; }
    }

    public sealed class AudioCueDeclarationFinding
    {
        public string CueId { get; set; }
        public string FieldName { get; set; }
        public string Message { get; set; }
    }

    public sealed class AudioCueDeclarationScanResult
    {
        public IReadOnlyList<AudioCueDeclaration> Declarations { get; set; }
        public IReadOnlyList<AudioCueDeclarationFinding> Findings { get; set; }
    }

    public static class AudioCueDeclarationScanner
    {
        public static AudioCueDeclarationScanResult Scan(params Assembly[] assemblies)
        {
            return Scan(null, assemblies);
        }

        public static AudioCueDeclarationScanResult Scan(
            AudioBindingCatalog catalog,
            params Assembly[] assemblies)
        {
            var declarations = new List<AudioCueDeclaration>();
            var findings = new List<AudioCueDeclarationFinding>();
            var byCueId = new Dictionary<string, AudioCueDeclaration>(StringComparer.Ordinal);
            var scanAssemblies = assemblies ?? Array.Empty<Assembly>();

            for (var assemblyIndex = 0; assemblyIndex < scanAssemblies.Length; assemblyIndex++)
            {
                var types = GetTypesSafely(scanAssemblies[assemblyIndex]);
                for (var typeIndex = 0; typeIndex < types.Length; typeIndex++)
                {
                    var fields = types[typeIndex].GetFields(
                        BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    for (var fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
                    {
                        var field = fields[fieldIndex];
                        var attributes = field.GetCustomAttributes(typeof(AudioCueAttribute), false);
                        var attribute = attributes.Length > 0 ? attributes[0] as AudioCueAttribute : null;
                        if (attribute == null)
                        {
                            continue;
                        }

                        var declaration = new AudioCueDeclaration(field, attribute);
                        declarations.Add(declaration);
                        if (string.IsNullOrWhiteSpace(attribute.CueId))
                        {
                            findings.Add(new AudioCueDeclarationFinding
                            {
                                CueId = string.Empty,
                                FieldName = field.DeclaringType.FullName + "." + field.Name,
                                Message = "声音提示 cue ID 不能为空。",
                            });
                        }

                        if (string.IsNullOrWhiteSpace(attribute.Note))
                        {
                            findings.Add(new AudioCueDeclarationFinding
                            {
                                CueId = attribute.CueId,
                                FieldName = field.DeclaringType.FullName + "." + field.Name,
                                Message = "声音提示中文音效说明不能为空。",
                            });
                        }

                        if (!string.IsNullOrWhiteSpace(attribute.CueId)
                            && byCueId.TryGetValue(attribute.CueId, out var previous))
                        {
                            findings.Add(new AudioCueDeclarationFinding
                            {
                                CueId = attribute.CueId,
                                FieldName = field.DeclaringType.FullName + "." + field.Name,
                                Message = "声音提示 cue ID 重复：" + previous.Field.DeclaringType.FullName + "." + previous.Field.Name,
                            });
                        }
                        else if (!string.IsNullOrWhiteSpace(attribute.CueId))
                        {
                            byCueId[attribute.CueId] = declaration;
                        }
                    }
                }
            }

            if (catalog != null)
            {
                var boundCueIds = new HashSet<string>(StringComparer.Ordinal);
                var bindings = catalog.Bindings;
                for (var bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
                {
                    var binding = bindings[bindingIndex];
                    if (binding != null && !string.IsNullOrWhiteSpace(binding.CueId))
                    {
                        boundCueIds.Add(binding.CueId);
                    }
                }

                for (var declarationIndex = 0; declarationIndex < declarations.Count; declarationIndex++)
                {
                    var declaration = declarations[declarationIndex];
                    var cueId = declaration.Attribute.CueId;
                    if (string.IsNullOrWhiteSpace(cueId) || boundCueIds.Contains(cueId))
                    {
                        continue;
                    }

                    findings.Add(new AudioCueDeclarationFinding
                    {
                        CueId = cueId,
                        FieldName = declaration.Field.DeclaringType.FullName + "." + declaration.Field.Name,
                        Message = "声音提示未绑定。",
                    });
                }
            }

            return new AudioCueDeclarationScanResult
            {
                Declarations = declarations,
                Findings = findings,
            };
        }

        private static Type[] GetTypesSafely(Assembly assembly)
        {
            if (assembly == null)
            {
                return Array.Empty<Type>();
            }

            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                var loaded = new List<Type>();
                var types = exception.Types;
                for (var i = 0; i < types.Length; i++)
                {
                    if (types[i] != null)
                    {
                        loaded.Add(types[i]);
                    }
                }

                return loaded.ToArray();
            }
        }
    }
}
