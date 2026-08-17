using System;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using QFramework;
using UnityEngine;

namespace NineGrid.Cards.Vfx
{
    /// <summary>
    /// 悬停范围荧光：在相关邻格边缘点亮一圈柔和的加色荧光描边。三类来源——
    /// ① 怪物卡：按攻击模式（ADR-0011：正交 / 斜角 / 全向）点亮威胁范围（暖橙威胁色）；
    /// ② 机关卡：效果模板含邻接原子（正交邻接语义）时点亮影响范围（冷青影响色，与怪物区分）；
    /// ③ 玩家本体：悬停 Avatar 格时按互动范围（Avatar 槽四向正交，与 Core AreAdjacent 同源）
    ///   点亮可攻击范围（与怪物同威胁色）。
    /// 纯装饰、不占主线、不进命中路由；背面卡（ADR-0016 双向惰性不开火）与「无」模式不显示。
    /// </summary>
    public static class BoardRangeGlowFx
    {
        /// <summary>悬停进入：解析该卡的威胁/影响范围并点亮；无范围时等价于清除本请求者的显示。</summary>
        public static void ShowThreatRange(Behaviour requester, ManagedCard card)
        {
            if (requester == null)
            {
                return;
            }

            BoardRangeGlowRunner.Ensure().Show(requester, card);
        }

        /// <summary>
        /// 悬停玩家本体：hoveredSlot 为 Avatar 槽时点亮玩家可攻击范围，否则清除本请求者的显示。
        /// 供场地面在无认领者格位的悬停分支调用（Avatar 永不认领，ADR-0023）。
        /// </summary>
        public static void ShowAvatarInteractionRange(Behaviour requester, int hoveredSlot)
        {
            if (requester == null)
            {
                return;
            }

            BoardRangeGlowRunner.Ensure().ShowAvatarIfSlotMatches(requester, hoveredSlot);
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

        /// <summary>教学阶段2：常驻显示玩家攻击范围（不依赖 hover）。</summary>
        public static void SetAvatarRangePinned(Behaviour owner, bool pinned)
        {
            BoardRangeGlowRunner.Ensure().SetAvatarRangePinned(owner, pinned);
        }
    }

    [DisallowMultipleComponent]
    public sealed class BoardRangeGlowRunner : MonoBehaviour
    {
        // --- 视觉调参（微微荧光、不抢戏） ---
        private static readonly Color ThreatColor = new Color(1f, 0.45f, 0.22f);
        // 机关影响范围用冷青色，与怪物威胁的暖橙拉开区分。
        private static readonly Color TrapInfluenceColor = new Color(0.3f, 0.85f, 1f);
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
        private Behaviour _pinnedOwner;
        private bool _avatarPinned;
        private int _originUid;
        private int _originSlot;
        private bool _avatarMode;
        private Color _activeColor = ThreatColor;

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
            _avatarMode = false;

            if (requester == null || card == null)
            {
                return;
            }

            var field = GroundFieldGeometryHook.FieldOrNull();
            if (field == null || !field.TryGetSlotOf(card.Uid, out var originSlot))
            {
                return;
            }

            if (!TryResolveRange(card, out var filter, out var color))
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
                _activeColor = color;
            }
        }

        /// <summary>
        /// 悬停 Avatar 格：点亮玩家可攻击范围（Avatar 槽四向正交，与 Core 互动范围同源）。
        /// hoveredSlot 非 Avatar 槽时仅清除本请求者显示；重复悬停同格幂等（避免逐帧重建）。
        /// </summary>
        public void ShowAvatarIfSlotMatches(Behaviour requester, int hoveredSlot)
        {
            var avatarSlot = ResolveAvatarSlotOrMinusOne();
            if (requester == null
                || avatarSlot < GroundSlotTopology.MinSlot
                || hoveredSlot != avatarSlot)
            {
                HideIfOwnedBy(requester);
                return;
            }

            if (_avatarMode
                && ReferenceEquals(_requester, requester)
                && _originSlot == avatarSlot)
            {
                return;
            }

            ClearTargets();
            _requester = null;
            _avatarMode = false;

            var field = GroundFieldGeometryHook.FieldOrNull();
            if (field == null)
            {
                return;
            }

            var reachable = GroundSlotTopology.GetNeighbors(avatarSlot, GroundSlotRelation.Orthogonal);
            var shown = false;
            for (var i = 0; i < reachable.Count; i++)
            {
                if (!TryPrepareCellRenderer(field, reachable[i]))
                {
                    continue;
                }

                _targetWeight[reachable[i]] = 1f;
                shown = true;
            }

            if (shown)
            {
                _requester = requester;
                _originUid = 0;
                _originSlot = avatarSlot;
                _avatarMode = true;
                _activeColor = ThreatColor;
            }
        }

