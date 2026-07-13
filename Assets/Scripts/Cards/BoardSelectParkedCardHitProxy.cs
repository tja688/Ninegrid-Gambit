using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 多选模式下驻留于玩家格的效果卡点击代理：点击即反悔回手。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class BoardSelectParkedCardHitProxy : MonoBehaviour
    {
        private BoxCollider2D _collider;
        private CardVisualDriver _driver;
        private bool _armed;

        private void Awake()
        {
            _collider = GetComponent<BoxCollider2D>();
            _driver = GetComponent<CardVisualDriver>();
            SetArmed(false);
        }

        public void SetArmed(bool armed)
        {
            _armed = armed;
            if (_collider != null)
            {
                _collider.enabled = armed;
            }
        }

        private void OnMouseDown()
        {
            if (!_armed || !CombatHitSink.BoardSelectModeActive)
            {
                return;
            }

            _driver ??= GetComponent<CardVisualDriver>();
            var uid = _driver?.BoundCard?.Uid ?? 0;
            if (uid <= 0)
            {
                return;
            }

            BoardCardSelectModeController.TryAbortByParkedItemClick(uid);
        }
    }
}
