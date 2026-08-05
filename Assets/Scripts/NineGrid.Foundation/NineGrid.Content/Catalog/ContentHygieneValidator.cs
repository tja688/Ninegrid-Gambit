using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NineGrid.Content.CardPresentation;
using UnityEngine;

namespace NineGrid.Content
{
    /// <summary>
    /// 内容卫生校验（#140）：索引↔磁盘、Authoring↔Streaming 双写、skillIds→技能 JSON、
    /// 装配→效果模板、模板 body 引用的 contentId、空壳技能、归档内容可达性。
    /// 纯磁盘 + 运行时 API，EditMode 测试与编辑器菜单共用。
    /// </summary>
    public static class ContentHygieneValidator
    {
        // 模板 body 中引用 contentId 的字段键（含条件 DSL 的 source/target/exclude 变体）。
        private const string DefIdKeys = "defId|sourceDefId|targetDefId|excludeSourceDefId";

        private static readonly Regex DefIdInBody =
            new Regex("\"(?:" + DefIdKeys + ")\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public sealed class Finding
        {
            public string Category;
            public string ContentId;
            public string Detail;

            public override string ToString()
            {
                return "[" + Category + "] " + ContentId + " " + Detail;
            }
        }

        [Serializable]
        private sealed class TemplateRowDto
        {
            public string id;
            public string body;
        }

        [Serializable]
        private sealed class JsonArrayWrapper<T>
        {
            public T[] items;
        }

        public static List<Finding> ValidateAll()
        {
            var findings = new List<Finding>();
            findings.AddRange(ValidateIndexVsDisk());
            findings.AddRange(ValidateMirror());
            findings.AddRange(ValidateSkillLinks());
            findings.AddRange(ValidateAssemblyTemplateLinks());
            findings.AddRange(ValidateTemplateBodyDefIds());
            findings.AddRange(ValidateEmptyShellSkills());
            findings.AddRange(ValidateArchiveReachability());
            return findings;
        }

        /// <summary>索引条目 ↔ 磁盘生产内容完全一致（双向、无重复、两侧索引一致）。</summary>
        public static List<Finding> ValidateIndexVsDisk()
        {
            var findings = new List<Finding>();
            var issues = CardPresentationIndexIO.ValidateIndexVsDisk(
                CardPresentationIndexIO.GetAuthoringFolderAbsolute(),
                CardPresentationIndexIO.GetStreamingFolderAbsolute());
            for (var i = 0; i < issues.Count; i++)
            {
                findings.Add(new Finding { Category = "index", ContentId = string.Empty, Detail = issues[i] });
            }

            return findings;
        }

        /// <summary>Authoring / Streaming 文件集合与内容一致。</summary>
        public static List<Finding> ValidateMirror()
        {
            var findings = new List<Finding>();
            var issues = CardPresentationIndexIO.ValidateMirror(
                CardPresentationIndexIO.GetAuthoringFolderAbsolute(),
                CardPresentationIndexIO.GetStreamingFolderAbsolute());
            for (var i = 0; i < issues.Count; i++)
            {
                findings.Add(new Finding { Category = "mirror", ContentId = string.Empty, Detail = issues[i] });
            }

            return findings;
        }

        /// <summary>所有 skillIds 必须对应磁盘上的 Skill JSON。</summary>
        public static List<Finding> ValidateSkillLinks()
        {
            var findings = new List<Finding>();
            var dtos = LoadAllDtos();
            var skills = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in dtos)
            {
                if (IsKind(pair.Value.kind, "Skill"))
                {
                    skills.Add(pair.Key);
                }
            }

            foreach (var pair in dtos)
            {
                var dto = pair.Value;
                if (dto.skillIds == null || dto.skillIds.Length == 0)
                {
                    continue;
                }

                for (var i = 0; i < dto.skillIds.Length; i++)
                {
                    var skillId = dto.skillIds[i];
                    if (string.IsNullOrWhiteSpace(skillId))
                    {
                        findings.Add(new Finding
                        {
                            Category = "skill-link",
                            ContentId = dto.contentId,
                            Detail = "empty skillIds[" + i + "]",
                        });
                        continue;
                    }

                    if (!skills.Contains(skillId.Trim()))
                    {
                        findings.Add(new Finding
                        {
                            Category = "skill-link",
                            ContentId = dto.contentId,
                            Detail = "skillIds -> missing skill " + skillId,
                        });
                    }
                }
            }

            return findings;
        }

