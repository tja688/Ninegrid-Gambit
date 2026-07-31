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
            var skills = ResolveSkillDetails(dto, snapshot, catalog, assemblies);
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
            CardPresentationSnapshot snapshot,
            GameContentCatalog catalog,
            EffectAssemblyDto[] assemblies)
        {
            var blocks = new List<string>(8);

            AppendSkillBlocks(blocks, dto, catalog);
            AppendAssemblyBlocks(blocks, assemblies, catalog);

            if (blocks.Count == 0)
            {
                var fallback = snapshot != null ? snapshot.DetailDescription : null;
                if (string.IsNullOrWhiteSpace(fallback) && snapshot != null)
                {
                    fallback = snapshot.BasicDescription;
                }

                if (string.IsNullOrWhiteSpace(fallback) && dto != null)
                {
                    fallback = CardFaceDescriptionParamFiller.FillFromAssemblies(
                        dto.description,
                        assemblies);
                }

                if (!string.IsNullOrWhiteSpace(fallback))
                {
                    blocks.Add(CardDetailDescriptionComposer.Compose(
                        fallback.Trim(),
                        CardFacePresentationBinder.PeekDescriptionIconCatalog()));
                }
            }

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
                string title = skillId;
                string body = string.Empty;

                if (catalog != null && catalog.TryGetSkill(skillId, out var skill) && skill != null)
                {
                    if (!string.IsNullOrWhiteSpace(skill.DisplayName))
                    {
                        title = skill.DisplayName.Trim();
                    }

                    body = skill.DesignText ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(body)
                    && CardPresentationConfigCatalog.TryGet(skillId, out var skillDto)
                    && skillDto != null)
                {
                    if (!string.IsNullOrWhiteSpace(skillDto.displayName))
                    {
                        title = skillDto.displayName.Trim();
                    }

                    body = CardFaceDescriptionParamFiller.FillFromAssemblies(
                        skillDto.description,
                        skillDto.effectAssemblies);
                }

                if (string.IsNullOrWhiteSpace(body))
                {
                    blocks.Add("【" + title + "】");
                }
                else
                {
                    blocks.Add("【" + title + "】\n" + body.Trim());
                }
            }
        }

        private static void AppendAssemblyBlocks(
            List<string> blocks,
            EffectAssemblyDto[] assemblies,
            GameContentCatalog catalog)
        {
            if (assemblies == null || assemblies.Length == 0 || catalog == null)
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
                if (!catalog.TryGetEffect(templateId, out var effect) || effect == null)
                {
                    continue;
                }

                var design = effect.DesignText ?? string.Empty;
                if (string.IsNullOrWhiteSpace(design))
                {
                    continue;
                }

                var withTokens = EffectDesignTextParameterizer.Parameterize(
                    design,
                    effect.Json,
                    assembly.argsJson);
                var filled = CardFaceDescriptionParamFiller.Fill(
                    withTokens,
                    EffectAssemblyResolver.ParseArgsJson(assembly.argsJson));
                if (string.IsNullOrWhiteSpace(filled))
                {
                    continue;
                }

                blocks.Add(filled.Trim());
            }
        }
    }
}
