using NineGrid.Core;
using QFramework;
using UnityEngine;

namespace NineGrid.Cards.Vfx
{
    /// <summary>
    /// 悬停威胁范围荧光：悬停场地怪物卡时，按其攻击模式（ADR-0011：正交 / 斜角 / 全向）
    /// 在受威胁的邻格边缘点亮一圈柔和的加色荧光描边。
    /// 纯装饰、不占主线、不进命中路由；背面卡（ADR-0016 双向惰性不开火）与「无」模式不显示。
    /// </summary>
    public static class BoardRangeGlowFx
    {
        /// <summary>悬停进入：解析该卡的威胁范围并点亮；无范围时等价于清除本请求者的显示。</summary>
        public static void ShowThreatRange(Behaviour requester, ManagedCard card)
        {
            if (requester == null)
            {
                return;
            }

            BoardRangeGlowRunner.Ensure().Show(requester, card);
        }

        /// <summary>悬停离开 / 认领释放：仅当当前显示属于该请求者时清除。</summary>
        public static void Hide(Behaviour requester)
        {
            var runner = BoardRangeGlowRunner.InstanceOrNull();
            if (runner != null)
            {
                runner.HideIfOwnedBy(requester);
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class BoardRangeGlowRunner : MonoBehaviour
    {
        // --- 视觉调参（微微荧光、不抢戏） ---
        private static readonly Color ThreatColor = new Color(1f, 0.45f, 0.22f);
        private const float BaseAlpha = 0.4f;
        private const float AvatarEmphasisWeight = 1.45f;
        private const float FadeInSeconds = 0.16f;
        private const float FadeOutSeconds = 0.28f;
        private const float BreathHz = 0.45f;
        private const float BreathDepth = 0.18f;

        // --- 程序化光环贴片（9-slice 内缘光晕） ---
        private const int GlowTexSize = 96;
        private const int GlowTexBorder = 36;
        private const float GlowPixelsPerUnit = 64f;
        private const float GlowAlphaPeakPx = 3.5f;
        private const float GlowAlphaTailPx = 8.5f;
        private const float GlowFadeStartPx = 24f;
        private const float GlowFadeEndPx = 33f;

        private const string GlowShaderResourceKey = "Arts/VisualProfiles/TableNineSlotGlowAdditive";
        private const string GlowShaderName = "TableNine/SlotGlowAdditive";

        private static BoardRangeGlowRunner sInstance;

        private readonly float[] _targetWeight = new float[GroundSlotTopology.MaxSlot + 1];
        private readonly float[] _currentWeight = new float[GroundSlotTopology.MaxSlot + 1];
        private readonly SpriteRenderer[] _cellRenderers = new SpriteRenderer[GroundSlotTopology.MaxSlot + 1];

        private Behaviour _requester;
        private int _originUid;
        private int _originSlot;

        private Sprite _glowSprite;
        private Material _glowMaterial;
        private bool _loggedShaderFallback;

        public static BoardRangeGlowRunner Ensure()
        {
            if (sInstance != null)
            {
                return sInstance;
            }

            var existing = FindFirstObjectByType<BoardRangeGlowRunner>();
            if (existing != null)
            {
                sInstance = existing;
                return sInstance;
            }

            var go = new GameObject("BoardRangeGlowFx");
            DontDestroyOnLoad(go);
            sInstance = go.AddComponent<BoardRangeGlowRunner>();
            return sInstance;
        }

        public static BoardRangeGlowRunner InstanceOrNull()
        {
            return sInstance;
        }

        public void Show(Behaviour requester, ManagedCard card)
        {
            ClearTargets();
            _requester = null;

            if (requester == null || card == null)
            {
                return;
            }

            var field = GroundFieldGeometryHook.FieldOrNull();
            if (field == null || !field.TryGetSlotOf(card.Uid, out var originSlot))
            {
                return;
            }

            if (!TryResolveThreatFilter(card, out var filter))
            {
                return;
            }

            var threatened = GroundSlotTopology.GetNeighbors(originSlot, filter);
            if (threatened.Count == 0)
            {
                return;
            }

            var avatarSlot = ResolveAvatarSlotOrMinusOne();
            var shown = false;
            for (var i = 0; i < threatened.Count; i++)
            {
                var slot = threatened[i];
                if (!TryPrepareCellRenderer(field, slot))
                {
                    continue;
                }

                _targetWeight[slot] = slot == avatarSlot ? AvatarEmphasisWeight : 1f;
                shown = true;
            }

            if (shown)
            {
                _requester = requester;
                _originUid = card.Uid;
                _originSlot = originSlot;
            }
        }

        public void HideIfOwnedBy(Behaviour requester)
        {
            if (_requester == null || !ReferenceEquals(_requester, requester))
            {
                return;
            }

            _requester = null;
            ClearTargets();
        }

        private void Update()
        {
            ValidateActiveRequest();
            TickCellVisuals();
        }

        private void OnDestroy()
        {
            if (sInstance == this)
            {
                sInstance = null;
            }

            if (_glowMaterial != null)
            {
                Destroy(_glowMaterial);
            }

            if (_glowSprite != null && _glowSprite.texture != null)
            {
                Destroy(_glowSprite.texture);
            }
        }

        /// <summary>
        /// 悬停离开事件之外的兜底：源卡换格 / 死亡 / 主线开跑 / 请求者失活时自动淡出，
        /// 避免旋转、齐射期间残留过期的威胁提示。
        /// </summary>
        private void ValidateActiveRequest()
        {
            if (_requester == null)
            {
                return;
            }

            var stillValid = _requester != null
                && _requester.isActiveAndEnabled
                && !NineGrid.Presentation.PresentationInputGates.MainlineBusy;

            if (stillValid)
            {
                var field = GroundFieldGeometryHook.FieldOrNull();
                stillValid = field != null
                    && field.TryGetSlotOf(_originUid, out var slot)
                    && slot == _originSlot;
            }

            if (!stillValid)
            {
                _requester = null;
                ClearTargets();
            }
        }

        private void TickCellVisuals()
        {
            var dt = Time.unscaledDeltaTime;
            var breath = 1f - BreathDepth
                * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * BreathHz * 2f * Mathf.PI));

            for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
            {
                var target = _targetWeight[slot];
                var current = _currentWeight[slot];
                if (target <= 0f && current <= 0.001f)
                {
                    var idle = _cellRenderers[slot];
                    if (idle != null && idle.enabled)
                    {
                        idle.enabled = false;
                    }

                    continue;
                }

                var speed = target > current ? 1f / FadeInSeconds : 1f / FadeOutSeconds;
                current = Mathf.MoveTowards(current, target, speed * dt);
                _currentWeight[slot] = current;

                var renderer = _cellRenderers[slot];
                if (renderer == null)
                {
                    continue;
                }

                if (current <= 0.001f && target <= 0f)
                {
                    renderer.enabled = false;
                    continue;
                }

                renderer.enabled = true;
                var alpha = Mathf.Clamp01(BaseAlpha * current * breath);
                renderer.color = new Color(ThreatColor.r, ThreatColor.g, ThreatColor.b, alpha);
            }
        }

        private static bool TryResolveThreatFilter(ManagedCard card, out GroundSlotRelation filter)
        {
            filter = GroundSlotRelation.None;
            var snapshot = card.CommittedPresentation;
            if (snapshot == null || !snapshot.FaceUp)
            {
                return false;
            }

            switch (snapshot.AttackPattern)
            {
                case AttackPattern.OrthogonalMelee:
                    filter = GroundSlotRelation.Orthogonal;
                    return true;
                case AttackPattern.DiagonalMelee:
                    filter = GroundSlotRelation.Diagonal;
                    return true;
                case AttackPattern.OmnidirectionalMelee:
                    filter = GroundSlotRelation.Orthogonal | GroundSlotRelation.Diagonal;
                    return true;
                default:
                    return false;
            }
        }

        private static int ResolveAvatarSlotOrMinusOne()
        {
            var arch = NineGridArchitecture.Current;
            var board = arch?.GetModel<BoardModel>();
            if (board == null || !board.AvatarSlot.Value.IsBoardSlot)
            {
                return -1;
            }

            return board.AvatarSlot.Value.Index;
        }

        private bool TryPrepareCellRenderer(GroundFieldView field, int slot)
        {
            var anchor = field.GetGroundAnchor(slot);
            if (anchor == null)
            {
                return false;
            }

            var renderer = _cellRenderers[slot];
            if (renderer == null)
            {
                renderer = CreateCellRenderer(slot);
                if (renderer == null)
                {
                    return false;
                }

                _cellRenderers[slot] = renderer;
            }

            var anchorRenderer = anchor.GetComponent<SpriteRenderer>();
            if (anchorRenderer != null)
            {
                renderer.sortingLayerID = anchorRenderer.sortingLayerID;
                renderer.sortingOrder = anchorRenderer.sortingOrder + 1;
                var bounds = anchorRenderer.bounds;
                renderer.transform.position = new Vector3(
                    bounds.center.x,
                    bounds.center.y,
                    anchor.position.z);
                renderer.size = new Vector2(bounds.size.x, bounds.size.y);
            }
            else
            {
                renderer.sortingLayerID = 0;
                renderer.sortingOrder = 1;
                renderer.transform.position = anchor.position;
                renderer.size = new Vector2(3.8f, 4.9f);
            }

            return true;
        }

        private SpriteRenderer CreateCellRenderer(int slot)
        {
            var sprite = EnsureGlowSprite();
            if (sprite == null)
            {
                return null;
            }

            var go = new GameObject("SlotRangeGlow_" + slot);
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.sharedMaterial = EnsureGlowMaterial();
            renderer.color = new Color(ThreatColor.r, ThreatColor.g, ThreatColor.b, 0f);
            renderer.enabled = false;
            return renderer;
        }

        private Material EnsureGlowMaterial()
        {
            if (_glowMaterial != null)
            {
                return _glowMaterial;
            }

            var shader = Resources.Load<Shader>(GlowShaderResourceKey);
            if (shader == null)
            {
                shader = Shader.Find(GlowShaderName);
            }

            if (shader == null)
            {
                // 退化为普通半透明混合，仍可见但失去加色荧光感；只告警一次。
                shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (!_loggedShaderFallback)
                {
                    _loggedShaderFallback = true;
                    Debug.LogWarning(
                        "[BoardRangeGlowFx] 未找到 " + GlowShaderName + "，回退 Sprite-Unlit-Default。");
                }
            }

            _glowMaterial = new Material(shader)
            {
                name = "BoardRangeGlowRuntime",
                hideFlags = HideFlags.HideAndDontSave,
            };
            return _glowMaterial;
        }

        /// <summary>
        /// 程序化生成 9-slice 光环贴片：alpha 沿格框内缘先快速升起、再指数衰减，
        /// 中央区域完全透明，只留一圈贴着格子边缘的柔和光晕。
        /// </summary>
        private Sprite EnsureGlowSprite()
        {
            if (_glowSprite != null)
            {
                return _glowSprite;
            }

            var texture = new Texture2D(GlowTexSize, GlowTexSize, TextureFormat.RGBA32, false)
            {
                name = "BoardRangeGlowTex",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[GlowTexSize * GlowTexSize];
            for (var y = 0; y < GlowTexSize; y++)
            {
                for (var x = 0; x < GlowTexSize; x++)
                {
                    var edgeDistance = Mathf.Min(
                        Mathf.Min(x, GlowTexSize - 1 - x),
                        Mathf.Min(y, GlowTexSize - 1 - y));
                    var alpha = EvaluateGlowAlpha(edgeDistance);
                    pixels[y * GlowTexSize + x] = new Color32(
                        255,
                        255,
                        255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var border = new Vector4(GlowTexBorder, GlowTexBorder, GlowTexBorder, GlowTexBorder);
            _glowSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, GlowTexSize, GlowTexSize),
                new Vector2(0.5f, 0.5f),
                GlowPixelsPerUnit,
                0,
                SpriteMeshType.FullRect,
                border);
            _glowSprite.name = "BoardRangeGlowSprite";
            _glowSprite.hideFlags = HideFlags.HideAndDontSave;
            return _glowSprite;
        }

        private static float EvaluateGlowAlpha(float edgeDistancePx)
        {
            float alpha;
            if (edgeDistancePx <= GlowAlphaPeakPx)
            {
                alpha = Mathf.Pow(edgeDistancePx / GlowAlphaPeakPx, 0.8f);
            }
            else
            {
                alpha = Mathf.Exp(-(edgeDistancePx - GlowAlphaPeakPx) / GlowAlphaTailPx);
            }

            // 9-slice 中央拉伸区必须归零，否则整格被平铺淡光填满。
            var interiorFade = 1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(GlowFadeStartPx, GlowFadeEndPx, edgeDistancePx));
            return alpha * interiorFade;
        }

        private void ClearTargets()
        {
            for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
            {
                _targetWeight[slot] = 0f;
            }
        }
    }
}
