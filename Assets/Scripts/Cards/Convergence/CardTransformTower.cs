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

        /// <summary>
        /// L4 下统一卡面挂载点。翻牌租赁只动此叶子，不另起视觉根（见 ADR-0002）。
        /// </summary>
        public const string FacePivotName = "FacePivot";

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
        [Tooltip("L4 视觉叶子。运行时自动查找/装配：Awake 中 EnsureTower 按固定名创建或绑定；其下仅保留 FacePivot 作为卡面挂载点。")]
        private Transform cardVisual;

        [SerializeField]
        [Tooltip("卡面挂载点（L4/FacePivot）。运行时自动查找/装配：EnsureTower 按固定名创建或绑定；Kind 卡面模板挂为其子物体。")]
        private Transform facePivot;

        public Transform CardRoot => transform;
        public Transform BoardFrame => boardFrame;
        public Transform SlotFrame => slotFrame;
        public Transform EffectFrame => effectFrame;
        public Transform CardVisual => cardVisual;
        public Transform FacePivot => facePivot;

        private void Awake()
        {
            EnsureTower();
        }

        /// <summary>
        /// 幂等：确保 L1–L4 与 L4/FacePivot 固定命名层级存在并缓存引用。
        /// 不改已有子节点归属（预制体装配负责迁入 L4 / FacePivot）。
        /// </summary>
        public void EnsureTower()
        {
            boardFrame = EnsureChild(transform, BoardFrameName, boardFrame);
            slotFrame = EnsureChild(boardFrame, SlotFrameName, slotFrame);
            effectFrame = EnsureChild(slotFrame, EffectFrameName, effectFrame);
            cardVisual = EnsureChild(effectFrame, CardVisualName, cardVisual);
            facePivot = EnsureChild(cardVisual, FacePivotName, facePivot);
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
