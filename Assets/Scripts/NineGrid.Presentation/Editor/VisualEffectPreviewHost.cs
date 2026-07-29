#if UNITY_EDITOR
using System;
using NineGrid.Cards;
using NineGrid.Cards.Anim;
using NineGrid.Cards.Presentation;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Presentation.Editor
{
    /// <summary>
    /// 特效库预览：左标准怪物卡参照 + 右精灵表特效；按内容与预览宽高比自动取景。
    /// </summary>
    public sealed class VisualEffectPreviewHost : IDisposable
    {
        public const string DefaultReferenceMonsterId = "monster.stone_man";

        private PreviewRenderUtility _previewUtility;
        private GameObject _sceneRoot;
        private CardFacePreviewBuilder.BuildResult _cardBuild;
        private SpriteRenderer _fxRenderer;
        private SpriteSheetLoopPlayer _player;
        private string _status = "就绪";
        private bool _disposed;
        private float _lastDrawAspect = -1f;

        public string Status => _status;
        public SpriteSheetLoopPlayer Player => _player;
        public GameObject SceneRoot => _sceneRoot;

        public bool Rebuild(VisualEffectEntryDto entry, CardFacePreviewRequest referenceCardRequest)
        {
            DestroyScene();
            if (entry == null || string.IsNullOrWhiteSpace(entry.sheetPath))
            {
                _status = "特效条目为空。";
                return false;
            }

            EnsurePreviewUtility();
            _sceneRoot = new GameObject("VisualEffectPreviewRoot");
            _sceneRoot.hideFlags = HideFlags.HideAndDontSave;

            if (referenceCardRequest == null)
            {
                referenceCardRequest = BuildFallbackMonsterRequest();
            }

            if (!CardFacePreviewBuilder.TryBuild(referenceCardRequest, out _cardBuild, out var cardError))
            {
                _status = "参照卡构建失败：" + (cardError ?? "unknown");
                DestroyScene();
                return false;
            }

            _cardBuild.Root.transform.SetParent(_sceneRoot.transform, false);
            _cardBuild.Root.transform.localPosition = new Vector3(-1.35f, 0f, 0f);
            CardMainVisualMaskAnchor.DisableMaskingForEditorPreview(_cardBuild.Root.transform);

            var fxGo = new GameObject("VisualEffectSprite");
            fxGo.hideFlags = HideFlags.HideAndDontSave;
            fxGo.transform.SetParent(_sceneRoot.transform, false);
            fxGo.transform.localPosition = new Vector3(1.35f, 0f, 0f);
            _fxRenderer = fxGo.AddComponent<SpriteRenderer>();
            _fxRenderer.sortingOrder = 200;
            _player = fxGo.AddComponent<SpriteSheetLoopPlayer>();
            _player.BindTarget(_fxRenderer);
            _player.Looping = true;
            _player.Fps = entry.defaultFps > 0.01f ? entry.defaultFps : 12f;
            _player.UniformScale = entry.defaultScale > 0.01f ? entry.defaultScale : 1f;

            var frames = CardAnimFrameSource.LoadFrames("atlas", entry.sheetPath);
            if (frames == null || frames.Length == 0)
            {
                _status = "无法加载精灵表：" + entry.sheetPath;
                _previewUtility.AddSingleGO(_sceneRoot);
                FrameCameraToContent();
                return false;
            }

            _player.SetFrames(frames, play: true);
            LayoutCardAndFx();
            _previewUtility.AddSingleGO(_sceneRoot);
            _lastDrawAspect = -1f;
            FrameCameraToContent();
            _status = "特效预览 " + entry.id + " · " + frames.Length + " 帧";
            return true;
        }

        public void ApplyScale(float scale)
        {
            if (_player != null)
            {
                _player.UniformScale = scale;
                LayoutCardAndFx();
                _lastDrawAspect = -1f;
                FrameCameraToContent();
            }
        }

        public void ApplyFps(float fps)
        {
            if (_player != null)
            {
                _player.Fps = fps;
            }
        }

        public void EditorTick(float deltaTime)
        {
            if (_player != null && _player.IsPlaying)
            {
                _player.EditorTick(deltaTime);
            }
        }

        public void Draw(Rect rect)
        {
            if (_disposed)
            {
                return;
            }

            EditorGUI.DrawRect(rect, new Color(0.09f, 0.075f, 0.06f, 1f));
            if (_sceneRoot == null || _previewUtility == null)
            {
                EditorGUI.LabelField(rect, _status, EditorStyles.centeredGreyMiniLabel);
                return;
            }

            if (rect.width < 8f || rect.height < 8f)
            {
                return;
            }

            var aspect = rect.width / Mathf.Max(1f, rect.height);
            if (Mathf.Abs(aspect - _lastDrawAspect) > 0.02f)
            {
                _lastDrawAspect = aspect;
                FrameCameraToContent(aspect);
            }

            _previewUtility.BeginPreview(rect, GUIStyle.none);
            _previewUtility.camera.Render();
            var texture = _previewUtility.EndPreview();
            if (texture != null)
            {
                GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            DestroyScene();
            if (_previewUtility != null)
            {
                _previewUtility.Cleanup();
                _previewUtility = null;
            }
        }

        /// <summary>
        /// 按卡面 / 特效实际半宽拉开间距，避免大 Scale 时互相叠住。
        /// </summary>
        private void LayoutCardAndFx()
        {
            if (_cardBuild?.Root == null || _fxRenderer == null)
            {
                return;
            }

            var cardBounds = CalculateRendererBounds(_cardBuild.Root);
            var fxExtent = EstimateFxHalfExtent();
            var cardHalf = Mathf.Max(0.4f, cardBounds.extents.x);
            var gap = 0.35f;
            var cardX = -(cardHalf + gap * 0.5f + fxExtent * 0.15f);
            var fxX = cardHalf + gap + fxExtent;
            // 以中点为 0，整体居中。
            var mid = (cardX + fxX) * 0.5f;
            _cardBuild.Root.transform.localPosition = new Vector3(cardX - mid, 0f, 0f);
            _fxRenderer.transform.localPosition = new Vector3(fxX - mid, 0f, 0f);
        }

        private float EstimateFxHalfExtent()
        {
            if (_fxRenderer == null)
            {
                return 0.8f;
            }

            var scale = _fxRenderer.transform.localScale.x;
            if (_fxRenderer.sprite != null)
            {
                return Mathf.Max(0.35f, _fxRenderer.bounds.extents.x);
            }

            return Mathf.Max(0.35f, 0.8f * scale);
        }

        private void FrameCameraToContent(float aspect = 0.95f)
        {
            if (_previewUtility == null || _sceneRoot == null)
            {
                return;
            }

            if (aspect < 0.2f)
            {
                aspect = 0.95f;
            }

            var cam = _previewUtility.camera;
            cam.orthographic = true;
            cam.transform.rotation = Quaternion.identity;

            var bounds = CalculateRendererBounds(_sceneRoot);
            if (bounds.size.sqrMagnitude < 0.0001f)
            {
                cam.orthographicSize = 2.1f;
                cam.transform.position = new Vector3(0f, 0f, -10f);
                return;
            }

            const float pad = 1.2f;
            var needHalfH = bounds.extents.y * pad;
            var needHalfW = bounds.extents.x * pad;
            // orthoSize 控半高；半宽 = orthoSize * aspect。
            cam.orthographicSize = Mathf.Max(needHalfH, needHalfW / aspect, 0.85f);
            cam.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10f);
        }

        private static Bounds CalculateRendererBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return new Bounds(root.transform.position, Vector3.zero);
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            return bounds;
        }

        private void EnsurePreviewUtility()
        {
            if (_previewUtility != null)
            {
                return;
            }

            _previewUtility = new PreviewRenderUtility();
            _previewUtility.camera.orthographic = true;
            _previewUtility.camera.nearClipPlane = 0.01f;
            _previewUtility.camera.farClipPlane = 50f;
            _previewUtility.camera.clearFlags = CameraClearFlags.SolidColor;
            _previewUtility.camera.backgroundColor = new Color(0.09f, 0.075f, 0.06f, 1f);
            if (_previewUtility.lights != null && _previewUtility.lights.Length > 0)
            {
                _previewUtility.lights[0].intensity = 1.2f;
                _previewUtility.lights[0].transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            }
        }

        private void DestroyScene()
        {
            _player = null;
            _fxRenderer = null;
            if (_cardBuild != null)
            {
                CardFacePreviewBuilder.DestroyBuild(_cardBuild);
                _cardBuild = null;
            }

            if (_sceneRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(_sceneRoot);
                _sceneRoot = null;
            }
        }

        public static CardFacePreviewRequest BuildFallbackMonsterRequest()
        {
            var request = new CardFacePreviewRequest
            {
                DefId = DefaultReferenceMonsterId,
                Kind = CardPresentationKind.Monster,
                DisplayName = "参照怪物",
                BasicDescription = string.Empty,
                Attack = 3,
                Armor = 1,
                Hp = 10,
                ActionCount = 2,
                FaceUp = true,
            };

            if (CardPresentationConfigCatalog.TryGet(DefaultReferenceMonsterId, out var dto) && dto != null)
            {
                var sprites = dto.sprites ?? new CardPresentationSpritesDto();
                var stats = dto.stats ?? new CardPresentationStatsDto();
                request.DisplayName = string.IsNullOrWhiteSpace(dto.displayName)
                    ? DefaultReferenceMonsterId
                    : dto.displayName;
                request.BasicDescription = dto.description ?? string.Empty;
                request.MainIcon = CardPresentationSpritePath.LoadSprite(sprites.mainIcon);
                request.FaceBackground = CardPresentationSpritePath.LoadSprite(sprites.faceBackground);
                request.BackBorder = CardPresentationSpritePath.LoadSprite(sprites.backBorder);
                request.BackShirt = CardPresentationSpritePath.LoadSprite(sprites.backShirt);
                request.BackLogo = CardPresentationSpritePath.LoadSprite(sprites.backLogo);
                request.Attack = Mathf.Max(0, stats.attack);
                request.Armor = Mathf.Max(0, stats.armor);
                request.Hp = Mathf.Max(0, stats.hp);
                request.ActionCount = Mathf.Max(0, stats.action);
            }

            return request;
        }
    }
}
#endif
