using System;
using System.Collections.Generic;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;

namespace NineGrid.Cards.Presentation
{
    /// <summary>
    /// 右键详情词条行按卡种装配（ADR-0037）。
    /// </summary>
    public static class CardInspectGlossaryAssembler
    {
        public const string OrthogonalAttackTermName = "正交攻击";
        public const string DiagonalAttackTermName = "斜向攻击";
        public const string OmniAttackTermName = "全向攻击";

        private static readonly string[] OrthogonalAliases = { "普通近战", "普通攻击", OrthogonalAttackTermName };
        private static readonly string[] DiagonalAliases = { "斜角近战", "斜角攻击", DiagonalAttackTermName };
        private static readonly string[] OmniAliases = { "全向近战", OmniAttackTermName };

        public static List<CardGlossaryTerms.ResolvedTerm> Assemble(
            CardPresentationKind kind,
            CardPresentationSnapshot snapshot,
            IReadOnlyList<string> extraNames,
            CardFaceDescriptionIconCatalogSO catalog)
        {
            var description = snapshot != null ? snapshot.BasicDescription : null;
            var extras = extraNames ?? Array.Empty<string>();

            switch (kind)
            {
                case CardPresentationKind.Monster:
                    return AssembleMonster(snapshot, description, extras, catalog);
                case CardPresentationKind.Trap:
                    return AssembleTrap(snapshot, description, extras, catalog);
                case CardPresentationKind.HelpCard:
                case CardPresentationKind.Relic:
                case CardPresentationKind.Item:
                    return AssembleSelfThenIcons(snapshot, description, extras, catalog, injectRhythm: false);
                default:
                    return CardGlossaryTerms.BuildInspectTerms(description, extras, catalog);
            }
        }

        private static List<CardGlossaryTerms.ResolvedTerm> AssembleMonster(
            CardPresentationSnapshot snapshot,
            string description,
            IReadOnlyList<string> extras,
            CardFaceDescriptionIconCatalogSO catalog)
        {
            var result = CanonicalizeAttackPatternTerms(
                CardGlossaryTerms.ExtractExplicitTerms(description, catalog),
                catalog);
            var occupied = BuildOccupied(result);

            TryAppendAttackRange(snapshot, catalog, occupied, result);
            TryAppendRhythm(snapshot, catalog, occupied, result, requireActiveRhythm: true);
            AppendExtras(extras, catalog, occupied, result);
            return result;
        }

        private static List<CardGlossaryTerms.ResolvedTerm> AssembleTrap(
            CardPresentationSnapshot snapshot,
            string description,
            IReadOnlyList<string> extras,
            CardFaceDescriptionIconCatalogSO catalog)
        {
            return AssembleSelfThenIcons(snapshot, description, extras, catalog, injectRhythm: true);
        }

        private static List<CardGlossaryTerms.ResolvedTerm> AssembleSelfThenIcons(
            CardPresentationSnapshot snapshot,
            string description,
            IReadOnlyList<string> extras,
            CardFaceDescriptionIconCatalogSO catalog,
            bool injectRhythm)
        {
            var result = new List<CardGlossaryTerms.ResolvedTerm>();
            var occupied = new HashSet<string>(StringComparer.Ordinal);

            var displayName = snapshot != null ? snapshot.DisplayName : string.Empty;
            if (string.IsNullOrWhiteSpace(displayName) && snapshot != null)
            {
                displayName = snapshot.DefId;
            }

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                var body = ResolveSelfEffectBody(snapshot, description, catalog);
                var self = new CardGlossaryTerms.ResolvedTerm(
                    displayName.Trim(),
                    body,
                    matched: !string.IsNullOrWhiteSpace(body),
                    hasColor: false,
                    color: default,
                    lookupName: displayName.Trim());
                result.Add(self);
                Occupy(occupied, self);
            }

            if (injectRhythm)
            {
                TryAppendRhythm(snapshot, catalog, occupied, result, requireActiveRhythm: false);
            }

            var icons = CardGlossaryTerms.ExtractInlineIconTerms(description, catalog);
            for (var i = 0; i < icons.Count; i++)
            {
                TryAdd(icons[i], occupied, result);
            }

            AppendExtras(extras, catalog, occupied, result);
            return result;
        }

        private static string ResolveSelfEffectBody(
            CardPresentationSnapshot snapshot,
            string description,
            CardFaceDescriptionIconCatalogSO catalog)
        {
            var defId = snapshot != null ? snapshot.DefId : null;
            if (InspectEffectCopyCatalog.TryGet(defId, out var copy) && !string.IsNullOrWhiteSpace(copy))
            {
                return copy;
            }

            return CardGlossaryTerms.StripMarkupForReadableFallback(description, catalog);
        }

        private static List<CardGlossaryTerms.ResolvedTerm> CanonicalizeAttackPatternTerms(
            List<CardGlossaryTerms.ResolvedTerm> source,
            CardFaceDescriptionIconCatalogSO catalog)
        {
            if (source == null || source.Count == 0)
            {
                return source ?? new List<CardGlossaryTerms.ResolvedTerm>();
            }

            for (var i = 0; i < source.Count; i++)
            {
                var canonical = CanonicalAttackRangeName(source[i].LookupName);
                if (string.IsNullOrEmpty(canonical)
                    || string.Equals(canonical, source[i].LookupName, StringComparison.Ordinal))
                {
                    continue;
                }

                CardGlossaryTerms.ResolveTerm(canonical, catalog, out var resolved);
                source[i] = CardGlossaryTerms.AttachInlineCode(resolved, catalog);
            }

            return source;
        }

