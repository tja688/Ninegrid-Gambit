using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NineGrid.Cards.Presentation
{
    /// <summary>
    /// 右键详情 ScrollView Content：按词条动态装行 + 一条可复用 hover 槽（ADR-0037）。
    /// hover 槽恒在首行——列表可滚动，落在末尾时词条多了就会滚出视野。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardInspectGlossaryListView : MonoBehaviour
    {
        public static string HoverHintText => NineGrid.Core.Localization.L10n.Tr(
            "inspect.hover_hint",
            "将鼠标移到卡面预览中的图标上，可查看其含义。");

        [SerializeField] private RectTransform content;
        [SerializeField] private CardInspectGlossaryRowView rowPrefab;
        [SerializeField] private CardInspectGlossaryRowView hoverRow;

        private readonly List<CardInspectGlossaryRowView> _spawned = new List<CardInspectGlossaryRowView>();

        public void Configure(RectTransform contentRoot, CardInspectGlossaryRowView prefab)
        {
            content = contentRoot;
            rowPrefab = prefab;
            EnsureLayout();
            EnsureHoverRow();
        }

        public void BindExplicitTerms(IReadOnlyList<CardGlossaryTerms.ResolvedTerm> terms)
        {
            EnsureLayout();
            ClearSpawned();
            EnsureHoverRow();

            if (terms != null && rowPrefab != null && content != null)
            {
                for (var i = 0; i < terms.Count; i++)
                {
                    var term = terms[i];
                    var row = Instantiate(rowPrefab, content);
                    row.gameObject.SetActive(true);
                    row.name = "词条行_" + term.DisplayName;
                    row.EnsureLayoutElement();
                    row.Bind(term.DisplayName, term.Explanation, term.HasColor, term.Color);
                    row.transform.SetAsLastSibling();
                    _spawned.Add(row);
                }
            }

            ClearHover();
            if (hoverRow != null)
            {
                hoverRow.gameObject.SetActive(true);
                hoverRow.BindHint(HoverHintText);
                hoverRow.transform.SetAsFirstSibling();
            }
        }

        public void ShowHoverTerm(string displayName, string explanation, bool hasColor, Color color)
        {
            EnsureHoverRow();
            if (hoverRow == null)
            {
                return;
            }

            hoverRow.gameObject.SetActive(true);
            hoverRow.Bind(displayName, explanation, hasColor, color);
            hoverRow.transform.SetAsFirstSibling();
        }

        public void ClearHover()
        {
            if (hoverRow == null)
            {
                return;
            }

            hoverRow.BindHint(HoverHintText);
        }

        public void ClearAll()
        {
            ClearSpawned();
            ClearHover();
        }

        private void ClearSpawned()
        {
            for (var i = 0; i < _spawned.Count; i++)
            {
                var row = _spawned[i];
                if (row != null)
                {
                    Destroy(row.gameObject);
                }
            }

            _spawned.Clear();
        }

        private void EnsureHoverRow()
        {
            if (hoverRow != null || rowPrefab == null || content == null)
            {
                return;
            }

            hoverRow = Instantiate(rowPrefab, content);
            hoverRow.gameObject.SetActive(true);
            hoverRow.name = "词条Hover槽";
            hoverRow.EnsureLayoutElement();
            hoverRow.BindHint(HoverHintText);
            hoverRow.transform.SetAsFirstSibling();
        }

        private void EnsureLayout()
        {
            if (content == null)
            {
                content = transform as RectTransform;
            }

            if (content == null)
            {
                return;
            }

            var vlg = content.GetComponent<VerticalLayoutGroup>();
            if (vlg == null)
            {
                vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(4, 4, 4, 4);
                vlg.spacing = 8f;
                vlg.childAlignment = TextAnchor.UpperLeft;
                vlg.childControlWidth = true;
                vlg.childControlHeight = true;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;
            }

            var fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            }

            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }
}
