using System;
using System.Collections.Generic;
using System.Text;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Core.Content;

namespace NineGrid.Cards.Presentation
{
    /// <summary>
    /// 右键详述面板文案：背景介绍 / 牌组介绍 / 技能·效果详述。
    /// 详述区只展开效果模板 <c>design_text</c>（分门别类的详细效果），
    /// 不重复卡面简要 <c>description</c>（简要已在展示的真卡面上）。
    /// </summary>
    public static class CardInspectDetailComposer
    {
        public readonly struct Result
        {
            public readonly string FaceIntro;
            public readonly string DeckIntro;
            public readonly string SkillDetails;

            public Result(string faceIntro, string deckIntro, string skillDetails)
            {
                FaceIntro = faceIntro ?? string.Empty;
                DeckIntro = deckIntro ?? string.Empty;
                SkillDetails = skillDetails ?? string.Empty;
            }
        }

        public static Result Compose(
            string defId,
            CardPresentationSnapshot snapshot,
            GameContentCatalog catalog)
        {
            CardPresentationConfigDto dto = null;
            if (!string.IsNullOrWhiteSpace(defId))
            {
                CardPresentationConfigCatalog.TryGet(defId.Trim(), out dto);
            }

            var assemblies = dto?.effectAssemblies;
            var faceIntro = ResolveFaceIntro(dto, snapshot, assemblies);
            var deckIntro = ResolveDeckIntro(dto, catalog);
            var skills = ResolveSkillDetails(dto, catalog, assemblies);
            return new Result(faceIntro, deckIntro, skills);
        }

        private static string ResolveFaceIntro(
            CardPresentationConfigDto dto,
            CardPresentationSnapshot snapshot,
            EffectAssemblyDto[] assemblies)
        {
            if (snapshot != null && !string.IsNullOrWhiteSpace(snapshot.FaceIntro))
            {
                return snapshot.FaceIntro.Trim();
            }

            if (dto == null || string.IsNullOrWhiteSpace(dto.faceIntro))
            {
                return string.Empty;
            }

            return CardFaceDescriptionParamFiller.FillFromAssemblies(dto.faceIntro, assemblies).Trim();
        }

        private static string ResolveDeckIntro(
            CardPresentationConfigDto dto,
            GameContentCatalog catalog)
        {
            var deckId = dto != null ? dto.deckId : null;
            if (string.IsNullOrWhiteSpace(deckId))
            {
                return string.Empty;
            }

            deckId = deckId.Trim();
            if (CardPresentationConfigCatalog.TryGet(deckId, out var deckDto) && deckDto != null)
            {
                var name = string.IsNullOrWhiteSpace(deckDto.displayName) ? deckId : deckDto.displayName.Trim();
                var body = !string.IsNullOrWhiteSpace(deckDto.description)
                    ? deckDto.description.Trim()
                    : (deckDto.faceIntro ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(body))
                {
                    return name;
                }

                return name + "\n" + body;
            }

            if (catalog != null
                && catalog.MonsterDecks != null
                && catalog.MonsterDecks.TryGetValue(deckId, out var monsterDeck)
                && monsterDeck != null
                && !string.IsNullOrWhiteSpace(monsterDeck.DisplayName))
            {
                return monsterDeck.DisplayName.Trim();
            }

            return deckId;
        }

        private static string ResolveSkillDetails(
            CardPresentationConfigDto dto,
            GameContentCatalog catalog,
            EffectAssemblyDto[] assemblies)
        {
            var blocks = new List<string>(8);

            AppendSkillBlocks(blocks, dto, catalog);
            AppendAssemblyBlocks(blocks, assemblies, catalog);
            AppendCatalogCardEffectBlocks(blocks, dto, catalog);

            if (blocks.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(256);
            for (var i = 0; i < blocks.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append("\n\n");
                }

                builder.Append(blocks[i]);
            }

            return builder.ToString();
        }

        /// <summary>
        /// 卡 JSON 未挂 skillIds / assemblies 时，仍尝试展开 Catalog 上已投影的卡级效果 DesignText。
        /// </summary>
        private static void AppendCatalogCardEffectBlocks(
            List<string> blocks,
            CardPresentationConfigDto dto,
            GameContentCatalog catalog)
        {
            if (blocks.Count > 0 || catalog == null || dto == null || string.IsNullOrWhiteSpace(dto.contentId))
            {
                return;
            }

            if (!catalog.TryGetCard(dto.contentId.Trim(), out var card) || card?.EffectIds == null)
            {
                return;
            }

            for (var i = 0; i < card.EffectIds.Count; i++)
            {
                var effectId = card.EffectIds[i];
                if (string.IsNullOrWhiteSpace(effectId))
                {
                    continue;
                }

                if (!catalog.TryGetEffect(effectId.Trim(), out var effect)
                    || effect == null
                    || string.IsNullOrWhiteSpace(effect.DesignText))
                {
                    continue;
                }

                AddUniqueLine(blocks, effect.DesignText.Trim());
            }
        }

