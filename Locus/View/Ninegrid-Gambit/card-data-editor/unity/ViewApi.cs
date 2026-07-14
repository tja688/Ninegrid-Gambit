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

public static class CardEditorApi
{
    private static string CardJsonPath => Path.Combine(Application.streamingAssetsPath, "TableNine/LubanData/tablenine_tbcard.json");
    private static string EffectJsonPath => Path.Combine(Application.streamingAssetsPath, "TableNine/LubanData/tablenine_tbeffect.json");
    private static string SkillJsonPath => Path.Combine(Application.streamingAssetsPath, "TableNine/LubanData/tablenine_tbskill.json");

    public static CardEditorResult ReadCards()
    {
        var result = new CardEditorResult { success = false };

        try
        {
            var cards = new List<CardRowData>();
            var cardPath = CardJsonPath;
            var effectPath = EffectJsonPath;
            var skillPath = SkillJsonPath;

            if (!File.Exists(cardPath))
            {
                result.error = $"Card JSON not found: {cardPath}";
                return result;
            }

            string cardJson = File.ReadAllText(cardPath);
            var rawCards = JsonUtility.FromJson<CardListWrapper>("{\"items\":" + cardJson + "}");

            if (rawCards?.items != null)
            {
                foreach (var raw in rawCards.items)
                    cards.Add(raw);
            }

            result.cards = cards;

            // Read effect IDs for reference
            var effectIds = new List<string>();
            if (File.Exists(effectPath))
            {
                string effectJson = File.ReadAllText(effectPath);
                var rawEffects = JsonUtility.FromJson<IdListWrapper>("{\"items\":" + effectJson + "}");
                if (rawEffects?.items != null)
                    effectIds = rawEffects.items.Select(e => e.id).ToList();
            }
            result.effectIds = effectIds;

            // Read skill IDs for reference
            var skillIds = new List<string>();
            if (File.Exists(skillPath))
            {
                string skillJson = File.ReadAllText(skillPath);
                var rawSkills = JsonUtility.FromJson<IdListWrapper>("{\"items\":" + skillJson + "}");
                if (rawSkills?.items != null)
                    skillIds = rawSkills.items.Select(s => s.def_id).ToList();
            }
            result.skillIds = skillIds;

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
            var cardPath = CardJsonPath;

            if (!File.Exists(cardPath))
            {
                result.error = $"Card JSON not found: {cardPath}";
                return result;
            }

            // Build JSON array manually for consistent formatting
            var jsonLines = new List<string>();
            jsonLines.Add("[");

            for (int i = 0; i < cards.Count; i++)
            {
                var c = cards[i];
                bool last = i == cards.Count - 1;
                jsonLines.Add("  {");
                jsonLines.Add($"    \"def_id\": \"{EscapeJson(c.def_id)}\",");
                jsonLines.Add($"    \"display_name\": \"{EscapeJson(c.display_name)}\",");
                jsonLines.Add($"    \"kind\": \"{EscapeJson(c.kind)}\",");
                jsonLines.Add($"    \"rarity\": \"{EscapeJson(c.rarity)}\",");
                jsonLines.Add($"    \"price\": {c.price},");
                jsonLines.Add($"    \"level\": {c.level},");
                jsonLines.Add($"    \"is_elite\": {c.is_elite.ToString().ToLower()},");
                jsonLines.Add($"    \"is_boss\": {c.is_boss.ToString().ToLower()},");
                jsonLines.Add($"    \"is_reserve\": {c.is_reserve.ToString().ToLower()},");
                jsonLines.Add($"    \"deck_id\": \"{EscapeJson(c.deck_id)}\",");
                jsonLines.Add($"    \"max_hp\": {c.max_hp},");
                jsonLines.Add($"    \"attack\": {c.attack},");
                jsonLines.Add($"    \"armor\": {c.armor},");
                jsonLines.Add($"    \"recovery\": {c.recovery},");
                jsonLines.Add($"    \"tags\": \"{EscapeJson(c.tags)}\",");
                jsonLines.Add($"    \"effect_ids\": \"{EscapeJson(c.effect_ids)}\",");
                jsonLines.Add($"    \"skill_ids\": \"{EscapeJson(c.skill_ids)}\"");
                jsonLines.Add(last ? "  }" : "  },");
            }

            jsonLines.Add("]");
            File.WriteAllText(cardPath, string.Join("\n", jsonLines) + "\n");

            result.success = true;
        }
        catch (Exception ex)
        {
            result.error = ex.Message;
        }

        return result;
    }

    private static string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    [Serializable]
    private class CardListWrapper
    {
        public List<CardRowData> items;
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
