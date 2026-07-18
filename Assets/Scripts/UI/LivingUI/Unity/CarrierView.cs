using UnityEngine;

namespace NineGrid.LivingUI.Unity
{
    /// <summary>
    /// 载体视图：SpriteRenderer 皮肤 + ContentAttach（A 类挂点）+ CarrierAnchorRegistry（B 类）。
    /// 作为将来 LayoutFrame/ImpactFrame/FlipFrame 帧栈的局部化 seam；M1 不建整栈。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class CarrierView : MonoBehaviour
    {
        public const string ContentAttachName = "ContentAttach";

        [Tooltip("载体 9-slice 皮肤；留空时运行时 GetComponent SpriteRenderer。")]
        [SerializeField] private SpriteRenderer skin;

        [Tooltip("A 类随行内容挂点；留空时运行时自动创建/查找子节点 ContentAttach。")]
        [SerializeField] private Transform contentAttach;

        [Tooltip("B 类锚点注册表；留空时运行时 GetComponent 或自动添加。")]
        [SerializeField] private CarrierAnchorRegistry anchorRegistry;

        [Tooltip("载体身份 1…12；0 表示运行时从物体名解析。")]
        [SerializeField] private int carrierId;

        public int CarrierId => carrierId;
        public SpriteRenderer Skin => skin;
        public Transform ContentAttach => contentAttach;
        public CarrierAnchorRegistry AnchorRegistry => anchorRegistry;

        private void Awake()
        {
            EnsureWired();
        }

        public void EnsureWired()
        {
            if (skin == null) skin = GetComponent<SpriteRenderer>();
            if (carrierId <= 0 && int.TryParse(name, out var parsed)) carrierId = parsed;
            EnsureContentAttach();
            EnsureAnchorRegistry();
        }

        public void EnsureContentAttach()
        {
            if (contentAttach != null) return;
            var existing = transform.Find(ContentAttachName);
            if (existing != null)
            {
                contentAttach = existing;
                return;
            }

            var go = new GameObject(ContentAttachName);
            contentAttach = go.transform;
            contentAttach.SetParent(transform, false);
            contentAttach.localPosition = Vector3.zero;
            contentAttach.localRotation = Quaternion.identity;
            contentAttach.localScale = Vector3.one;
        }

        private void EnsureAnchorRegistry()
        {
            if (anchorRegistry != null) return;
            anchorRegistry = GetComponent<CarrierAnchorRegistry>();
            if (anchorRegistry == null) anchorRegistry = gameObject.AddComponent<CarrierAnchorRegistry>();
        }
    }
}
