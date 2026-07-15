using UnityEngine;

namespace NineGrid.Cards.Convergence
{
    /// <summary>
    /// 每卡固定 L0–L4 变换塔：永不换父。世界位置 = 各层 local 叠加。
    /// L0 为本组件所在根；L1–L4 为固定命名空节点。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardTransformTower : MonoBehaviour
    {
        public const string BoardFrameName = "_L1_BoardFrame";
        public const string SlotFrameName = "_L2_SlotFrame";
        public const string EffectFrameName = "_L3_EffectFrame";
        public const string CardVisualName = "_L4_CardVisual";

        [SerializeField]
        [Tooltip("L1 整盘运动层。运行时自动查找/装配：Awake 中 EnsureTower 按固定名创建或绑定。")]
        private Transform boardFrame;

        [SerializeField]
        [Tooltip("L2 格位收敛层。运行时自动查找/装配：Awake 中 EnsureTower 按固定名创建或绑定。")]
        private Transform slotFrame;

        [SerializeField]
        [Tooltip("L3 特效位移层。运行时自动查找/装配：Awake 中 EnsureTower 按固定名创建或绑定。")]
        private Transform effectFrame;

        [SerializeField]
        [Tooltip("L4 视觉叶子。运行时自动查找/装配：Awake 中 EnsureTower 按固定名创建或绑定；sprite/数值子节点挂其下。")]
        private Transform cardVisual;

        public Transform CardRoot => transform;
        public Transform BoardFrame => boardFrame;
        public Transform SlotFrame => slotFrame;
        public Transform EffectFrame => effectFrame;
        public Transform CardVisual => cardVisual;

        private void Awake()
        {
            EnsureTower();
        }

        /// <summary>
        /// 幂等：确保 L1–L4 固定命名层级存在并缓存引用。不改已有子节点归属（预制体装配负责迁入 L4）。
        /// </summary>
        public void EnsureTower()
        {
            boardFrame = EnsureChild(transform, BoardFrameName, boardFrame);
            slotFrame = EnsureChild(boardFrame, SlotFrameName, slotFrame);
            effectFrame = EnsureChild(slotFrame, EffectFrameName, effectFrame);
            cardVisual = EnsureChild(effectFrame, CardVisualName, cardVisual);
        }

        public Transform GetLayer(TowerLayer layer)
        {
            EnsureTower();
            return layer switch
            {
                TowerLayer.CardRoot => CardRoot,
                TowerLayer.BoardFrame => boardFrame,
                TowerLayer.SlotFrame => slotFrame,
                TowerLayer.EffectFrame => effectFrame,
                TowerLayer.CardVisual => cardVisual,
                _ => CardRoot,
            };
        }

        public bool TryGetLayer(TowerLayer layer, out Transform layerTransform)
        {
            layerTransform = GetLayer(layer);
            return layerTransform != null;
        }

        private static Transform EnsureChild(Transform parent, string childName, Transform cached)
        {
            if (cached != null && cached.parent == parent && cached.name == childName)
            {
                return cached;
            }

            var existing = parent.Find(childName);
            if (existing != null)
            {
                ResetLocalIdentity(existing);
                return existing;
            }

            var go = new GameObject(childName);
            var child = go.transform;
            child.SetParent(parent, false);
            ResetLocalIdentity(child);
            return child;
        }

        private static void ResetLocalIdentity(Transform t)
        {
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
        }
    }
}
