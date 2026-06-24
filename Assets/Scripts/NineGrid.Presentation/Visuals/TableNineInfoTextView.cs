using TMPro;
using UnityEngine;

namespace NineGrid.Presentation.Visuals
{
    /// <summary>
    /// 悬停信息区：仅绑定描述文案 TMP。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TableNineInfoTextView : MonoBehaviour
    {
        [SerializeField] private TMP_Text descriptionText;

        public void SetDescription(string description)
        {
            if (descriptionText == null)
            {
                return;
            }

            descriptionText.text = description ?? string.Empty;
        }

        public void Clear()
        {
            SetDescription(string.Empty);
        }

        private void Reset()
        {
            if (descriptionText == null)
            {
                descriptionText = GetComponent<TMP_Text>();
            }
        }
    }
}
