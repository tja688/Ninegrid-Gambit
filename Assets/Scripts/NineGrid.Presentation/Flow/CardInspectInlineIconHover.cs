using System;
using NineGrid.Cards.Presentation;
using TMPro;
using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 仅挂在右键详述预览卡面上：hover <c>Basic_Description</c> 内联 sprite 字符时，
    /// 把对应图标词条填入可复用解释槽（ADR-0037）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardInspectInlineIconHover : MonoBehaviour
    {
        private TMP_Text _description;
        private CardInspectGlossaryListView _list;
        private CardFaceDescriptionIconCatalogSO _catalog;
        private Camera _uiCamera;
        private string _activeCode;

        public void Configure(
            TMP_Text description,
            CardInspectGlossaryListView list,
            CardFaceDescriptionIconCatalogSO catalog,
            Camera uiCamera)
        {
            _description = description;
            _list = list;
            _catalog = catalog;
            _uiCamera = uiCamera;
            _activeCode = null;
        }

        private void Update()
        {
            if (_description == null || _list == null || !_description.gameObject.activeInHierarchy)
            {
                return;
            }

            if (!TryFindHoveredSpriteCode(out var code))
            {
                if (_activeCode != null)
                {
                    _activeCode = null;
                    _list.ClearHover();
                }

                return;
            }

            if (string.Equals(_activeCode, code, StringComparison.Ordinal))
            {
                return;
            }

            _activeCode = code;
            if (_catalog != null
                && _catalog.TryGetByCode(code, out var entry)
                && entry != null)
            {
                var name = string.IsNullOrWhiteSpace(entry.displayNameZh)
                    ? code
                    : entry.displayNameZh.Trim();
                var explanation = entry.explanation ?? string.Empty;
                if (string.IsNullOrWhiteSpace(explanation))
                {
                    explanation = "（暂无介绍）";
                }

                _list.ShowHoverTerm(name, explanation, entry.HasColorOverride, entry.color);
            }
            else
            {
                _list.ShowHoverTerm(code, "（未配置词条介绍）", hasColor: false, color: default);
            }
        }

        private bool TryFindHoveredSpriteCode(out string code)
        {
            code = null;
            if (_description == null || _description.textInfo == null)
            {
                return false;
            }

            _description.ForceMeshUpdate(true);
            var cam = _uiCamera != null ? _uiCamera : Camera.main;
            var screen = Input.mousePosition;
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