        public void HideIfOwnedBy(Behaviour requester)
        {
            if (_avatarPinned && ReferenceEquals(_pinnedOwner, requester))
            {
                return;
            }

            if (_requester == null || !ReferenceEquals(_requester, requester))
            {
                return;
            }

            _requester = null;
            _avatarMode = false;
            ClearTargets();
        }

        public void SetAvatarRangePinned(Behaviour owner, bool pinned)
        {
            if (pinned)
            {
                _avatarPinned = true;
                _pinnedOwner = owner;
                if (owner != null)
                {
                    ShowAvatarIfSlotMatches(owner, ResolveAvatarSlotOrMinusOne());
                }

                return;
            }

            if (!ReferenceEquals(_pinnedOwner, owner) && owner != null)
            {
                return;
            }

            _avatarPinned = false;
            _pinnedOwner = null;
            if (_avatarMode && ReferenceEquals(_requester, owner))
            {
                _requester = null;
                _avatarMode = false;
                ClearTargets();
            }
        }

        private void Update()
        {
            if (_avatarPinned && _pinnedOwner != null && _pinnedOwner.isActiveAndEnabled)
            {
                ShowAvatarIfSlotMatches(_pinnedOwner, ResolveAvatarSlotOrMinusOne());
            }

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
        /// 避免旋转、齐射期间残留过期的威胁提示。Avatar 模式改验 Avatar 槽未变。
        /// </summary>
        private void ValidateActiveRequest()
        {
            if (_requester == null)
            {
                return;
            }

            if (_avatarPinned && ReferenceEquals(_requester, _pinnedOwner))
            {
                return;
            }

            var stillValid = _requester != null
                && _requester.isActiveAndEnabled
                && !NineGrid.Presentation.PresentationInputGates.MainlineBusy;

            if (stillValid)
            {
                if (_avatarMode)
                {
                    stillValid = ResolveAvatarSlotOrMinusOne() == _originSlot;
                }
                else
                {
                    var field = GroundFieldGeometryHook.FieldOrNull();
                    stillValid = field != null
                        && field.TryGetSlotOf(_originUid, out var slot)
                        && slot == _originSlot;
                }
            }

            if (!stillValid)
            {
                _requester = null;
                _avatarMode = false;
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
                renderer.color = new Color(_activeColor.r, _activeColor.g, _activeColor.b, alpha);
            }
        }

        private static bool TryResolveRange(ManagedCard card, out GroundSlotRelation filter, out Color color)
        {
            filter = GroundSlotRelation.None;
            color = ThreatColor;
            var snapshot = card.CommittedPresentation;
            if (snapshot == null || !snapshot.FaceUp)
            {
                return false;
            }

            // 机关影响范围：效果模板含邻接原子（Core 邻接语义 = 正交）才点亮；无邻接影响的机关不显示。
            if (card.CoreKind == CardPresentationKind.Trap)
            {
                if (!TrapHasAdjacentInfluence(card.DefId))
                {
                    return false;
                }

                filter = GroundSlotRelation.Orthogonal;
                color = TrapInfluenceColor;
                return true;
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

        /// <summary>
        /// 机关是否具有邻接影响范围：任一效果模板 Body 含邻接原子
        /// （AdjacentMonstersAndPlayer / adjacentTo 等，均为正交邻接语义）。
        /// 与 CoreCardPresentationMapper.DetectSyncRhythmFromDto 同款模板扫描；不缓存，跟随 Catalog 重载。
        /// </summary>
        private static bool TrapHasAdjacentInfluence(string defId)
        {
            if (string.IsNullOrEmpty(defId)
                || !CardPresentationConfigCatalog.TryGet(defId, out var dto)
                || dto?.effectAssemblies == null)
            {
                return false;
            }

            for (var i = 0; i < dto.effectAssemblies.Length; i++)
            {
                var assembly = dto.effectAssemblies[i];
                if (assembly == null || string.IsNullOrWhiteSpace(assembly.templateId))
                {
                    continue;
                }

                if (EffectTemplateCatalog.TryGet(assembly.templateId.Trim(), out var template)
                    && template != null
                    && !string.IsNullOrEmpty(template.BodyJson)
                    && template.BodyJson.IndexOf("Adjacent", StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
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
            renderer.color = new Color(_activeColor.r, _activeColor.g, _activeColor.b, 0f);
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
