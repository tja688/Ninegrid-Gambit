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
    /// 特效库预览：左侧标准怪物卡参照，右侧精灵表特效（循环播放）。
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
        private Bounds _framedBounds;
        private bool _framedOnce;

        public string Status => _status;
        public SpriteSheetLoopPlayer Player => _player;
        public GameObject SceneRoot => _sceneRoot;

        public bool Rebuild(VisualEffectEntryDto entry, CardFacePreviewRequest referenceCardRequest)
        {
            DestroyScene();
            _framedOnce = false;
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
            _cardBuild.Root.transform.localPosition = new Vector3(-1.6f, 0f, 0f);
            CardMainVisualMaskAnchor.DisableMaskingForEditorPreview(_cardBuild.Root.transform);

            var fxGo = new GameObject("VisualEffectSprite");
            fxGo.hideFlags = HideFlags.HideAndDontSave;
            fxGo.transform.SetParent(_sceneRoot.transform, false);
            fxGo.transform.localPosition = new Vector3(1.6f, 0f, 0f);
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
                FrameCameraFixed();
                return false;
            }

            _player.SetFrames(frames, play: true);
            _previewUtility.AddSingleGO(_sceneRoot);
            FrameCameraFixed();
            _status = "特效预览 " + entry.id + " · " + frames.Length + " 帧";
            return true;
        }

        public void ApplyScale(float scale)
        {
            if (_player != null)
            {
                _player.UniformScale = scale;
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

        private void FrameCameraFixed()
        {
            if (_previewUtility == null)
            {
                return;
            }

            // 固定 framing：左卡 + 右特效对照稳定，不随缩放抖动。
            _framedBounds = new Bounds(Vector3.zero, new Vector3(5.2f, 3.6f, 1f));
            var cam = _previewUtility.camera;
            cam.orthographic = true;
            cam.orthographicSize = 2.1f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
            _framedOnce = true;
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
