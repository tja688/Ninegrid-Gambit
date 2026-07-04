using NineGrid.Presentation.Visuals;
using UnityEngine;

namespace NineGrid.UI
{
    /// <summary>
    /// 鼠标悬停某个 <see cref="SelectableSceneElement"/> 时，在通用通知文字里显示一段说明；移开即收起。
    /// 船体强化槽位、场景可交互物等「悬停看信息」都可复用。数值 / 真实数据待接入，先用占位文案。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HoverNoticePresenter : MonoBehaviour
    {
        [Header("目标（留空取自身）")]
        [SerializeField] SelectableSceneElement target;

        [Header("提示内容")]
        [SerializeField] NoticeChannel channel = NoticeChannel.Notice;
        [TextArea(1, 3)]
        [SerializeField] string message = "船体强化槽位（空）";

        bool _showing;

        void Awake()
        {
            if (target == null)
            {
                target = GetComponent<SelectableSceneElement>();
            }
        }

        void OnDisable()
        {
            HideIfMine();
        }

        void Update()
        {
            var hovered = target != null && target.IsHovered;
            if (hovered == _showing)
            {
                return;
            }

            if (hovered)
            {
                Show();
            }
            else
            {
                HideIfMine();
            }
        }

        void Show()
        {
            var notice = UiSystem.Instance != null ? UiSystem.Instance.Notice : null;
            if (notice == null)
            {
                return;
            }

            // duration<=0：悬停期间常驻，不自动隐藏。
            if (notice.Show(channel, message, 0f))
            {
                _showing = true;
            }
        }

        void HideIfMine()
        {
            if (!_showing)
            {
                return;
            }

            _showing = false;
            var notice = UiSystem.Instance != null ? UiSystem.Instance.Notice : null;
            if (notice != null && notice.IsShowing && notice.ActiveChannel == channel)
            {
                notice.Hide();
            }
        }
    }
}
