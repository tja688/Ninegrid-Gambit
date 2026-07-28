using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[Serializable]
public class CardRowData
{
    public string def_id;
    public string display_name;
    public string kind;
    public string rarity;
    public int price;
    public int level;
    public bool is_elite;
    public bool is_boss;
    public bool is_reserve;
    public string deck_id;
    public int max_hp;
    public int attack;
    public int armor;
    public int recovery;
    public string tags;
    public string effect_ids;
    public string skill_ids;
}

[Serializable]
public class CardEditorResult
{
    public bool success;
    public string error;
    public List<CardRowData> cards;
    public List<string> effectIds;
    public List<string> skillIds;
}

/// <summary>
/// Locus 卡牌编辑器桥：读一卡一文件 JSON + tables/effects（#69，不再读 StreamingAssets tablenine_tb*）。
/// </summary>
public static class CardEditorApi
{
    private static string CardsFolder =>
        Path.Combine(Application.dataPath, "Arts", "ContentVisual", "cards");

    private static string EffectsPath =>
        Path.Combine(Application.dataPath, "Arts", "ContentVisual", "tables", "effects.json");

    public static CardEditorResult ReadCards()
    {
        var result = new CardEditorResult { success = false };

        try
        {
            var cardsFolder = CardsFolder;
            if (!Directory.Exists(cardsFolder))
            {
                result.error = $"Cards folder not found: {cardsFolder}";
                return result;
            }

            var cards = new List<CardRowData>();
            foreach (var file in Directory.GetFiles(cardsFolder, "*.json", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(file);
                if (string.Equals(name, "_index.json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var dto = JsonUtility.FromJson<CardPresentationLiteDto>(File.ReadAllText(file));
                if (dto == null || string.IsNullOrWhiteSpace(dto.contentId))
                {
                    continue;
                }

                // 仅卡类进编辑列表（Skill/Relic/Deck/Room 仍可在 effect/skill 引用里出现）
                if (!IsEditableCardKind(dto.kind))
                {
                    continue;
                }

                cards.Add(ToRow(dto));
            }

            result.cards = cards.OrderBy(c => c.def_id, StringComparer.Ordinal).ToList();

            var effectIds = new List<string>();
            if (File.Exists(EffectsPath))
            {
                var wrapped = "{\"items\":" + File.ReadAllText(EffectsPath) + "}";
                var effects = JsonUtility.FromJson<IdListWrapper>(wrapped);
                if (effects?.items != null)
                {
                    effectIds = effects.items
                        .Where(e => e != null && !string.IsNullOrEmpty(e.id))
                        .Select(e => e.id)
                        .ToList();
                }
            }

            result.effectIds = effectIds;

            var skillIds = new List<string>();
            foreach (var file in Directory.GetFiles(cardsFolder, "skill_*.json", SearchOption.TopDirectoryOnly))
            {
                var dto = JsonUtility.FromJson<CardPresentationLiteDto>(File.ReadAllText(file));
                if (dto != null && !string.IsNullOrWhiteSpace(dto.contentId))
                {
                    skillIds.Add(dto.contentId.Trim());
                }
            }

            result.skillIds = skillIds.Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToList();
            result.success = true;
        }
        catch (Exception ex)
        {
            result.error = ex.Message;
        }

        return result;
    }

    public static CardEditorResult WriteCards(List<CardRowData> cards)
    {
        var result = new CardEditorResult { success = false };

        try
        {
            var cardsFolder = CardsFolder;
            if (!Directory.Exists(cardsFolder))
            {
                result.error = $"Cards folder not found: {cardsFolder}";
                return result;
            }

            if (cards == null)
            {
                result.error = "cards is null";
                return result;
            }

            for (var i = 0; i < cards.Count; i++)
            {
                var row = cards[i];
                if (row == null || string.IsNullOrWhiteSpace(row.def_id))
                {
                    continue;
                }

                var path = FindCardFile(cardsFolder, row.def_id);
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    result.error = $"Card JSON not found for {row.def_id}";
                    return result;
                }

                var dto = JsonUtility.FromJson<CardPresentationLiteDto>(File.ReadAllText(path));
                if (dto == null)
                {
                    result.error = $"Failed to parse {path}";
                    return result;
                }

                ApplyRow(dto, row);
                File.WriteAllText(path, JsonUtility.ToJson(dto, true) + "\n");
            }

            result.success = true;
        }
        catch (Exception ex)
        {
            result.error = ex.Message;
        }

        return result;
    }

    private static bool IsEditableCardKind(string kind)
    {
        return string.Equals(kind, "HelpCard", StringComparison.OrdinalIgnoreCase)
               || string.Equals(kind, "Monster", StringComparison.OrdinalIgnoreCase)
               || string.Equals(kind, "PlayerCard", StringComparison.OrdinalIgnoreCase)
               || string.Equals(kind, "Item", StringComparison.OrdinalIgnoreCase)
               || string.Equals(kind, "Avatar", StringComparison.OrdinalIgnoreCase)
               || string.Equals(kind, "ChoiceOption", StringComparison.OrdinalIgnoreCase);
    }

    private static string FindCardFile(string folder, string contentId)
    {
        var underscored = contentId.Replace('.', '_') + ".json";
        var direct = Path.Combine(folder, underscored);
        if (File.Exists(direct))
        {
            return direct;
        }

        foreach (var file in Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly))
        {
            if (string.Equals(Path.GetFileName(file), "_index.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var dto = JsonUtility.FromJson<CardPresentationLiteDto>(File.ReadAllText(file));
            if (dto != null && string.Equals(dto.contentId, contentId, StringComparison.Ordinal))
            {
                return file;
            }
        }

        return null;
    }

    private static CardRowData ToRow(CardPresentationLiteDto dto)
    {
        return new CardRowData
        {
            def_id = dto.contentId ?? string.Empty,
            display_name = dto.displayName ?? string.Empty,
            kind = dto.kind ?? string.Empty,
            rarity = dto.rarity ?? string.Empty,
            price = dto.gold,
            level = dto.level,
            is_elite = dto.isElite,
            is_boss = dto.isBoss,
            is_reserve = dto.isReserve,
            deck_id = dto.deckId ?? string.Empty,
            max_hp = dto.stats != null ? dto.stats.hp : 0,
            attack = dto.stats != null ? dto.stats.attack : 0,
            armor = dto.stats != null ? dto.stats.armor : 0,
            recovery = dto.stats != null ? dto.stats.recovery : 0,
            tags = JoinTokens(dto.tags),
            effect_ids = JoinTokens(dto.effectIds),
            skill_ids = JoinTokens(dto.skillIds),
        };
    }

    private static void ApplyRow(CardPresentationLiteDto dto, CardRowData row)
    {
        dto.displayName = row.display_name ?? string.Empty;
        dto.kind = row.kind ?? dto.kind;
        dto.rarity = row.rarity ?? string.Empty;
        dto.gold = row.price;
        dto.level = row.level;
        dto.isElite = row.is_elite;
        dto.isBoss = row.is_boss;
        dto.isReserve = row.is_reserve;
        dto.deckId = row.deck_id ?? string.Empty;
        dto.stats ??= new CardPresentationLiteStatsDto();
        dto.stats.hp = row.max_hp;
        dto.stats.attack = row.attack;
        dto.stats.armor = row.armor;
        dto.stats.recovery = row.recovery;
        dto.tags = SplitTokens(row.tags);
        dto.effectIds = SplitTokens(row.effect_ids);
        dto.skillIds = SplitTokens(row.skill_ids);
        if (dto.schemaVersion < 2)
        {
            dto.schemaVersion = 2;
        }
    }

    private static string JoinTokens(string[] values)
    {
        if (values == null || values.Length == 0)
        {
            return string.Empty;
        }

        return string.Join(";", values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()));
    }

    private static string[] SplitTokens(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<string>();
        }

        return raw.Split(new[] { ';', '|', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(v => v.Trim())
            .Where(v => v.Length > 0)
            .ToArray();
    }

    [Serializable]
    private class CardPresentationLiteDto
    {
        public int schemaVersion = 1;
        public string contentId;
        public string kind;
        public string deckId;
        public string displayName;
        public string description;
        public int gold;
        public CardPresentationLiteStatsDto stats;
        public CardPresentationLiteSpritesDto sprites;
        public CardPresentationLiteMainVisualDto mainVisual;
        public CardPresentationLiteAnimationsDto animations;
        public CardPresentationLiteExtraSlotDto[] extraSlots;
        public string rarity;
        public string[] tags;
        public string[] effectIds;
        public string[] skillIds;
        public int level;
        public bool isElite;
        public bool isBoss;
        public bool isReserve;
        public string containerType;
        public string deckKind;
        public string[] monsterDefIds;
        public int weight;
        public int goldDelta;
        public int maxHpDelta;
        public bool healToFull;
        public string rewardPoolId;
        public int shopOfferCount;
    }

    [Serializable]
    private class CardPresentationLiteStatsDto
    {
        public int hp;
        public int armor;
        public int attack;
        public int action;
        public int recovery;
    }

    [Serializable]
    private class CardPresentationLiteSpritesDto
    {
        public string mainIcon;
        public string faceBackground;
        public string cardFrame;
        public string banner;
        public string backBorder;
        public string backShirt;
        public string backLogo;
    }

    [Serializable]
    private class CardPresentationLiteMainVisualDto
    {
        public float offsetX;
        public float offsetY;
        public float uniformScale = 1f;
    }

    [Serializable]
    private class CardPresentationLiteAnimationsDto
    {
        public float defaultFps = 8f;
        public CardPresentationLiteAnimSlotDto[] slots;
    }

    [Serializable]
    private class CardPresentationLiteAnimSlotDto
    {
        public string id;
        public string sourceType;
        public string path;
        public float offsetX;
        public float offsetY;
    }

    [Serializable]
    private class CardPresentationLiteExtraSlotDto
    {
        public string code;
        public string path;
    }

    [Serializable]
    private class IdItem
    {
        public string id;
        public string def_id;
    }

    [Serializable]
    private class IdListWrapper
    {
        public List<IdItem> items;
    }
}
