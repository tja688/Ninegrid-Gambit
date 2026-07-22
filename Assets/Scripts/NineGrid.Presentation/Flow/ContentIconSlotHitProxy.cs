using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 遗物/技能图标槽 hover 命中代理：挂在槽位节点，由 Manager ApplyDefIds 写入 DefId。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class ContentIconSlotHitProxy : MonoBehaviour
    {
        [Tooltip("运行时由 Relic/Skill Manager 写入；留空表示空槽，不响应 hover。")]
        [SerializeField] private string defId;

        private int _showGeneration = -1;

        public string DefId
        {
            get => defId;
            set => defId = value ?? string.Empty;
        }

        private void OnMouseEnter()
        {
            if (string.IsNullOrEmpty(defId))
            {
                return;
            }

            var mgr = UnityEngine.Object.FindFirstObjectByType<DescriptionManagerSingleton>();
            if (mgr == null)
            {
                return;
            }

            _showGeneration = mgr.Show(defId);
        }

        private void OnMouseExit()
        {
            var mgr = UnityEngine.Object.FindFirstObjectByType<DescriptionManagerSingleton>();
            if (mgr == null)
            {
                return;
            }

            if (_showGeneration >= 0)
            {
                mgr.Clear(_showGeneration);
                _showGeneration = -1;
            }
            else
            {
                mgr.Clear();
            }
        }

        private void OnDisable()
        {
            if (_showGeneration < 0)
            {
                return;
            }

            var mgr = UnityEngine.Object.FindFirstObjectByType<DescriptionManagerSingleton>();
            mgr?.Clear(_showGeneration);
            _showGeneration = -1;
        }
    }
}
