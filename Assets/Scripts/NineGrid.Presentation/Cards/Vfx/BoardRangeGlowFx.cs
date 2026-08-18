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
        /// <summary>道具目标/候选高亮色（明亮金黄）。</summary>
        public static readonly Color ItemTargetColor = new Color(1f, 0.82f, 0.22f);

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

        /// <summary>
        /// 道具卡单体/离散目标：在所有合法目标格四周浮现提醒光圈。
        /// </summary>
        public static void ShowItemTargetSlots(
            Behaviour requester,
            System.Collections.Generic.IReadOnlyCollection<int> validSlots,
            Color? color = null)
        {
            if (requester == null)
            {
                return;
            }

            BoardRangeGlowRunner.Ensure().ShowItemTargetSlots(requester, validSlots, color);
        }

        /// <summary>
        /// 道具卡棋盘全局目标：在整个九宫格棋盘四周浮现一道大光圈，提示拖入棋盘即可释放。
        /// </summary>
        public static void ShowBoardApplyZone(Behaviour requester, Color? color = null)
        {
            if (requester == null)
            {
                return;
            }

            BoardRangeGlowRunner.Ensure().ShowBoardApplyZone(requester, color);
        }

        /// <summary>
        /// 场地多选模式：在所有未选中的合法候选四周浮现光圈，已选中的候选格光圈熄灭。
        /// </summary>
        public static void ShowMultiSelectCandidates(
            Behaviour requester,
            System.Collections.Generic.IReadOnlyCollection<int> candidateSlots,
            System.Collections.Generic.IReadOnlyCollection<int> selectedSlots,
            Color? color = null)
        {
            if (requester == null)
            {
                return;
            }

            BoardRangeGlowRunner.Ensure().ShowMultiSelectCandidates(
                requester,
                candidateSlots,
                selectedSlots,
                color);
        }

        /// <summary>悬停离开 / 认领释放 / 拖拽结束：仅当当前显示属于该请求者时清除。</summary>
        public static void Hide(Behaviour requester)
        {
            var runner = BoardRangeGlowRunner.InstanceOrNull();
            if (runner != null)
            {
                runner.HideIfOwnedBy(requester);
            }
        }

        /// <summary>强制清除所有光圈显示（用于状态机重置或打断）。</summary>
        public static void ForceHideAll()
        {
            var runner = BoardRangeGlowRunner.InstanceOrNull();
            if (runner != null)
            {
                runner.ForceHideAll();
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
        private enum GlowMode
        {
            None = 0,
            Threat = 1,
            Avatar = 2,
            ItemSlots = 3,
            BoardApplyZone = 4,
            MultiSelectCandidates = 5,
        }

        // --- 视觉调参（微微荧光、不抢戏） ---
        private static readonly Color ThreatColor = new Color(1f, 0.45f, 0.22f);
        // 机关影响范围用冷青色，与怪物威胁的暖橙拉开区分。
        private static readonly Color TrapInfluenceColor = new Color(0.3f, 0.85f, 1f);
        public static readonly Color ItemTargetColor = BoardRangeGlowFx.ItemTargetColor;
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

        private SpriteRenderer _boardFrameRenderer;
        private float _targetBoardWeight;
        private float _currentBoardWeight;

        private Behaviour _requester;
        private Behaviour _pinnedOwner;
        private bool _avatarPinned;
        private int _originUid;
        private int _originSlot;
        private GlowMode _mode = GlowMode.None;
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
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(go);
            }

            sInstance = go.AddComponent<BoardRangeGlowRunner>();
            return sInstance;
        }

        public static BoardRangeGlowRunner InstanceOrNull()
        {
            return sInstance;
        }

        public Behaviour ActiveRequester => _requester;

        public float TargetBoardWeight => _targetBoardWeight;

        public float CurrentBoardWeight => _currentBoardWeight;

        public bool IsBoardApplyZoneActive => _mode == GlowMode.BoardApplyZone && _targetBoardWeight > 0f;

        public bool IsMultiSelectActive => _mode == GlowMode.MultiSelectCandidates;

        public float GetTargetWeight(int slot)
        {
            return GroundSlotTopology.IsValidSlot(slot) ? _targetWeight[slot] : 0f;
        }

        public float GetCurrentWeight(int slot)
        {
            return GroundSlotTopology.IsValidSlot(slot) ? _currentWeight[slot] : 0f;
        }

        public void Show(Behaviour requester, ManagedCard card)
        {
            ClearTargets();
            _requester = null;
            _mode = GlowMode.None;

            if (requester == null || card == null)
            {
                return;
            }

            var field = GroundFieldGeometryHook.FieldOrNull();
            int originSlot = -1;
            if (field != null)
            {
                if (!field.TryGetSlotOf(card.Uid, out originSlot))
                {
                    return;
                }
            }

            if (!TryResolveRange(card, out var filter, out var color))
            {
                return;
            }

            if (originSlot <= 0)
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
                if (field != null && !TryPrepareCellRenderer(field, slot))
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
                _mode = GlowMode.Threat;
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

            if (_mode == GlowMode.Avatar
                && ReferenceEquals(_requester, requester)
                && _originSlot == avatarSlot)
            {
                return;
            }

            ClearTargets();
            _requester = null;
            _mode = GlowMode.None;

            var field = GroundFieldGeometryHook.FieldOrNull();
            var reachable = GroundSlotTopology.GetNeighbors(avatarSlot, GroundSlotRelation.Orthogonal);
            var shown = false;
            for (var i = 0; i < reachable.Count; i++)
            {
                if (field != null && !TryPrepareCellRenderer(field, reachable[i]))
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
                _mode = GlowMode.Avatar;
                _activeColor = ThreatColor;
            }
        }

        /// <summary>
        /// 道具卡单体/离散目标：点亮所有合法目标槽位。
        /// </summary>
        public void ShowItemTargetSlots(
            Behaviour requester,
            System.Collections.Generic.IReadOnlyCollection<int> validSlots,
            Color? color = null)
        {
            ClearTargets();
            _requester = null;
            _mode = GlowMode.None;

            if (requester == null || validSlots == null || validSlots.Count == 0)
            {
                return;
            }

            var field = GroundFieldGeometryHook.FieldOrNull();
            var shown = false;
            foreach (var slot in validSlots)
            {
                if (!GroundSlotTopology.IsValidSlot(slot))
                {
                    continue;
                }

                if (field != null && !TryPrepareCellRenderer(field, slot))
                {
                    continue;
                }

                _targetWeight[slot] = 1f;
                shown = true;
            }

            if (shown)
            {
                _requester = requester;
                _mode = GlowMode.ItemSlots;
                _activeColor = color ?? ItemTargetColor;
            }
        }

        /// <summary>
        /// 道具卡棋盘全局目标：点亮覆盖整个 3x3 棋盘的大光圈。
        /// </summary>
        public void ShowBoardApplyZone(Behaviour requester, Color? color = null)
        {
            ClearTargets();
            _requester = null;
            _mode = GlowMode.None;

            if (requester == null)
            {
                return;
            }

            var field = GroundFieldGeometryHook.FieldOrNull();
            if (field != null && !TryPrepareBoardFrameRenderer(field))
            {
                return;
            }

            _requester = requester;
            _mode = GlowMode.BoardApplyZone;
            _activeColor = color ?? ItemTargetColor;
            _targetBoardWeight = 1f;
        }

        /// <summary>
        /// 场地多选模式：在所有未选中的合法候选四周浮现光圈，已选中的候选格光圈熄灭。
        /// </summary>
        public void ShowMultiSelectCandidates(
            Behaviour requester,
            System.Collections.Generic.IReadOnlyCollection<int> candidateSlots,
            System.Collections.Generic.IReadOnlyCollection<int> selectedSlots,
            Color? color = null)
        {
            if (requester == null || candidateSlots == null || candidateSlots.Count == 0)
            {
                HideIfOwnedBy(requester);
                return;
            }

            var field = GroundFieldGeometryHook.FieldOrNull();
            _requester = requester;
            _mode = GlowMode.MultiSelectCandidates;
            _activeColor = color ?? ItemTargetColor;
            _targetBoardWeight = 0f;

            var selectedSet = selectedSlots != null
                ? new System.Collections.Generic.HashSet<int>(selectedSlots)
                : null;
            var candidateSet = new System.Collections.Generic.HashSet<int>(candidateSlots);

            for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
            {
                if (candidateSet.Contains(slot))
                {
                    var isSelected = selectedSet != null && selectedSet.Contains(slot);
                    if (!isSelected && (field == null || TryPrepareCellRenderer(field, slot)))
                    {
                        _targetWeight[slot] = 1f;
                    }
                    else
                    {
                        _targetWeight[slot] = 0f;
                    }
                }
                else
                {
                    _targetWeight[slot] = 0f;
                }
            }
        }

        public void HideIfOwnedBy(Behaviour requester)
        {
            if (_avatarPinned && ReferenceEquals(_pinnedOwner, requester))
            {
                return;
            }

            if (_requester == null || (!ReferenceEquals(_requester, requester) && requester != null))
            {
                return;
            }

            _requester = null;
            _mode = GlowMode.None;
            ClearTargets();
        }

        public void ForceHideAll()
        {
            _avatarPinned = false;
            _pinnedOwner = null;
            _requester = null;
            _mode = GlowMode.None;
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
            if (_mode == GlowMode.Avatar && ReferenceEquals(_requester, owner))
            {
                _requester = null;
                _mode = GlowMode.None;
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

            if (_boardFrameRenderer != null)
            {
                Destroy(_boardFrameRenderer.gameObject);
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
        /// 悬停离开/拖拽中断/流程切换之兜底：源卡换格 / 死亡 / 主线开跑 / 请求者失活时自动淡出，
        /// 避免旋转、齐射期间残留过期的提示。
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
                switch (_mode)
                {
                    case GlowMode.Avatar:
                        stillValid = ResolveAvatarSlotOrMinusOne() == _originSlot;
                        break;
                    case GlowMode.Threat:
                        var field = GroundFieldGeometryHook.FieldOrNull();
                        stillValid = field != null
                            && field.TryGetSlotOf(_originUid, out var slot)
                            && slot == _originSlot;
                        break;
                    case GlowMode.MultiSelectCandidates:
                        stillValid = NineGrid.Cards.BoardCardSelectModeController.IsActive;
                        break;
                    case GlowMode.ItemSlots:
                    case GlowMode.BoardApplyZone:
                        // 道具拖拽或常驻模式：请求者失活即淡出
                        break;
                }
            }

            if (!stillValid)
            {
                _requester = null;
                _mode = GlowMode.None;
                ClearTargets();
            }
        }

        private void TickCellVisuals()
        {
            var dt = Time.unscaledDeltaTime;
            var breath = 1f - BreathDepth
                * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * BreathHz * 2f * Mathf.PI));

            // 1. 单格光圈更新
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

            // 2. 棋盘大光圈更新
            if (_boardFrameRenderer != null)
            {
                var boardTarget = _targetBoardWeight;
                var boardCurrent = _currentBoardWeight;
                if (boardTarget <= 0f && boardCurrent <= 0.001f)
                {
                    if (_boardFrameRenderer.enabled)
                    {
                        _boardFrameRenderer.enabled = false;
                    }
                }
                else
                {
                    var speed = boardTarget > boardCurrent ? 1f / FadeInSeconds : 1f / FadeOutSeconds;
                    boardCurrent = Mathf.MoveTowards(boardCurrent, boardTarget, speed * dt);
                    _currentBoardWeight = boardCurrent;

                    if (boardCurrent <= 0.001f && boardTarget <= 0f)
                    {
                        _boardFrameRenderer.enabled = false;
                    }
                    else
                    {
                        _boardFrameRenderer.enabled = true;
                        var alpha = Mathf.Clamp01(BaseAlpha * boardCurrent * breath);
                        _boardFrameRenderer.color = new Color(_activeColor.r, _activeColor.g, _activeColor.b, alpha);
                    }
                }
            }
        }

        private bool TryPrepareBoardFrameRenderer(GroundFieldView field)
        {
            if (field == null)
            {
                return false;
            }

            if (_boardFrameRenderer == null)
            {
                _boardFrameRenderer = CreateBoardFrameRenderer();
                if (_boardFrameRenderer == null)
                {
                    return false;
                }
            }

            Bounds? combined = null;
            int sortingLayerID = 0;
            int sortingOrder = 1;
            float z = transform.position.z;

            for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
            {
                var anchor = field.GetGroundAnchor(slot);
                if (anchor == null)
                {
                    continue;
                }

                z = anchor.position.z;
                var anchorRenderer = anchor.GetComponent<SpriteRenderer>();
                Bounds b;
                if (anchorRenderer != null)
                {
                    sortingLayerID = anchorRenderer.sortingLayerID;
                    sortingOrder = anchorRenderer.sortingOrder + 1;
                    b = anchorRenderer.bounds;
                }
                else
                {
                    b = new Bounds(anchor.position, new Vector3(3.8f, 4.9f, 0f));
                }

                if (!combined.HasValue)
                {
                    combined = b;
                }
                else
                {
                    var c = combined.Value;
                    c.Encapsulate(b);
                    combined = c;
                }
            }

            if (!combined.HasValue)
            {
                return false;
            }

            var totalBounds = combined.Value;
            const float BoardPadding = 0.25f;
            var sizeX = totalBounds.size.x + BoardPadding * 2f;
            var sizeY = totalBounds.size.y + BoardPadding * 2f;

            _boardFrameRenderer.sortingLayerID = sortingLayerID;
            _boardFrameRenderer.sortingOrder = sortingOrder;
            _boardFrameRenderer.transform.position = new Vector3(totalBounds.center.x, totalBounds.center.y, z);
            _boardFrameRenderer.size = new Vector2(sizeX, sizeY);
            return true;
        }

        private SpriteRenderer CreateBoardFrameRenderer()
        {
            var sprite = EnsureGlowSprite();
            if (sprite == null)
            {
                return null;
            }

            var go = new GameObject("BoardFrameRangeGlow");
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.sharedMaterial = EnsureGlowMaterial();
            renderer.color = new Color(_activeColor.r, _activeColor.g, _activeColor.b, 0f);
            renderer.enabled = false;
            return renderer;
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

            _targetBoardWeight = 0f;
        }
    }
}
