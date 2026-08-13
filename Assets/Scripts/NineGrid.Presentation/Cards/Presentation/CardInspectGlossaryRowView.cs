using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NineGrid.Cards.Presentation
{
    /// <summary>
    /// 右键详情中的单条词条行（名字 + 详细介绍）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardInspectGlossaryRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text body;

        private bool _hasDefaultColor;
        private Color _defaultColor;

        public void Bind(string displayName, string explanation, bool hasColor, Color color)
        {
            EnsureBody();
            if (body == null)
            {
                return;
            }

            var title = displayName ?? string.Empty;
            var detail = explanation ?? string.Empty;
            if (string.IsNullOrWhiteSpace(detail))
            {
                body.text = title;
            }
            else
            {
                body.text = title + "\n" + detail.Trim();
            }

            // hover 槽跨词条复用：未着色的词条须退回模板默认色，不留上一条的颜色。
            body.color = hasColor ? color : _defaultColor;
        }

        public void BindHint(string hint)
        {
            EnsureBody();
            if (body == null)
            {
                return;
            }

            body.text = hint ?? string.Empty;
            body.color = _defaultColor;
        }

        public void Clear()
        {
            EnsureBody();
            if (body != null)
            {
                body.text = string.Empty;
            }
        }

        private void EnsureBody()
        {
            if (body == null)
            {
                body = GetComponent<TMP_Text>();
                if (body == null)
                {
                    body = GetComponentInChildren<TMP_Text>(true);
                }
            }

            if (body != null && !_hasDefaultColor)
            {
                _defaultColor = body.color;
                _hasDefaultColor = true;
            }
        }

        private void OnValidate()
        {
            EnsureBody();
        }

        /// <summary>为布局组准备：优先吃高、宽拉伸。</summary>
        public void EnsureLayoutElement()
        {
            var le = GetComponent<LayoutElement>();
            if (le == null)
            {
                le = gameObject.AddComponent<LayoutElement>();
            }

            le.minHeight = 40f;
            le.preferredHeight = -1f;
            le.flexibleWidth = 1f;
            le.flexibleHeight = 0f;
        }
    }
}