        /// <summary>所有装配引用必须对应 effect_templates.json 中的模板。</summary>
        public static List<Finding> ValidateAssemblyTemplateLinks()
        {
            var findings = new List<Finding>();
            var templates = LoadTemplateIds();
            foreach (var pair in LoadAllDtos())
            {
                var dto = pair.Value;
                if (dto.effectAssemblies == null || dto.effectAssemblies.Length == 0)
                {
                    continue;
                }

                for (var i = 0; i < dto.effectAssemblies.Length; i++)
                {
                    var assembly = dto.effectAssemblies[i];
                    if (assembly == null || string.IsNullOrWhiteSpace(assembly.id))
                    {
                        findings.Add(new Finding
                        {
                            Category = "assembly",
                            ContentId = dto.contentId,
                            Detail = "assembly[" + i + "] has no id",
                        });
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(assembly.templateId))
                    {
                        findings.Add(new Finding
                        {
                            Category = "assembly",
                            ContentId = dto.contentId,
                            Detail = "assembly " + assembly.id + " has no templateId",
                        });
                        continue;
                    }

                    if (!templates.Contains(assembly.templateId.Trim()))
                    {
                        findings.Add(new Finding
                        {
                            Category = "assembly",
                            ContentId = dto.contentId,
                            Detail = "assembly " + assembly.id + " -> missing template " + assembly.templateId,
                        });
                    }
                }
            }

            return findings;
        }

        /// <summary>模板 body 中引用的 defId（非 {{占位符}}）必须存在。</summary>
        public static List<Finding> ValidateTemplateBodyDefIds()
        {
            var findings = new List<Finding>();
            var dtos = LoadAllDtos();
            var templates = LoadTemplates();
            for (var i = 0; i < templates.Count; i++)
            {
                var row = templates[i];
                if (string.IsNullOrWhiteSpace(row.body))
                {
                    continue;
                }

                foreach (Match match in DefIdInBody.Matches(row.body))
                {
                    var id = match.Groups[1].Value;
                    if (string.IsNullOrWhiteSpace(id) || id.Contains("{{"))
                    {
                        continue;
                    }

                    if (!dtos.ContainsKey(id))
                    {
                        findings.Add(new Finding
                        {
                            Category = "template-ref",
                            ContentId = row.id,
                            Detail = "body defId -> missing content " + id,
                        });
                    }
                }
            }

            return findings;
        }

        /// <summary>不得存在空壳技能（Skill 且无任何效果装配/效果 id）——归档投影不得留成正式空技能。</summary>
        public static List<Finding> ValidateEmptyShellSkills()
        {
            var findings = new List<Finding>();
            foreach (var pair in LoadAllDtos())
            {
                var dto = pair.Value;
                if (!IsKind(dto.kind, "Skill"))
                {
                    continue;
                }

                var hasAssemblies = dto.effectAssemblies != null && dto.effectAssemblies.Length > 0;
                var hasEffectIds = dto.effectIds != null && dto.effectIds.Length > 0;
                if (!hasAssemblies && !hasEffectIds)
                {
                    findings.Add(new Finding
                    {
                        Category = "empty-shell",
                        ContentId = dto.contentId,
                        Detail = "Skill with no assemblies and no effectIds",
                    });
                }
            }

            return findings;
        }

        /// <summary>
        /// 归档内容（deck.relic_archive / deck.help_archive 成员）不得进入正式 grant 路径：
        /// 房间开局注入、模板 body defId、legacy reward_entries 白名单。
        /// 奖池查询展开的归档排除由 RewardPoolQueryExpander / ProfessionCatalog 保证（#115 / #139 契约）。
        /// </summary>
        public static List<Finding> ValidateArchiveReachability()
        {
            var findings = new List<Finding>();
            var dtos = LoadAllDtos();
            var archived = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in dtos)
            {
                var dto = pair.Value;
                if (string.IsNullOrWhiteSpace(dto.deckId))
                {
                    continue;
                }

                var deck = dto.deckId.Trim();
                if (string.Equals(deck, "deck.relic_archive", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(deck, "deck.help_archive", StringComparison.OrdinalIgnoreCase))
                {
                    archived.Add(pair.Key);
                }
            }

            if (archived.Count == 0)
            {
                return findings;
            }

            foreach (var pair in dtos)
            {
                var dto = pair.Value;
                if (dto.openingInjects == null)
                {
                    continue;
                }

                for (var i = 0; i < dto.openingInjects.Length; i++)
                {
                    var inject = dto.openingInjects[i];
                    if (inject == null)
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(inject.cardDefId)
                        && archived.Contains(inject.cardDefId.Trim()))
                    {
                        findings.Add(new Finding
                        {
                            Category = "archive-grant",
                            ContentId = dto.contentId,
                            Detail = "openingInject[" + i + "] references archived " + inject.cardDefId,
                        });
                    }

                    if (inject.pool == null)
                    {
                        continue;
                    }

                    for (var p = 0; p < inject.pool.Length; p++)
                    {
                        var option = inject.pool[p];
                        if (option != null
                            && !string.IsNullOrWhiteSpace(option.cardDefId)
                            && archived.Contains(option.cardDefId.Trim()))
                        {
                            findings.Add(new Finding
                            {
                                Category = "archive-grant",
                                ContentId = dto.contentId,
                                Detail = "openingInject[" + i + "].pool[" + p + "] references archived " + option.cardDefId,
                            });
                        }
                    }
                }
            }

