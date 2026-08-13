using System;
using System.Collections.Generic;
using NineGrid.Cards.Presentation;
using TMPro;
using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 仅挂在右键详述预览卡面上：hover 描述里的 <c>[code]</c> 内联 sprite，
    /// 或卡面上的机制图标（攻/甲/血、倒计时、攻击模式、同步子图标）时，
    /// 把对应词条填入词条栏首行的可复用解释槽（ADR-0037）；移出恢复提示。
    /// 卡面图标不长命中体，命中按世界 AABB 计算，与棋盘命中路由无关。
    /// 战斗内场上卡面不挂此行为。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardInspectIconHover : MonoBehaviour
    {
        private const string InlineKeyPrefix = "code:";
        private const string FaceKeyPrefix = "term:";

        private readonly List<CardFaceIconGlossaryTargets.Target> _faceTargets =
            new List<CardFaceIconGlossaryTargets.Target>();

        private TMP_Text _description;
        private CardInspectGlossaryListView _list;
        private CardFaceDescriptionIconCatalogSO _catalog;
        private Camera _uiCamera;
        private string _activeKey;

        public void Configure(
            TMP_Text description,
            CardInspectGlossaryListView list,
            CardFaceDescriptionIconCatalogSO catalog,
            Camera uiCamera,
            CardPresentationSnapshot snapshot)
        {
            _description = description;
            _list = list;
            _catalog = catalog;
            _uiCamera = uiCamera;
            _activeKey = null;
            CardFaceIconGlossaryTargets.Collect(transform, snapshot, _faceTargets);
        }

        private void Update()
        {
            if (_list == null || !gameObject.activeInHierarchy)
            {
                return;
            }

            if (TryFindHoveredInlineCode(out var code))
            {
                ShowInlineCode(code);
                return;
            }

            if (TryFindHoveredFaceTermName(out var termName))
            {
                ShowFaceTerm(termName);
                return;
            }

            if (_activeKey != null)
            {
                _activeKey = null;
                _list.ClearHover();
            }
        }

        private void ShowInlineCode(string code)
        {
            var key = InlineKeyPrefix + code;
            if (string.Equals(_activeKey, key, StringComparison.Ordinal))
            {
                return;
            }

            _activeKey = key;
            if (_catalog != null && _catalog.TryGetByCode(code, out var entry) && entry != null)
            {
                ShowEntry(entry, code);
                return;
            }

            ShowUnconfigured(code);
        }

        private void ShowFaceTerm(string termName)
        {
            var key = FaceKeyPrefix + termName;
            if (string.Equals(_activeKey, key, StringComparison.Ordinal))
            {
                return;
            }

            _activeKey = key;
            if (_catalog != null && _catalog.TryGetByDisplayName(termName, out var entry) && entry != null)
            {
                ShowEntry(entry, termName);
                return;
            }

            ShowUnconfigured(termName);
        }

        private void ShowEntry(CardFaceDescriptionIconCatalogSO.Entry entry, string fallbackName)
        {
            // hover 词条名与解释按当前语言取 glossary 表（键 = zh 名），缺翻译回中文（ADR-0046）。
            var name = fallbackName;
            var explanation = entry.explanation ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(entry.displayNameZh))
            {
                var zhName = entry.displayNameZh.Trim();
                name = NineGrid.Core.Localization.LocalizationCatalog
                    .ResolveGlossaryDisplayName(zhName);
                explanation = NineGrid.Core.Localization.LocalizationCatalog
                    .ResolveGlossaryIntro(zhName, explanation);
            }

            if (string.IsNullOrWhiteSpace(explanation))
            {
                explanation = NineGrid.Core.Localization.L10n.Tr(
                    "inspect.term_no_intro",
                    "（暂无介绍）");
            }

            _list.ShowHoverTerm(name, explanation, entry.HasColorOverride, entry.color);
        }

        private void ShowUnconfigured(string displayName)
        {
            _list.ShowHoverTerm(
                displayName,
                NineGrid.Core.Localization.L10n.Tr("inspect.term_unconfigured", "（未配置词条介绍）"),
                hasColor: false,
                color: default);
        }

        /// <summary>
        /// 卡面机制图标命中：世界 AABB 包含指针即命中；多个相交时取面积最小者（更贴合的那个）。
        /// </summary>
        private bool TryFindHoveredFaceTermName(out string termName)
        {
            termName = null;
            if (_faceTargets.Count == 0)
            {
                return false;
            }

            if (!WorldPointerUtility.TryGetPointerWorld(_uiCamera, out var world))
            {
                return false;
            }

            var bestArea = float.MaxValue;
            for (var i = 0; i < _faceTargets.Count; i++)
            {
                var target = _faceTargets[i];
                var renderer = target.Renderer;
                if (renderer == null
                    || !renderer.enabled
                    || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var bounds = renderer.bounds;
                if (world.x < bounds.min.x
                    || world.x > bounds.max.x
                    || world.y < bounds.min.y
                    || world.y > bounds.max.y)
                {
                    continue;
                }

                var area = bounds.size.x * bounds.size.y;
                if (area < bestArea)
                {
                    bestArea = area;
                    termName = target.TermName;
                }
            }

            return !string.IsNullOrEmpty(termName);
        }

        private bool TryFindHoveredInlineCode(out string code)
        {
            code = null;
            if (_description == null
                || _description.textInfo == null
                || !_description.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (!WorldPointerUtility.TryGetPointerScreen(out var screen))
            {
                return false;
            }

            _description.ForceMeshUpdate(true);
            var cam = WorldPointerUtility.ResolveCamera(_uiCamera);
            if (!TMP_TextUtilities.IsIntersectingRectTransform(_description.rectTransform, screen, cam))
            {
                return false;
            }

            var charIndex = TMP_TextUtilities.FindNearestCharacter(_description, screen, cam, visibleOnly: true);
            if (charIndex < 0 || charIndex >= _description.textInfo.characterCount)
            {
                return false;
            }

            var info = _description.textInfo.characterInfo[charIndex];
            if (!info.isVisible || info.elementType != TMP_TextElementType.Sprite)
            {
                return false;
            }

            // sprite 名存在 rich text 源串；从 character 的 string 索引切片取 <sprite name="…">。
            var source = _description.text;
            if (string.IsNullOrEmpty(source) || info.index < 0 || info.index >= source.Length)
            {
                return false;
            }

            var sliceStart = info.index;
            // 向前找最近的 <sprite
            var tagStart = source.LastIndexOf("<sprite", sliceStart, StringComparison.OrdinalIgnoreCase);
            if (tagStart < 0)
            {
                tagStart = sliceStart;
            }

            var sliceEnd = Mathf.Min(source.Length, tagStart + 96);
            var slice = source.Substring(tagStart, sliceEnd - tagStart);
            const string marker = "name=\"";
            var nameAt = slice.IndexOf(marker, StringComparison.Ordinal);
            if (nameAt < 0)
            {
                return false;
            }

            var valueStart = nameAt + marker.Length;
            var valueEnd = slice.IndexOf('"', valueStart);
            if (valueEnd <= valueStart)
            {
                return false;
            }

            code = slice.Substring(valueStart, valueEnd - valueStart);
            return !string.IsNullOrEmpty(code);
        }
    }
}
