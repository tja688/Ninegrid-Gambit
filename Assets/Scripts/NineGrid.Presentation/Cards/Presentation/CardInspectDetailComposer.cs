using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Core.Content;

namespace NineGrid.Cards.Presentation
{
    /// <summary>
    /// 右键详述面板文案：背景介绍 / 牌组介绍。
    /// 效果信息区改由词条行驱动（ADR-0037），本 composer 不再产出 design_text 堆砌。
    /// </summary>
    public static class CardInspectDetailComposer
    {
        /// <summary>当局此卡上已激活的效果挂载（保留结构供测试/诊断兼容）。</summary>
        public readonly struct LiveEffectMount
        {
            public readonly string SourceDefId;
            public readonly string EffectId;

            public LiveEffectMount(string sourceDefId, string effectId)
            {
                SourceDefId = sourceDefId ?? string.Empty;
                EffectId = effectId ?? string.Empty;
            }
        }

        public readonly struct Result
        {
            public readonly string FaceIntro;
            public readonly string DeckIntro;

            public Result(string faceIntro, string deckIntro)
            {
                FaceIntro = faceIntro ?? string.Empty;
                DeckIntro = deckIntro ?? string.Empty;
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
            return new Result(faceIntro, deckIntro);
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
                // monster_decks.json display_name 与 deck JSON displayName 共用 deckId 键（ADR-0046）。
                var deckName = monsterDeck.DisplayName.Trim();
                if (NineGrid.Core.Localization.LocalizationCatalog.TryGetCardText(deckId, out var text)
                    && !string.IsNullOrWhiteSpace(text.DisplayName))
                {
                    deckName = text.DisplayName.Trim();
                }

                return deckName;
            }

            return deckId;
        }
    }
}
