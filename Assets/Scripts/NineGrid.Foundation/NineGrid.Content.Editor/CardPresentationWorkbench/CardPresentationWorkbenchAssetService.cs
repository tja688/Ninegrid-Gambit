#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using NineGrid.Cards;
using NineGrid.Cards.Anim;
using NineGrid.Cards.Presentation;
using NineGrid.Cards.Slots;
using NineGrid.Content.CardPresentation;
using NineGrid.Flow;
using NineGrid.Presentation.Editor;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    /// <summary>
    /// 工作台 PNG 资产服务：精灵裁切缩略图与卡面 / 模板 / 词条样例的离屏预览渲染。
    /// 复用 <see cref="CardFacePreviewBuilder"/> 底盘，与 Unity 窗口版预览同源同貌。
    /// </summary>
    internal sealed class CardPresentationWorkbenchAssetService : IDisposable
    {
        private const int MaxSpriteCacheEntries = 600;

        private readonly CardPresentationWorkbenchEditorState state;
        private readonly Dictionary<string, byte[]> spritePngCache =
            new Dictionary<string, byte[]>(StringComparer.Ordinal);
        private CardFacePreviewHost previewHost;

        public CardPresentationWorkbenchAssetService(CardPresentationWorkbenchEditorState state)
        {
            this.state = state;
        }

        public void InvalidateCaches()
        {
            spritePngCache.Clear();
        }

        public void Dispose()
        {
            spritePngCache.Clear();
            previewHost?.Dispose();
            previewHost = null;
        }

        public bool TryGetAsset(
            NameValueCollection query,
            out byte[] bytes,
            out string contentType,
            out string error)
        {
            bytes = null;
            contentType = "image/png";
            error = null;
            var kind = query["kind"] ?? string.Empty;
            switch (kind)
            {
                case "sprite":
                    return TryRenderSprite(query["path"], out bytes, out error);
                case "facePreview":
                    return TryRenderFacePreview(
                        query["contentId"],
                        !string.Equals(query["face"], "back", StringComparison.OrdinalIgnoreCase),
                        ParseSize(query["w"], 520),
                        ParseSize(query["h"], 640),
                        out bytes,
                        out error);
                case "templatePreview":
                    return TryRenderTemplatePreview(
                        query["templateId"],
                        ParseSize(query["w"], 520),
                        ParseSize(query["h"], 640),
                        out bytes,
                        out error);
                case "samplePreview":
                    return TryRenderSamplePreview(
                        query["cardKind"],
                        query["text"] ?? string.Empty,
                        ParseSize(query["w"], 520),
                        ParseSize(query["h"], 640),
                        out bytes,
                        out error);
                default:
                    error = "未知资产种类：" + kind;
                    return false;
            }
        }

        private static int ParseSize(string raw, int fallback)
        {
            return int.TryParse(raw, out var value) ? Mathf.Clamp(value, 8, 2048) : fallback;
        }

        private bool TryRenderSprite(string path, out byte[] bytes, out string error)
        {
            bytes = null;
            error = null;
            path = (path ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(path))
            {
                error = "path 为空。";
                return false;
            }

            if (spritePngCache.TryGetValue(path, out bytes) && bytes != null)
            {
                return true;
            }

            var sprite = CardPresentationSpritePath.LoadSprite(path);
            if (sprite == null)
            {
                error = "无法加载精灵：" + path;
                return false;
            }

            bytes = EncodeSpritePng(sprite);
            if (bytes == null)
            {
                error = "精灵编码失败：" + path;
                return false;
            }

            if (spritePngCache.Count >= MaxSpriteCacheEntries)
            {
                spritePngCache.Clear();
            }

            spritePngCache[path] = bytes;
            return true;
        }

        private static byte[] EncodeSpritePng(Sprite sprite)
        {
            var texture = sprite.texture;
            if (texture == null)
            {
                return null;
            }

            var rect = sprite.textureRect;
            var rt = RenderTexture.GetTemporary(
                texture.width,
                texture.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            Texture2D readable = null;
            Texture2D cropped = null;
            try
            {
                Graphics.Blit(texture, rt);
                RenderTexture.active = rt;
                readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
                readable.Apply();

                var x = Mathf.Clamp(Mathf.FloorToInt(rect.x), 0, texture.width - 1);
                var y = Mathf.Clamp(Mathf.FloorToInt(rect.y), 0, texture.height - 1);
                var w = Mathf.Clamp(Mathf.CeilToInt(rect.width), 1, texture.width - x);
                var h = Mathf.Clamp(Mathf.CeilToInt(rect.height), 1, texture.height - y);
                var pixels = readable.GetPixels(x, y, w, h);
                cropped = new Texture2D(w, h, TextureFormat.RGBA32, false);
                cropped.SetPixels(pixels);
                cropped.Apply();
                return cropped.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                if (readable != null)
                {
                    UnityEngine.Object.DestroyImmediate(readable);
                }

                if (cropped != null)
                {
                    UnityEngine.Object.DestroyImmediate(cropped);
                }
            }
        }

        private bool TryRenderFacePreview(
            string contentId,
            bool faceUp,
            int width,
            int height,
            out byte[] bytes,
            out string error)
        {
            bytes = null;
            error = null;
            var entry = state.FindFace(contentId) ?? state.FindDeck(contentId);
            if (entry?.Dto == null)
            {
                error = "未找到条目：" + contentId;
                return false;
            }

            if (!TryBuildPreviewRequest(entry.Dto, faceUp, out var request, out error))
            {
                return false;
            }

            return RenderRequest(request, entry.Dto, faceUp, width, height, out bytes, out error);
        }

        private bool TryRenderTemplatePreview(
            string templateId,
            int width,
            int height,
            out byte[] bytes,
            out string error)
        {
            bytes = null;
            error = null;
            templateId = (templateId ?? string.Empty).Trim();
            string designText = null;
            var templates = state.Session.EffectTemplates;
            for (var i = 0; i < templates.Count; i++)
            {
                var row = templates[i];
                if (row != null && string.Equals(row.id, templateId, StringComparison.Ordinal))
                {
                    designText = row.design_text ?? string.Empty;
                    break;
                }
            }

            if (designText == null)
            {
                error = "未找到效果模板：" + templateId;
                return false;
            }

            var request = new CardFacePreviewRequest
            {
                DefId = "preview.template",
                Kind = CardPresentationKind.HelpCard,
                DisplayName = string.Empty,
                BasicDescription = designText,
                FaceUp = true,
            };
            return RenderRequest(request, null, faceUp: true, width, height, out bytes, out error);
        }

        private bool TryRenderSamplePreview(
            string cardKind,
            string text,
            int width,
            int height,
            out byte[] bytes,
            out string error)
        {
            var kind = (cardKind ?? string.Empty).Trim() switch
            {
                "Avatar" => CardPresentationKind.Avatar,
                "HelpCard" => CardPresentationKind.HelpCard,
                "Relic" => CardPresentationKind.Relic,
                _ => CardPresentationKind.Monster,
            };

            var request = new CardFacePreviewRequest
            {
                DefId = "preview.richtext." + kind,
                Kind = kind,
                DisplayName = "描述富文本预览",
                BasicDescription = text ?? string.Empty,
                FaceUp = true,
            };
            return RenderRequest(request, null, faceUp: true, width, height, out bytes, out error);
        }

        private bool RenderRequest(
            CardFacePreviewRequest request,
            CardPresentationConfigDto dto,
            bool faceUp,
            int width,
            int height,
            out byte[] bytes,
            out string error)
        {
            bytes = null;
            error = null;
            state.EnsureDescriptionIconPipeline();
            previewHost ??= new CardFacePreviewHost();
            if (!previewHost.Rebuild(request))
            {
                error = previewHost.Status;
                return false;
            }

            var root = previewHost.PreviewRoot;
            if (root != null)
            {
                if (dto?.sprites != null)
                {
                    TryApplySpritePath(root.transform, CardFaceSlotCodes.CardFrame, dto.sprites.cardFrame);
                    TryApplySpritePath(root.transform, CardFaceSlotCodes.Banner, dto.sprites.banner);
                }

                CardMainVisualMaskAnchor.DisableMaskingForEditorPreview(root.transform);
                ApplyFaceVisibility(root.transform, faceUp);
            }

            if (!previewHost.TryRenderStaticPng(width, height, out bytes))
            {
                error = "预览渲染失败。";
                return false;
            }

            return true;
        }

        private static void TryApplySpritePath(Transform root, string slotCode, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var sprite = CardPresentationSpritePath.LoadSprite(path);
            if (sprite == null)
            {
                return;
            }

            if (CardFaceSlotNodeMap.TryFindRenderer(root, slotCode, out var renderer))
            {
                renderer.sprite = sprite;
                renderer.enabled = true;
            }
        }

        private static void ApplyFaceVisibility(Transform root, bool showFront)
        {
            if (root == null)
            {
                return;
            }

            var front = CardPresentationFlipPreview.FindFrontRoot(root);
            var back = CardPresentationFlipPreview.FindBackRoot(root);
            if (front != null)
            {
                front.gameObject.SetActive(showFront);
            }

            if (back != null)
            {
                back.gameObject.SetActive(!showFront);
            }
        }

        private bool TryBuildPreviewRequest(
            CardPresentationConfigDto dto,
            bool faceUp,
            out CardFacePreviewRequest request,
            out string error)
        {
            request = null;
            error = null;
            var session = state.Session;
            var isDeckEntry = string.Equals(dto.kind, "Deck", StringComparison.OrdinalIgnoreCase);

            CardPresentationKind kind;
            if (isDeckEntry)
            {
                kind = ResolveDeckPreviewKind(dto.contentId);
            }
            else
            {
                if (!Enum.TryParse(dto.kind, true, out ContentVisualKind contentKind))
                {
                    contentKind = ContentVisualKind.Unknown;
                }

                kind = CardFacePreviewBuilder.ToPresentationKind(contentKind, dto.contentId);
                if (kind == CardPresentationKind.Unknown)
                {
                    kind = FallbackKind(dto.kind);
                }
            }

            if (kind == CardPresentationKind.Unknown)
            {
                error = "该 Kind 不挂卡面预览：" + dto.contentId;
                return false;
            }

            var sprites = dto.sprites ?? new CardPresentationSpritesDto();
            var stats = dto.stats ?? new CardPresentationStatsDto();
            var backBorderPath = sprites.backBorder;
            var backShirtPath = sprites.backShirt;
            var backLogoPath = sprites.backLogo;
            if (!isDeckEntry
                && session.TryGetDeckDto(dto.deckId, out var deckDto)
                && deckDto?.sprites != null)
            {
                if (!string.IsNullOrWhiteSpace(deckDto.sprites.backBorder))
                {
                    backBorderPath = deckDto.sprites.backBorder;
                }

                if (!string.IsNullOrWhiteSpace(deckDto.sprites.backShirt))
                {
                    backShirtPath = deckDto.sprites.backShirt;
                }

                if (!string.IsNullOrWhiteSpace(deckDto.sprites.backLogo))
                {
                    backLogoPath = deckDto.sprites.backLogo;
                }
            }

            request = new CardFacePreviewRequest
            {
                DefId = dto.contentId,
                Kind = kind,
                DisplayName = string.IsNullOrWhiteSpace(dto.displayName)
                    ? session.GetDisplayName(dto.contentId, dto.kind)
                    : dto.displayName,
                BasicDescription = isDeckEntry
                    ? string.Empty
                    : CardFaceDescriptionProjector.Project(
                        CardDescriptionProjectionMode.Instance,
                        dto.description ?? string.Empty,
                        dto.effectAssemblies),
                RoomIconPrefabPath = dto.iconPrefab ?? string.Empty,
                MainIcon = isDeckEntry ? null : CardPresentationSpritePath.LoadSprite(sprites.mainIcon),
                FaceBackground = isDeckEntry
                    ? null
                    : CardPresentationSpritePath.LoadSprite(sprites.faceBackground),
                BackBorder = CardPresentationSpritePath.LoadSprite(backBorderPath),
                BackShirt = CardPresentationSpritePath.LoadSprite(backShirtPath),
                BackLogo = CardPresentationSpritePath.LoadSprite(backLogoPath),
                Attack = isDeckEntry ? 0 : Mathf.Max(0, stats.attack),
                Armor = isDeckEntry ? 0 : Mathf.Max(0, stats.armor),
                Hp = isDeckEntry ? 0 : Mathf.Max(0, stats.hp),
                ActionCount = isDeckEntry
                    ? 0
                    : Mathf.Max(0, dto.rhythmPeriod > 0 ? dto.rhythmPeriod : stats.action),
                FaceUp = faceUp,
            };

            if (!isDeckEntry && kind == CardPresentationKind.Trap)
            {
                var trapVisual = CoreCardPresentationMapper.BuildVisualSnapshotFromDefId(dto.contentId, kind);
                request.ActionCount = trapVisual.ActionCount;
                request.ShowActionCount = trapVisual.ShowActionCount;
            }

            return true;
        }

        private static CardPresentationKind FallbackKind(string kind)
        {
            if (CardPresentationEditorSession.IsItemLikeKind(kind))
            {
                return CardPresentationKind.HelpCard;
            }

            if (string.Equals(kind, "Avatar", StringComparison.OrdinalIgnoreCase))
            {
                return CardPresentationKind.Avatar;
            }

            if (string.Equals(kind, "Monster", StringComparison.OrdinalIgnoreCase))
            {
                return CardPresentationKind.Monster;
            }

            if (string.Equals(kind, "Trap", StringComparison.OrdinalIgnoreCase))
            {
                return CardPresentationKind.Trap;
            }

            if (string.Equals(kind, "Relic", StringComparison.OrdinalIgnoreCase))
            {
                return CardPresentationKind.Relic;
            }

            if (string.Equals(kind, "Room", StringComparison.OrdinalIgnoreCase))
            {
                return CardPresentationKind.Room;
            }

            if (string.Equals(kind, "ChoiceOption", StringComparison.OrdinalIgnoreCase))
            {
                return CardPresentationKind.ChoiceOption;
            }

            return CardPresentationKind.Unknown;
        }

        private static CardPresentationKind ResolveDeckPreviewKind(string deckContentId)
        {
            if (string.IsNullOrWhiteSpace(deckContentId))
            {
                return CardPresentationKind.Monster;
            }

            var id = deckContentId.Trim();
            if (string.Equals(id, CardPresentationEditorSession.PlayerDeckId, StringComparison.OrdinalIgnoreCase)
                || id.IndexOf("player", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return CardPresentationKind.Avatar;
            }

            if (id.IndexOf("help", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return CardPresentationKind.HelpCard;
            }

            if (id.IndexOf("trap", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return CardPresentationKind.Trap;
            }

            if (id.IndexOf("relic", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return CardPresentationKind.Relic;
            }

            return CardPresentationKind.Monster;
        }
    }
}
#endif