            var templates = LoadTemplates();
            var formalOwnedTemplates = ComputeFormalOwnedTemplates(dtos, archived);
            for (var i = 0; i < templates.Count; i++)
            {
                var row = templates[i];
                if (string.IsNullOrWhiteSpace(row.body) || !formalOwnedTemplates.Contains(row.id))
                {
                    continue;
                }

                foreach (Match match in DefIdInBody.Matches(row.body))
                {
                    var id = match.Groups[1].Value;
                    if (!string.IsNullOrWhiteSpace(id) && archived.Contains(id))
                    {
                        findings.Add(new Finding
                        {
                            Category = "archive-grant",
                            ContentId = row.id,
                            Detail = "template body defId references archived " + id,
                        });
                    }
                }
            }

            return findings;
        }

        /// <summary>
        /// 模板若只被归档卡装配引用（archive→archive 死数据，永不可达），不参与归档可达性检查；
        /// 任一装配者非归档（正式内容）才算正式挂载。
        /// </summary>
        private static HashSet<string> ComputeFormalOwnedTemplates(
            Dictionary<string, CardPresentationConfigDto> dtos,
            HashSet<string> archived)
        {
            var formal = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in dtos)
            {
                var dto = pair.Value;
                if (archived.Contains(dto.contentId)
                    || dto.effectAssemblies == null
                    || dto.effectAssemblies.Length == 0)
                {
                    continue;
                }

                for (var i = 0; i < dto.effectAssemblies.Length; i++)
                {
                    var assembly = dto.effectAssemblies[i];
                    if (assembly != null && !string.IsNullOrWhiteSpace(assembly.templateId))
                    {
                        formal.Add(assembly.templateId.Trim());
                    }
                }
            }

            return formal;
        }

        private static Dictionary<string, CardPresentationConfigDto> LoadAllDtos()
        {
            var dtos = new Dictionary<string, CardPresentationConfigDto>(StringComparer.Ordinal);
            var folder = CardPresentationIndexIO.GetAuthoringFolderAbsolute();
            if (!Directory.Exists(folder))
            {
                return dtos;
            }

            var files = Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly);
            for (var i = 0; i < files.Length; i++)
            {
                var name = Path.GetFileName(files[i]);
                if (string.Equals(name, CardPresentationJsonIO.IndexFileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!CardPresentationJsonIO.TryLoad(files[i], out var dto, out _) || dto == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(dto.contentId))
                {
                    dtos[dto.contentId.Trim()] = dto;
                }
            }

            return dtos;
        }

        private static HashSet<string> LoadTemplateIds()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var rows = LoadTemplates();
            for (var i = 0; i < rows.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(rows[i].id))
                {
                    ids.Add(rows[i].id.Trim());
                }
            }

            return ids;
        }

        private static List<TemplateRowDto> LoadTemplates()
        {
            var rows = new List<TemplateRowDto>();
            var folder = ContentCatalogTableLoader.ResolveTablesDirectory();
            if (string.IsNullOrEmpty(folder))
            {
                return rows;
            }

            var path = Path.Combine(folder, "effect_templates.json");
            if (!File.Exists(path))
            {
                return rows;
            }

            try
            {
                var raw = File.ReadAllText(path, System.Text.Encoding.UTF8);
                var wrapped = "{\"items\":" + raw + "}";
                var list = JsonUtility.FromJson<JsonArrayWrapper<TemplateRowDto>>(wrapped);
                rows.AddRange(list != null && list.items != null ? list.items : new TemplateRowDto[0]);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ContentHygieneValidator] Failed to load " + path + ": " + ex.Message);
            }

            return rows;
        }

        private static bool IsKind(string kind, string expected)
        {
            return string.Equals(kind, expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}
