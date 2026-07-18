using UnityEngine;

namespace NineGrid.LivingUI.Unity
{
    /// <summary>
    /// 挂在 A 类内容物体上，声明内容绑定元数据；由 LivingUiContentDriver 驱动显隐与反应式投影。
    /// RigidTravel 靠父子挂接；BoundaryReactive / PartialFollow 由投影器写 local 位姿。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LivingUiContentMarker : MonoBehaviour
    {
        [Tooltip("内容逻辑 ID，如 menu.title；须唯一。")]
        [SerializeField] private string contentId;

        [Tooltip("所属载体 ID 1…12；应与父 CarrierView 一致。")]
        [SerializeField] private int carrierId;

        [Tooltip("跟随策略：RigidTravel / BoundaryReactive / PartialFollow。")]
        [SerializeField] private LivingUiContentFollowPolicy followPolicy = LivingUiContentFollowPolicy.RigidTravel;

        [Tooltip("显隐策略；反应式内容建议 AlwaysVisible 以便转场中可见投影。")]
        [SerializeField] private LivingUiContentVisibilityPolicy visibilityPolicy =
            LivingUiContentVisibilityPolicy.HideDuringTransit;

        [Tooltip("仅在该构型 Face 显示；MainMenu 切片内容绑 MainMenu。")]
        [SerializeField] private LivingUiLayoutId faceLayout = LivingUiLayoutId.MainMenu;

        [Tooltip("是否限制 Face；关则任意构型都可显示（仍受显隐策略约束）。")]
        [SerializeField] private bool restrictToFace = true;

        [Tooltip("随行包裹尺寸（世界单位）；>0 时抬高该载体流动下限。")]
        [SerializeField] private Vector2 envelopeSize;

        [Tooltip("反应式基线载体尺寸；留 (0,0) 时运行时用 MainMenu 终态尺寸。")]
        [SerializeField] private Vector2 baselineSize;

        [Tooltip("PartialFollow 贴边方向。")]
        [SerializeField] private LivingUiPartialFollowEdge followEdge = LivingUiPartialFollowEdge.Left;

        [Tooltip("BoundaryReactive 错峰跨度 [0,1]；越大则边缘内容越早坍缩。")]
        [Range(0f, 1f)]
        [SerializeField] private float staggerSpan = LivingUiContentProjector.DefaultStaggerSpan;

        [Tooltip("作者局部位姿（相对 ContentAttach）；反应式投影以此为基准。留空则 Awake 从 Transform 捕获。")]
        [SerializeField] private Vector3 authoredLocalPosition;

        [Tooltip("作者局部缩放；留 (0,0,0) 时 Awake 从 Transform 捕获（通常为 1,1,1）。")]
        [SerializeField] private Vector3 authoredLocalScale = Vector3.one;

        [Tooltip("是否已写入作者位姿；关则 Awake/首次 ToBinding 从 Transform 捕获。")]
        [SerializeField] private bool hasAuthoredPose;

        public string ContentId => contentId;
        public int CarrierId => carrierId;
        public LivingUiContentFollowPolicy FollowPolicy => followPolicy;
        public Vector2 BaselineSize => baselineSize;
        public LivingUiPartialFollowEdge FollowEdge => followEdge;
        public float StaggerSpan => staggerSpan;

        private void Awake()
        {
            EnsureAuthoredPose();
        }

        public void EnsureAuthoredPose()
        {
            if (hasAuthoredPose) return;
            authoredLocalPosition = transform.localPosition;
            authoredLocalScale = transform.localScale;
            if (authoredLocalScale == Vector3.zero) authoredLocalScale = Vector3.one;
            hasAuthoredPose = true;
        }

        public LivingUiContentLocalPose AuthoredPose
        {
            get
            {
                EnsureAuthoredPose();
                return new LivingUiContentLocalPose(authoredLocalPosition, authoredLocalScale);
            }
        }

        public LivingUiContentBinding ToBinding()
        {
            EnsureAuthoredPose();
            return new LivingUiContentBinding(
                string.IsNullOrEmpty(contentId) ? name : contentId,
                carrierId,
                AuthoredPose,
                followPolicy,
                visibilityPolicy,
                restrictToFace ? faceLayout : (LivingUiLayoutId?)null,
                new LivingUiContentEnvelope(envelopeSize),
                baselineSize,
                followEdge,
                staggerSpan);
        }

        public void ApplyAuthored(
            string id,
            int carrier,
            LivingUiContentFollowPolicy follow,
            LivingUiContentVisibilityPolicy visibility,
            LivingUiLayoutId face,
            bool restrictFace,
            Vector2 envelope = default,
            Vector2 baseline = default,
            LivingUiPartialFollowEdge edge = LivingUiPartialFollowEdge.Left,
            float stagger = LivingUiContentProjector.DefaultStaggerSpan)
        {
            contentId = id;
            carrierId = carrier;
            followPolicy = follow;
            visibilityPolicy = visibility;
            faceLayout = face;
            restrictToFace = restrictFace;
            envelopeSize = envelope;
            baselineSize = baseline;
            followEdge = edge;
            staggerSpan = stagger;
        }

        public void CaptureAuthoredPoseFromTransform()
        {
            authoredLocalPosition = transform.localPosition;
            authoredLocalScale = transform.localScale;
            if (authoredLocalScale == Vector3.zero) authoredLocalScale = Vector3.one;
            hasAuthoredPose = true;
        }
    }
}