        private static void AppendSkillBlocks(
            List<string> blocks,
            CardPresentationConfigDto dto,
            GameContentCatalog catalog)
        {
            if (dto?.skillIds == null || dto.skillIds.Length == 0)
            {
                return;
            }

            for (var i = 0; i < dto.skillIds.Length; i++)
            {
                var skillId = dto.skillIds[i];
                if (string.IsNullOrWhiteSpace(skillId))
                {
                    continue;
                }

                skillId = skillId.Trim();
                var title = skillId;
                if (catalog != null && catalog.TryGetSkill(skillId, out var skill) && skill != null
                    && !string.IsNullOrWhiteSpace(skill.DisplayName))
                {
                    title = skill.DisplayName.Trim();
                }
                else if (CardPresentationConfigCatalog.TryGet(skillId, out var skillDtoForName)
                         && skillDtoForName != null
                         && !string.IsNullOrWhiteSpace(skillDtoForName.displayName))
                {
                    title = skillDtoForName.displayName.Trim();
                }

                var detailLines = new List<string>(4);
                CollectSkillDetailLines(detailLines, skillId, catalog);

                if (detailLines.Count == 0)
                {
                    blocks.Add("【" + title + "】");
                }
                else
                {
                    var body = new StringBuilder(128);
                    body.Append("【").Append(title).Append("】");
                    for (var line = 0; line < detailLines.Count; line++)
                    {
                        body.Append('\n').Append(detailLines[line]);
                    }

                    blocks.Add(body.ToString());
                }
            }
        }

        /// <summary>
        /// 技能详述：只收效果模板 / 已解析效果的 <c>design_text</c>，
        /// 不用 skill JSON <c>description</c>（那是卡面简要）。
        /// </summary>
        private static void CollectSkillDetailLines(
            List<string> lines,
            string skillId,
            GameContentCatalog catalog)
        {
            if (CardPresentationConfigCatalog.TryGet(skillId, out var skillDto)
                && skillDto?.effectAssemblies != null
                && skillDto.effectAssemblies.Length > 0)
            {
                AppendAssemblyLines(lines, skillDto.effectAssemblies, catalog);
                if (lines.Count > 0)
                {
                    return;
                }
            }

            if (catalog == null
                || !catalog.TryGetSkill(skillId, out var skill)
                || skill?.EffectIds == null
                || skill.EffectIds.Count == 0)
            {
                return;
            }

            for (var i = 0; i < skill.EffectIds.Count; i++)
            {
                var effectId = skill.EffectIds[i];
                if (string.IsNullOrWhiteSpace(effectId))
                {
                    continue;
                }

                if (!catalog.TryGetEffect(effectId.Trim(), out var effect)
                    || effect == null
                    || string.IsNullOrWhiteSpace(effect.DesignText))
                {
                    continue;
                }

                AddUniqueLine(lines, effect.DesignText.Trim());
            }
        }

        private static void AppendAssemblyBlocks(
            List<string> blocks,
            EffectAssemblyDto[] assemblies,
            GameContentCatalog catalog)
        {
            var lines = new List<string>(8);
            AppendAssemblyLines(lines, assemblies, catalog);
            for (var i = 0; i < lines.Count; i++)
            {
                blocks.Add(lines[i]);
            }
        }

        private static void AppendAssemblyLines(
            List<string> lines,
            EffectAssemblyDto[] assemblies,
            GameContentCatalog catalog)
        {
            if (assemblies == null || assemblies.Length == 0)
            {
                return;
            }

            for (var i = 0; i < assemblies.Length; i++)
            {
                var assembly = assemblies[i];
                if (assembly == null || string.IsNullOrWhiteSpace(assembly.templateId))
                {
                    continue;
                }

                var templateId = assembly.templateId.Trim();
                string design = null;
                string bodyJson = null;

                if (catalog != null
                    && !string.IsNullOrWhiteSpace(assembly.id)
                    && catalog.TryGetEffect(assembly.id.Trim(), out var mounted)
                    && mounted != null
                    && !string.IsNullOrWhiteSpace(mounted.DesignText))
                {
                    // 已投影进 Catalog 的挂载效果：DesignText 已按实参参数化。
                    AddUniqueLine(lines, mounted.DesignText.Trim());
                    continue;
                }

                if (EffectTemplateCatalog.TryGet(templateId, out var template) && template != null)
                {
                    design = template.DesignText;
                    bodyJson = template.BodyJson;
                }
                else if (catalog != null
                         && catalog.TryGetEffect(templateId, out var effect)
                         && effect != null)
                {
                    design = effect.DesignText;
                    bodyJson = effect.Json;
                }

                if (string.IsNullOrWhiteSpace(design))
                {
                    continue;
                }

                var withTokens = EffectDesignTextParameterizer.Parameterize(
                    design,
                    bodyJson,
                    assembly.argsJson);
                var filled = CardFaceDescriptionParamFiller.Fill(
                    withTokens,
                    EffectAssemblyResolver.ParseArgsJson(assembly.argsJson));
                if (string.IsNullOrWhiteSpace(filled))
                {
                    continue;
                }

                AddUniqueLine(lines, filled.Trim());
            }
        }

        private static void AddUniqueLine(List<string> lines, string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            for (var i = 0; i < lines.Count; i++)
            {
                if (string.Equals(lines[i], line, StringComparison.Ordinal))
                {
                    return;
                }
            }

            lines.Add(line);
        }
    }
}