        private static void TryAppendAttackRange(
            CardPresentationSnapshot snapshot,
            CardFaceDescriptionIconCatalogSO catalog,
            HashSet<string> occupied,
            List<CardGlossaryTerms.ResolvedTerm> result)
        {
            if (snapshot == null)
            {
                return;
            }

            string termName;
            switch (snapshot.AttackPattern)
            {
                case AttackPattern.OrthogonalMelee:
                    termName = OrthogonalAttackTermName;
                    break;
                case AttackPattern.DiagonalMelee:
                    termName = DiagonalAttackTermName;
                    break;
                case AttackPattern.OmnidirectionalMelee:
                    termName = OmniAttackTermName;
                    break;
                default:
                    return;
            }

            CardGlossaryTerms.ResolveTerm(termName, catalog, out var resolved);
            TryAdd(CardGlossaryTerms.AttachInlineCode(resolved, catalog), occupied, result);
        }

        private static void TryAppendRhythm(
            CardPresentationSnapshot snapshot,
            CardFaceDescriptionIconCatalogSO catalog,
            HashSet<string> occupied,
            List<CardGlossaryTerms.ResolvedTerm> result,
            bool requireActiveRhythm)
        {
            if (snapshot == null)
            {
                return;
            }

            var hasRhythm = snapshot.HasActiveRhythm
                || (!requireActiveRhythm && snapshot.ShowActionCount);
            if (!hasRhythm)
            {
                return;
            }

            var termName = ResolveRhythmTermName(snapshot);
            if (string.IsNullOrEmpty(termName))
            {
                return;
            }

            CardGlossaryTerms.ResolveTerm(termName, catalog, out var resolved);
            TryAdd(CardGlossaryTerms.AttachInlineCode(resolved, catalog), occupied, result);
        }

        private static string ResolveRhythmTermName(CardPresentationSnapshot snapshot)
        {
            var defId = snapshot != null ? snapshot.DefId : null;
            if (!string.IsNullOrWhiteSpace(defId)
                && CardPresentationConfigCatalog.TryGet(defId.Trim(), out var dto)
                && dto != null
                && CardRhythmRules.TryParse(dto.rhythmSource, out var source)
                && source == CardRhythmSource.Move)
            {
                return CardRhythmRules.TokenMove;
            }

            return CardRhythmRules.TokenAction;
        }

        private static void AppendExtras(
            IReadOnlyList<string> extras,
            CardFaceDescriptionIconCatalogSO catalog,
            HashSet<string> occupied,
            List<CardGlossaryTerms.ResolvedTerm> result)
        {
            if (extras == null)
            {
                return;
            }

            for (var i = 0; i < extras.Count; i++)
            {
                var name = (extras[i] ?? string.Empty).Trim();
                if (name.Length == 0)
                {
                    continue;
                }

                CardGlossaryTerms.ResolveTerm(name, catalog, out var resolved);
                TryAdd(CardGlossaryTerms.AttachInlineCode(resolved, catalog), occupied, result);
            }
        }

        private static HashSet<string> BuildOccupied(List<CardGlossaryTerms.ResolvedTerm> terms)
        {
            var occupied = new HashSet<string>(StringComparer.Ordinal);
            if (terms == null)
            {
                return occupied;
            }

            for (var i = 0; i < terms.Count; i++)
            {
                Occupy(occupied, terms[i]);
            }

            return occupied;
        }

        private static void TryAdd(
            CardGlossaryTerms.ResolvedTerm term,
            HashSet<string> occupied,
            List<CardGlossaryTerms.ResolvedTerm> result)
        {
            var key = !string.IsNullOrWhiteSpace(term.LookupName) ? term.LookupName : term.DisplayName;
            if (string.IsNullOrWhiteSpace(key) || IsOccupied(occupied, key))
            {
                return;
            }

            Occupy(occupied, term);
            result.Add(term);
        }

        private static bool IsOccupied(HashSet<string> occupied, string name)
        {
            if (occupied.Contains(name))
            {
                return true;
            }

            var aliases = AliasesFor(name);
            if (aliases == null)
            {
                return false;
            }

            for (var i = 0; i < aliases.Length; i++)
            {
                if (occupied.Contains(aliases[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static void Occupy(HashSet<string> occupied, CardGlossaryTerms.ResolvedTerm term)
        {
            OccupyName(occupied, term.LookupName);
            OccupyName(occupied, term.DisplayName);
        }

        private static void OccupyName(HashSet<string> occupied, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            name = name.Trim();
            occupied.Add(name);
            var aliases = AliasesFor(name);
            if (aliases == null)
            {
                return;
            }

            for (var i = 0; i < aliases.Length; i++)
            {
                occupied.Add(aliases[i]);
            }
        }

        public static string CanonicalAttackRangeName(string lookupName)
        {
            if (string.IsNullOrWhiteSpace(lookupName))
            {
                return null;
            }

            switch (lookupName.Trim())
            {
                case "普通近战":
                case "普通攻击":
                case OrthogonalAttackTermName:
                    return OrthogonalAttackTermName;
                case "斜角近战":
                case "斜角攻击":
                case DiagonalAttackTermName:
                    return DiagonalAttackTermName;
                case "全向近战":
                case OmniAttackTermName:
                    return OmniAttackTermName;
                default:
                    return lookupName.Trim();
            }
        }

        public static string[] AliasesFor(string name)
        {
            switch (name)
            {
                case "普通近战":
                case "普通攻击":
                case OrthogonalAttackTermName:
                    return OrthogonalAliases;
                case "斜角近战":
                case "斜角攻击":
                case DiagonalAttackTermName:
                    return DiagonalAliases;
                case "全向近战":
                case OmniAttackTermName:
                    return OmniAliases;
                default:
                    return null;
            }
        }
    }
}
