using UnityEngine;

namespace NineGrid.LivingUI.Unity
{
    /// <summary>
    /// 挂在 A 类内容物体上：声明锚点与作者位姿；模式由 LivingUiContentController 下发。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LivingUiContentMarker : MonoBehaviour
    {
        [Tooltip("内容逻辑 ID，如 menu.title；须唯一。")]
        [SerializeField] private string contentId;

        [Tooltip("所属载体 ID 1…12；应与父 CarrierView 一致。")]
        [SerializeField] private int carrierId;

        [Tooltip("相对载体的锚点：左上/左下/右下/右上/中心。默认左上。")]
        [SerializeField] private LivingUiContentAnchor anchor = LivingUiContentAnchor.TopLeft;

        [Tooltip("登记的主构型 Face；跨构型复用时由 ContentController 映射表扩展。留空限制关则不限 Face。")]
        [SerializeField] private LivingUiLayoutId faceLayout = LivingUiLayoutId.MainMenu;

        [Tooltip("是否限制到 faceLayout；关则任意构型都可显示（仍受控制器映射约束）。")]
        [SerializeField] private bool restrictToFace = true;

        [Tooltip("随行包裹尺寸（世界单位）；>0 时抬高该载体流动下限。")]
        [SerializeField] private Vector2 envelopeSize;

        [Tooltip("作者化时的载体尺寸；用于锚点换算。留 (0,0) 时运行时用 Face 终态尺寸。")]
        [SerializeField] private Vector2 authoringSize;

        [Tooltip("作者局部位姿（相对所选锚点的偏移）；驱动每帧写回中心系 local。留空则 Awake 从 Transform 捕获。")]
        [SerializeField] private Vector3 authoredLocalPosition;

        [Tooltip("作者局部缩放；留 (0,0,0) 时 Awake 从 Transform 捕获（通常为 1,1,1）。")]
        [SerializeField] private Vector3 authoredLocalScale = Vector3.one;

        [Tooltip("是否已写入作者位姿；关则 Awake/首次 ToBinding 从 Transform 捕获。")]
        [SerializeField] private bool hasAuthoredPose;

        public string ContentId => contentId;
        public int CarrierId => carrierId;
        public LivingUiContentAnchor Anchor => anchor;
        public LivingUiLayoutId FaceLayout => faceLayout;
        public bool RestrictToFace => restrictToFace;
        public Vector2 AuthoringSize => authoringSize;
        public Vector2 EnvelopeSize => envelopeSize;

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
                anchor,
                restrictToFace ? faceLayout : (LivingUiLayoutId?)null,
                new LivingUiContentEnvelope(envelopeSize),
                authoringSize);
        }

        public void ApplyAuthored(
            string id,
            int carrier,
            LivingUiContentAnchor contentAnchor,
            LivingUiLayoutId face,
            bool restrictFace,
            Vector2 envelope = default,
            Vector2 authoring = default)
        {
            contentId = id;
            carrierId = carrier;
            anchor = contentAnchor;
            faceLayout = face;
            restrictToFace = restrictFace;
            envelopeSize = envelope;
            authoringSize = authoring;
        }

        public void SetAuthoredPose(Vector3 offsetFromAnchor, Vector3 localScale)
        {
            authoredLocalPosition = offsetFromAnchor;
            authoredLocalScale = localScale == Vector3.zero ? Vector3.one : localScale;
            hasAuthoredPose = true;
        }

        public void CaptureAuthoredPoseFromTransform()
        {
            authoredLocalPosition = transform.localPosition;
            authoredLocalScale = transform.localScale;
            if (authoredLocalScale == Vector3.zero) authoredLocalScale = Vector3.one;
            hasAuthoredPose = true;
        }

        /// <summary>
        /// 把当前中心系 local 换算为相对锚点偏移（在 authoringSize 下世界位不变）。
        /// </summary>
        public void ConvertCenterLocalToAnchorOffset(Vector2 carrierSize)
        {
            EnsureAuthoredPose();
            if (carrierSize.x <= 0f || carrierSize.y <= 0f) carrierSize = Vector2.one;
            authoredLocalPosition = LivingUiContentProjector.CenterLocalToAnchorOffset(
                anchor, authoredLocalPosition, carrierSize);
            if (authoringSize.x <= 0f || authoringSize.y <= 0f) authoringSize = carrierSize;
            hasAuthoredPose = true;
        }
    }
}
