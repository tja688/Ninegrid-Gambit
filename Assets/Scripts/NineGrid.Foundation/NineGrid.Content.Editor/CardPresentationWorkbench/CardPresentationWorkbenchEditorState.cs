#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using NineGrid.Cards;
using NineGrid.Cards.Presentation;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    /// <summary>
    /// 表现层配置网页工作台的作者态权威：包装 <see cref="CardPresentationEditorSession"/>，
    /// 提供 revision 快照与命令分发。磁盘写入复用既有 JsonIO / 表 IO，与 Unity 窗口版同一真源。
    /// </summary>
    public sealed class CardPresentationWorkbenchEditorState
    {
        private static CardPresentationWorkbenchEditorState instance;

        private readonly CardPresentationEditorSession session = new CardPresentationEditorSession();
        private CardPresentationWorkbenchAssetService assetService;
        private CardFaceDescriptionInlineIconStyleSO inlineIconStyle;
        private CardFaceDescriptionIconCatalogSO iconCatalog;
        private bool inlineIconStyleDirty;
        private bool iconCatalogDirty;
        private List<CardPresentationInsertableIcon> insertableIconsCache;
        private long localRevision = 1;
        private string statusMessage = string.Empty;
        private string errorMessage = string.Empty;
        private bool loaded;

        public static CardPresentationWorkbenchEditorState Instance =>
            instance ??= new CardPresentationWorkbenchEditorState();

        public CardPresentationEditorSession Session => session;

        public long LocalRevision => localRevision;

        public static void ResetForTests()
        {
            instance?.assetService?.Dispose();
            instance = null;
        }

        public void InitializeFromDisk()
        {
            if (loaded)
            {
                return;
            }

            ReloadAll();
            loaded = true;
        }

        public void OnBeforeAssemblyReload()
        {
            assetService?.Dispose();
            assetService = null;
        }

        public void TickObservation()
        {
        }

        public bool TryGetAsset(
            System.Collections.Specialized.NameValueCollection query,
            out byte[] bytes,
            out string contentType,
            out string error)
        {
            assetService ??= new CardPresentationWorkbenchAssetService(this);
            return assetService.TryGetAsset(query, out bytes, out contentType, out error);
        }

        public void EnsureDescriptionIconPipeline()
        {
            EnsureGlossaryLoaded();
            CardFacePresentationBinder.SetInlineIconStyleOverride(inlineIconStyle);
            CardFacePresentationBinder.SetDescriptionIconCatalogOverride(iconCatalog);
        }

        public bool TryDispatchCommand(string command, string payloadJson, out object payload, out string error)
        {
            payload = null;
            error = null;
            JObject request;
            try
            {
                request = string.IsNullOrWhiteSpace(payloadJson) ? new JObject() : JObject.Parse(payloadJson);
            }
            catch (Exception ex)
            {
                error = "payload 解析失败：" + ex.Message;
                return false;
            }

            try
            {
                switch (command)
                {
                    case "ping":
                        payload = new { ok = true, revision = localRevision };
                        return true;
                    case "reload":
                        ReloadAll();
                        payload = new { reloaded = true };
                        return true;
                    case "saveAll":
                        return DispatchSaveAll(out payload, out error);
                    case "saveFace":
                        return DispatchSaveEntry(request, isDeck: false, out payload, out error);
                    case "saveDeck":
                        return DispatchSaveEntry(request, isDeck: true, out payload, out error);
                    case "saveTemplates":
                        return DispatchSaveTemplates(out payload, out error);
                    case "saveVisualEffects":
                        return DispatchSaveVisualEffects(out payload, out error);
                    case "saveGlossary":
                        return DispatchSaveGlossary(out payload, out error);
                    case "openFace":
                        return DispatchOpenFace(request, out payload, out error);
                    case "updateFace":
                        return DispatchUpdateFace(request, out payload, out error);
                    case "updateDeck":
                        return DispatchUpdateDeck(request, out payload, out error);
                    case "createDeck":
                        return DispatchCreateDeck(request, out payload, out error);
                    case "updateTemplate":
                        return DispatchUpdateTemplate(request, out payload, out error);
                    case "updateVisualEffect":
                        return DispatchUpdateVisualEffect(request, out payload, out error);
                    case "suggestArgs":
                    {
                        var templateId = request.Value<string>("templateId") ?? string.Empty;
                        payload = new { argsJson = session.SuggestArgsJsonForTemplate(templateId) };
                        return true;
                    }
                    case "glossaryAdd":
                        return DispatchGlossaryAdd(out payload, out error);
                    case "glossaryLayout":
                        return DispatchGlossaryLayout(request, out payload, out error);
                    case "glossaryUpdate":
                        return DispatchGlossaryUpdate(request, out payload, out error);
                    case "glossaryRemove":
                        return DispatchGlossaryRemove(request, out payload, out error);
                    case "exportIndex":
                    {
                        var ok = session.TryExportIndex(out var message);
                        statusMessage = message;
                        BumpRevision();
                        if (!ok)
                        {
                            error = message;
                            return false;
                        }

                        payload = new { message };
                        return true;
                    }
                    case "migrateLegacy":
                    {
                        var created = session.MigrateFromLegacyCatalogs(out var message);
                        statusMessage = message;
                        BumpRevision();
                        payload = new { created, message };
                        return true;
                    }
                    case "listSprites":
                        return DispatchListSprites(request, out payload, out error);
                    case "animFrames":
                        return DispatchAnimFrames(request, out payload, out error);
                    case "vfxFrames":
                        return DispatchVfxFrames(request, out payload, out error);
                    default:
                        error = "未知命令：" + command;
                        return false;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public object BuildSnapshotPayload()
        {
            EnsureGlossaryLoaded();
            var faces = new List<object>(session.FaceEntries.Count);
            for (var i = 0; i < session.FaceEntries.Count; i++)
            {
                var entry = session.FaceEntries[i];
                if (entry?.Dto == null)
                {
                    continue;
                }

                faces.Add(BuildFacePayload(entry));
            }

            var decks = new List<object>(session.DeckEntries.Count);
            for (var i = 0; i < session.DeckEntries.Count; i++)
            {
                var entry = session.DeckEntries[i];
                if (entry?.Dto == null)
                {
                    continue;
                }

                decks.Add(BuildDeckPayload(entry));
            }

            var templates = new List<object>(session.EffectTemplates.Count);
            for (var i = 0; i < session.EffectTemplates.Count; i++)
            {
                var row = session.EffectTemplates[i];
                if (row == null || string.IsNullOrWhiteSpace(row.id))
                {
                    continue;
                }

                var category = CardPresentationEditorSession.ResolveEffectTemplateCategory(row.id);
                templates.Add(new
                {
                    id = row.id,
                    state = row.state ?? string.Empty,
                    designText = row.design_text ?? string.Empty,
                    paramText = EffectDesignTextParameterizer.Parameterize(
                        row.design_text,
                        row.body,
                        (IReadOnlyDictionary<string, object>)null),
                    requiresJson = row.requires_json ?? "[]",
                    conditionsJson = row.conditions_json ?? "[]",
                    body = row.body ?? string.Empty,
                    category = category.ToString(),
                    categoryTitle = CardPresentationEditorSession.ResolveEffectCategoryTitle(category),
                    isDirty = row.IsDirty,
                });
            }

            var vfx = new List<object>(session.VisualEffects.Count);
            for (var i = 0; i < session.VisualEffects.Count; i++)
            {
                var row = session.VisualEffects[i];
                var dto = row?.Dto;
                if (dto == null)
                {
                    continue;
                }

                vfx.Add(new
                {
                    id = dto.id,
                    category = dto.category,
                    categoryTitle = VisualEffectCatalogEditorIO.GetCategoryLabel(dto.category),
                    variantId = dto.variantId,
                    size = dto.size,
                    color = dto.color,
                    sheetPath = dto.sheetPath,
                    defaultFps = dto.defaultFps,
                    defaultScale = dto.defaultScale,
                    displayName = dto.displayName ?? string.Empty,
                    isDirty = row.IsDirty,
                });
            }

            return new
            {
                isPlayMode = EditorApplication.isPlaying,
                status = statusMessage,
                error = errorMessage,
                dirtyCount = ComputeDirtyCount(),
                contentArtRoot = CardPresentationContentArt.RootAssetFolder,
                animSlotIds = CardAnimSlotIds.All,
                attackPatterns = new[]
                {
                    AttackPatternRules.TokenNone,
                    AttackPatternRules.TokenOrthogonalMelee,
                    AttackPatternRules.TokenDiagonalMelee,
                    AttackPatternRules.TokenOmnidirectionalMelee,
                },
                rhythmSources = new[] { CardRhythmRules.TokenAction, CardRhythmRules.TokenMove },
                deckChoices = session.GetDeckChoices()
                    .Select(c => new { deckId = c.DeckId, label = c.Label })
                    .ToArray(),
                faces,
                decks,
                templates,
                visualEffects = vfx,
                glossary = BuildGlossaryPayload(),
            };
        }

        private object BuildFacePayload(CardPresentationEditorEntry entry)
        {
            var dto = entry.Dto;
            CardPresentationEditorSession.NormalizeLogicMounts(dto);
            var stats = dto.stats ?? new CardPresentationStatsDto();
            var sprites = dto.sprites ?? new CardPresentationSpritesDto();
            var mv = dto.mainVisual ?? new CardPresentationMainVisualDto { uniformScale = 1f };
            var anims = dto.animations;

            var assemblies = new List<object>();
            if (dto.effectAssemblies != null)
            {
                for (var i = 0; i < dto.effectAssemblies.Length; i++)
                {
                    var assembly = dto.effectAssemblies[i];
                    if (assembly == null)
                    {
                        continue;
                    }

                    assemblies.Add(new
                    {
                        id = assembly.id ?? string.Empty,
                        templateId = assembly.templateId ?? string.Empty,
                        containerType = assembly.containerType ?? string.Empty,
                        argsJson = assembly.argsJson ?? string.Empty,
                        brief = session.GetParameterizedDesignText(assembly.templateId, assembly.argsJson),
                        sendableKeys = CollectSendableKeys(assembly),
                    });
                }
            }

            var animSlots = new List<object>();
            if (anims?.slots != null)
            {
                for (var i = 0; i < anims.slots.Length; i++)
                {
                    var slot = anims.slots[i];
                    if (slot == null)
                    {
                        continue;
                    }

                    animSlots.Add(new
                    {
                        id = slot.id ?? string.Empty,
                        sourceType = slot.sourceType ?? "none",
                        path = slot.path ?? string.Empty,
                        offsetX = slot.offsetX,
                        offsetY = slot.offsetY,
                    });
                }
            }

            var extraSlots = new List<object>();
            if (dto.extraSlots != null)
            {
                for (var i = 0; i < dto.extraSlots.Length; i++)
                {
                    var slot = dto.extraSlots[i];
                    if (slot == null)
                    {
                        continue;
                    }

                    extraSlots.Add(new { code = slot.code ?? string.Empty, path = slot.path ?? string.Empty });
                }
            }

            return new
            {
                contentId = dto.contentId,
                kind = dto.kind ?? string.Empty,
                deckId = dto.deckId ?? string.Empty,
                displayName = dto.displayName ?? string.Empty,
                designSlotName = dto.designSlotName ?? string.Empty,
                description = dto.description ?? string.Empty,
                descriptionLocked = session.InferDescriptionCustomLocked(dto),
                faceIntro = dto.faceIntro ?? string.Empty,
                extraGlossaryTerms = dto.extraGlossaryTerms ?? Array.Empty<string>(),
                attackPattern = dto.attackPattern ?? string.Empty,
                rhythmSource = dto.rhythmSource ?? string.Empty,
                rhythmPeriod = dto.rhythmPeriod,
                gold = dto.gold,
                rarity = dto.rarity ?? string.Empty,
                level = dto.level ?? string.Empty,
                sequence = dto.sequence,
                isReserve = dto.isReserve,
                weight = dto.weight,
                rewardPoolId = dto.rewardPoolId ?? string.Empty,
                shopOfferCount = dto.shopOfferCount,
                iconPrefab = dto.iconPrefab ?? string.Empty,
                boardSlot = dto.boardSlot,
                openingInjectsJson = SerializeOpeningInjects(dto.openingInjects),
                stats = new
                {
                    hp = stats.hp,
                    armor = stats.armor,
                    attack = stats.attack,
                    action = stats.action,
                    recovery = stats.recovery,
                },
                sprites = new
                {
                    mainIcon = sprites.mainIcon ?? string.Empty,
                    faceBackground = sprites.faceBackground ?? string.Empty,
                    cardFrame = sprites.cardFrame ?? string.Empty,
                    banner = sprites.banner ?? string.Empty,
                    backBorder = sprites.backBorder ?? string.Empty,
                    backShirt = sprites.backShirt ?? string.Empty,
                    backLogo = sprites.backLogo ?? string.Empty,
                },
                mainVisual = new { offsetX = mv.offsetX, offsetY = mv.offsetY, uniformScale = mv.uniformScale },
                animations = new { defaultFps = anims?.defaultFps ?? 8f, slots = animSlots },
                extraSlots,
                effectAssemblies = assemblies,
                skillIds = dto.skillIds ?? Array.Empty<string>(),
                isDirty = entry.IsDirty,
            };
        }

        private object BuildDeckPayload(CardPresentationEditorEntry entry)
        {
            var dto = entry.Dto;
            var sprites = dto.sprites ?? new CardPresentationSpritesDto();
            var usage = MonsterDeckUsageInspector.InspectDeck(dto.contentId, session);
            return new
            {
                contentId = dto.contentId,
                displayName = dto.displayName ?? string.Empty,
                backBorder = sprites.backBorder ?? string.Empty,
                backShirt = sprites.backShirt ?? string.Empty,
                backLogo = sprites.backLogo ?? string.Empty,
                isDirty = entry.IsDirty,
                usage = new
                {
                    status = usage.Status.ToString(),
                    headline = usage.StatusHeadline ?? string.Empty,
                    detail = usage.DetailLines ?? string.Empty,
                },
            };
        }

        private object BuildGlossaryPayload()
        {
            EnsureGlossaryLoaded();
            var entries = new List<object>();
            var list = iconCatalog != null ? iconCatalog.Entries : null;
            if (list != null)
            {
                for (var i = 0; i < list.Count; i++)
                {
                    var entry = list[i];
                    if (entry == null)
                    {
                        continue;
                    }

                    float bx = 0f, by = 0f, scale = 1f;
                    if (!string.IsNullOrEmpty(entry.code))
                    {
                        inlineIconStyle.Resolve(entry.code, out bx, out by, out scale);
                    }

                    entries.Add(new
                    {
                        index = i,
                        code = entry.code ?? string.Empty,
                        displayNameZh = entry.displayNameZh ?? string.Empty,
                        explanation = entry.explanation ?? string.Empty,
                        colorHex = entry.HasColorOverride
                            ? "#" + ColorUtility.ToHtmlStringRGB(entry.color)
                            : string.Empty,
                        spritePath = CardPresentationMigration.AssetPathOrEmpty(entry.sprite),
                        bearingX = bx,
                        bearingY = by,
                        baseScale = scale,
                    });
                }
            }

            var insertable = new List<object>();
            var icons = insertableIconsCache ??= session.GetInsertableIcons();
            for (var i = 0; i < icons.Count; i++)
            {
                var icon = icons[i];
                inlineIconStyle.Resolve(icon.Code, out var bx, out var by, out var scale);
                insertable.Add(new
                {
                    code = icon.Code,
                    displayNameZh = icon.DisplayNameZh,
                    bearingX = bx,
                    bearingY = by,
                    baseScale = scale,
                });
            }

            return new
            {
                dirty = inlineIconStyleDirty || iconCatalogDirty,
                entries,
                insertable,
            };
        }

        private object[] CollectSendableKeys(EffectAssemblyDto assembly)
        {
            if (assembly == null
                || string.IsNullOrWhiteSpace(assembly.id)
                || string.IsNullOrWhiteSpace(assembly.argsJson))
            {
                return Array.Empty<object>();
            }

            var args = EffectAssemblyResolver.ParseArgsJson(assembly.argsJson);
            var result = new List<object>(args.Count);
            foreach (var pair in args)
            {
                if (string.IsNullOrEmpty(pair.Key) || EffectDesignTextParameterizer.IsOpaqueArgKey(pair.Key))
                {
                    continue;
                }

                result.Add(new
                {
                    key = pair.Key,
                    value = Convert.ToString(pair.Value, CultureInfo.InvariantCulture) ?? string.Empty,
                });
            }

            return result.ToArray();
        }

        private int ComputeDirtyCount()
        {
            var count = session.DirtyCount;
            if (inlineIconStyleDirty || iconCatalogDirty)
            {
                count++;
            }

            return count;
        }

        private void ReloadAll()
        {
            session.Reload();
            inlineIconStyle = null;
            iconCatalog = null;
            inlineIconStyleDirty = false;
            iconCatalogDirty = false;
            insertableIconsCache = null;
            EnsureGlossaryLoaded();
            assetService?.InvalidateCaches();
            errorMessage = string.Empty;
            statusMessage = "已从磁盘加载 " + session.FaceEntries.Count + " 张卡面 / "
                            + session.DeckEntries.Count + " 套卡组 / "
                            + session.EffectTemplates.Count + " 条效果模板 / "
                            + session.VisualEffects.Count + " 条特效。";
            BumpRevision();
        }

        private bool DispatchSaveAll(out object payload, out string error)
        {
            payload = null;
            PersistGlossaryIfDirty();
            if (!session.TrySaveAll(out error))
            {
                errorMessage = error ?? "保存失败。";
                BumpRevision();
                return false;
            }

            errorMessage = string.Empty;
            statusMessage = "已保存全部改动（Authoring + StreamingAssets）。";
            BumpRevision();
            payload = new { saved = true };
            return true;
        }

        private bool DispatchSaveEntry(JObject request, bool isDeck, out object payload, out string error)
        {
            payload = null;
            error = null;
            var contentId = request.Value<string>("contentId") ?? string.Empty;
            var entry = isDeck ? FindDeck(contentId) : FindFace(contentId);
            if (entry?.Dto == null)
            {
                error = "未找到条目：" + contentId;
                return false;
            }

            if (!entry.IsDirty)
            {
                payload = new { saved = false };
                return true;
            }

            CardPresentationJsonIO.EnsureDirectoriesExist();
            if (!isDeck)
            {
                session.TryExpandSkillIdsIntoAssemblies(entry.Dto);
                CardPresentationEditorSession.NormalizeLogicMounts(entry.Dto);
            }

            CardPresentationJsonIO.SaveAuthoring(entry.Dto);
            entry.MarkSaved();
            CardPresentationConfigCatalog.Invalidate();
            statusMessage = "已保存 " + contentId + "。";
            errorMessage = string.Empty;
            BumpRevision();
            payload = new { saved = true };
            return true;
        }

        private bool DispatchSaveTemplates(out object payload, out string error)
        {
            payload = null;
            var rows = session.EffectTemplates.ToList();
            if (!EffectTemplateEditorIO.TrySaveAll(rows, out error))
            {
                errorMessage = error ?? "保存效果模板失败。";
                BumpRevision();
                return false;
            }

            statusMessage = "已保存效果模板表。";
            errorMessage = string.Empty;
            BumpRevision();
            payload = new { saved = true };
            return true;
        }

        private bool DispatchSaveVisualEffects(out object payload, out string error)
        {
            payload = null;
            var rows = session.VisualEffects.ToList();
            if (!VisualEffectCatalogEditorIO.TrySaveAll(rows, out error))
            {
                errorMessage = error ?? "保存特效库失败。";
                BumpRevision();
                return false;
            }

            statusMessage = "已保存 visual_effects.json。";
            errorMessage = string.Empty;
            BumpRevision();
            payload = new { saved = true };
            return true;
        }

        private bool DispatchSaveGlossary(out object payload, out string error)
        {
            payload = null;
            error = null;
            PersistGlossaryIfDirty();
            statusMessage = "已保存词条表与图标样式。";
            errorMessage = string.Empty;
            BumpRevision();
            payload = new { saved = true };
            return true;
        }

        private bool DispatchOpenFace(JObject request, out object payload, out string error)
        {
            payload = null;
            error = null;
            var contentId = request.Value<string>("contentId") ?? string.Empty;
            var entry = FindFace(contentId);
            if (entry?.Dto == null)
            {
                error = "未找到卡面：" + contentId;
                return false;
            }

            var changed = session.TryExpandSkillIdsIntoAssemblies(entry.Dto);
            var locked = session.InferDescriptionCustomLocked(entry.Dto);
            if (!locked && session.TrySyncAutoDescription(entry.Dto, customLocked: false))
            {
                changed = true;
            }

            if (changed)
            {
                BumpRevision();
            }

            payload = new { expanded = changed };
            return true;
        }

        private bool DispatchUpdateFace(JObject request, out object payload, out string error)
        {
            payload = null;
            error = null;
            var contentId = request.Value<string>("contentId") ?? string.Empty;
            var entry = FindFace(contentId);
            if (entry?.Dto == null)
            {
                error = "未找到卡面：" + contentId;
                return false;
            }

            var dto = entry.Dto;
            var lockedBefore = session.InferDescriptionCustomLocked(dto);

            ApplyString(request, "displayName", v => dto.displayName = v);
            ApplyString(request, "designSlotName", v => dto.designSlotName = v);
            ApplyString(request, "faceIntro", v => dto.faceIntro = v);
            ApplyString(request, "deckId", v => dto.deckId = v);
            ApplyString(request, "attackPattern", v => dto.attackPattern = v);
            ApplyString(request, "rewardPoolId", v => dto.rewardPoolId = v);
            ApplyString(request, "iconPrefab", v => dto.iconPrefab = v);
            ApplyInt(request, "gold", v => dto.gold = v);
            ApplyInt(request, "weight", v => dto.weight = v);
            ApplyInt(request, "shopOfferCount", v => dto.shopOfferCount = v);
            ApplyInt(request, "boardSlot", v => dto.boardSlot = Mathf.Clamp(v, 0, 9));
            ApplyBool(request, "isReserve", v => dto.isReserve = v);
            ApplyString(request, "rhythmSource", v => dto.rhythmSource = v);
            ApplyInt(request, "rhythmPeriod", v =>
            {
                dto.rhythmPeriod = Mathf.Max(0, v);
                if (dto.stats != null)
                {
                    dto.stats.action = dto.rhythmPeriod;
                }
            });

            if (request.TryGetValue("sequence", out var seqToken))
            {
                dto.sequence = Mathf.Clamp(seqToken.Value<int>(), 0, 5);
                if (dto.sequence == 5)
                {
                    dto.level = "层主";
                    dto.isBoss = true;
                    dto.isElite = false;
                }
                else if (dto.sequence > 0 && string.Equals(dto.level, "层主", StringComparison.Ordinal))
                {
                    dto.level = "普通";
                    dto.isBoss = false;
                }
            }

            if (request.TryGetValue("level", out var levelToken))
            {
                dto.level = levelToken.Value<string>() ?? string.Empty;
                if (string.Equals(dto.level.Trim(), "层主", StringComparison.Ordinal))
                {
                    dto.isBoss = true;
                    dto.isElite = false;
                    if (dto.sequence <= 0)
                    {
                        dto.sequence = 5;
                    }
                }
            }

            if (request.TryGetValue("openingInjectsJson", out var injectToken))
            {
                dto.openingInjects = ParseOpeningInjects(injectToken.Value<string>());
            }

            if (request["stats"] is JObject statsObj)
            {
                dto.stats ??= new CardPresentationStatsDto();
                ApplyInt(statsObj, "hp", v => dto.stats.hp = v);
                ApplyInt(statsObj, "armor", v => dto.stats.armor = v);
                ApplyInt(statsObj, "attack", v => dto.stats.attack = v);
                ApplyInt(statsObj, "action", v => dto.stats.action = v);
                ApplyInt(statsObj, "recovery", v => dto.stats.recovery = v);
            }

            if (request["mainVisual"] is JObject mvObj)
            {
                dto.mainVisual ??= new CardPresentationMainVisualDto { uniformScale = 1f };
                ApplyFloat(mvObj, "offsetX", v => dto.mainVisual.offsetX = v);
                ApplyFloat(mvObj, "offsetY", v => dto.mainVisual.offsetY = v);
                ApplyFloat(mvObj, "uniformScale", v => dto.mainVisual.uniformScale = v);
            }

            if (request["sprites"] is JObject spritesObj)
            {
                dto.sprites ??= new CardPresentationSpritesDto();
                ApplyString(spritesObj, "mainIcon", v => dto.sprites.mainIcon = v);
                ApplyString(spritesObj, "faceBackground", v => dto.sprites.faceBackground = v);
                ApplyString(spritesObj, "cardFrame", v => dto.sprites.cardFrame = v);
                ApplyString(spritesObj, "banner", v => dto.sprites.banner = v);
                ApplyString(spritesObj, "backBorder", v => dto.sprites.backBorder = v);
                ApplyString(spritesObj, "backShirt", v => dto.sprites.backShirt = v);
                ApplyString(spritesObj, "backLogo", v => dto.sprites.backLogo = v);
            }

            if (request["animations"] is JObject animObj)
            {
                dto.animations ??= new CardPresentationAnimationsDto { defaultFps = 8f };
                ApplyFloat(animObj, "defaultFps", v => dto.animations.defaultFps = Mathf.Max(0.01f, v));
                if (animObj["slots"] is JArray slotArray)
                {
                    dto.animations.slots = slotArray
                        .OfType<JObject>()
                        .Select(o => new CardPresentationAnimSlotDto
                        {
                            id = o.Value<string>("id") ?? string.Empty,
                            sourceType = o.Value<string>("sourceType") ?? "none",
                            path = o.Value<string>("path") ?? string.Empty,
                            offsetX = o.Value<float?>("offsetX") ?? 0f,
                            offsetY = o.Value<float?>("offsetY") ?? 0f,
                        })
                        .ToArray();
                }
            }

            if (request["extraSlots"] is JArray extraArray)
            {
                dto.extraSlots = extraArray
                    .OfType<JObject>()
                    .Select(o => new CardPresentationExtraSlotDto
                    {
                        code = o.Value<string>("code") ?? string.Empty,
                        path = o.Value<string>("path") ?? string.Empty,
                    })
                    .ToArray();
            }

            if (request["effectAssemblies"] is JArray assemblyArray)
            {
                var containerType = InferDefaultContainerType(dto.kind);
                dto.effectAssemblies = assemblyArray
                    .OfType<JObject>()
                    .Select(o => new EffectAssemblyDto
                    {
                        id = o.Value<string>("id") ?? string.Empty,
                        templateId = o.Value<string>("templateId") ?? string.Empty,
                        containerType = string.IsNullOrWhiteSpace(o.Value<string>("containerType"))
                            ? containerType
                            : o.Value<string>("containerType"),
                        argsJson = o.Value<string>("argsJson") ?? "{}",
                    })
                    .ToArray();
                dto.skillIds = Array.Empty<string>();
                if (dto.effectAssemblies.Length == 0)
                {
                    dto.effectIds = Array.Empty<string>();
                }

                if (!lockedBefore)
                {
                    session.TrySyncAutoDescription(dto, customLocked: false);
                }
            }

            if (request.TryGetValue("description", out var descToken))
            {
                var text = descToken.Value<string>() ?? string.Empty;
                dto.description = text;
                if (string.IsNullOrWhiteSpace(text))
                {
                    session.TrySyncAutoDescription(dto, customLocked: false);
                }
            }

            if (request["extraGlossaryTerms"] is JArray termArray)
            {
                dto.extraGlossaryTerms = termArray
                    .OfType<JValue>()
                    .Select(v => (v.Value as string ?? string.Empty).Trim())
                    .Where(s => s.Length > 0)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
            }

            BumpRevision();
            payload = new
            {
                description = dto.description ?? string.Empty,
                descriptionLocked = session.InferDescriptionCustomLocked(dto),
            };
            return true;
        }

        private bool DispatchUpdateDeck(JObject request, out object payload, out string error)
        {
            payload = null;
            error = null;
            var contentId = request.Value<string>("contentId") ?? string.Empty;
            var entry = FindDeck(contentId);
            if (entry?.Dto == null)
            {
                error = "未找到卡组：" + contentId;
                return false;
            }

            var dto = entry.Dto;
            dto.sprites ??= new CardPresentationSpritesDto();
            ApplyString(request, "displayName", v => dto.displayName = v);
            ApplyString(request, "backBorder", v => dto.sprites.backBorder = v);
            ApplyString(request, "backShirt", v => dto.sprites.backShirt = v);
            ApplyString(request, "backLogo", v => dto.sprites.backLogo = v);
            BumpRevision();
            payload = new { updated = true };
            return true;
        }

        private bool DispatchCreateDeck(JObject request, out object payload, out string error)
        {
            payload = null;
            var contentId = request.Value<string>("contentId") ?? string.Empty;
            var displayName = request.Value<string>("displayName") ?? string.Empty;
            if (!session.TryCreateDeck(contentId, displayName, out error))
            {
                return false;
            }

            statusMessage = "已新建卡组。";
            BumpRevision();
            payload = new { created = true };
            return true;
        }

        private bool DispatchUpdateTemplate(JObject request, out object payload, out string error)
        {
            payload = null;
            error = null;
            var id = request.Value<string>("id") ?? string.Empty;
            var row = session.EffectTemplates.FirstOrDefault(t =>
                t != null && string.Equals(t.id, id, StringComparison.Ordinal));
            if (row == null)
            {
                error = "未找到效果模板：" + id;
                return false;
            }

            ApplyString(request, "state", v => row.state = v);
            ApplyString(request, "designText", v => row.design_text = v);
            BumpRevision();
            payload = new { updated = true };
            return true;
        }

        private bool DispatchUpdateVisualEffect(JObject request, out object payload, out string error)
        {
            payload = null;
            error = null;
            var id = request.Value<string>("id") ?? string.Empty;
            var row = session.VisualEffects.FirstOrDefault(v =>
                v != null && string.Equals(v.Id, id, StringComparison.Ordinal));
            if (row?.Dto == null)
            {
                error = "未找到特效：" + id;
                return false;
            }

            ApplyString(request, "displayName", v => row.Dto.displayName = v);
            ApplyFloat(request, "defaultFps", v => row.Dto.defaultFps = Mathf.Max(0.01f, v));
            ApplyFloat(request, "defaultScale", v => row.Dto.defaultScale = Mathf.Max(0.01f, v));
            BumpRevision();
            payload = new { updated = true };
            return true;
        }

        private bool DispatchGlossaryAdd(out object payload, out string error)
        {
            error = null;
            EnsureGlossaryLoaded();
            var created = iconCatalog.AddBlankEntry();
            created.displayNameZh = "新词条";
            iconCatalog.InvalidateLookup();
            iconCatalogDirty = true;
            RefreshGlossaryOverrides();
            BumpRevision();
            payload = new { added = true };
            return true;
        }

        private bool DispatchGlossaryUpdate(JObject request, out object payload, out string error)
        {
            payload = null;
            error = null;
            EnsureGlossaryLoaded();
            var index = request.Value<int?>("index") ?? -1;
            var entries = iconCatalog.Entries;
            if (index < 0 || index >= entries.Count || entries[index] == null)
            {
                error = "词条索引无效：" + index;
                return false;
            }

            var entry = entries[index];
            if (request.TryGetValue("code", out var codeToken))
            {
                var next = (codeToken.Value<string>() ?? string.Empty).Trim();
                if (CardFaceDescriptionIconCatalogSO.IsReservedAssemblySlotCode(next))
                {
                    error = "不能占用装配槽代号：" + next;
                    return false;
                }

                entry.code = next;
            }

            ApplyString(request, "displayNameZh", v => entry.displayNameZh = v);
            ApplyString(request, "explanation", v => entry.explanation = v);
            if (request.TryGetValue("colorHex", out var colorToken))
            {
                var hex = (colorToken.Value<string>() ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(hex))
                {
                    entry.color = new Color(1f, 1f, 1f, 0f);
                }
                else if (ColorUtility.TryParseHtmlString(hex, out var parsed))
                {
                    parsed.a = 1f;
                    entry.color = parsed;
                }
            }

            if (request.TryGetValue("spritePath", out var spriteToken))
            {
                var path = (spriteToken.Value<string>() ?? string.Empty).Trim();
                entry.sprite = string.IsNullOrEmpty(path)
                    ? null
                    : CardPresentationSpritePath.LoadSprite(path);
            }

            var layoutCode = (entry.code ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(layoutCode)
                && (request.ContainsKey("bearingX")
                    || request.ContainsKey("bearingY")
                    || request.ContainsKey("baseScale")))
            {
                inlineIconStyle.Resolve(layoutCode, out var bx, out var by, out var scale);
                bx = request.Value<float?>("bearingX") ?? bx;
                by = request.Value<float?>("bearingY") ?? by;
                scale = request.Value<float?>("baseScale") ?? scale;
                inlineIconStyle.SetEntry(layoutCode, bx, by, scale);
                inlineIconStyleDirty = true;
                CardFacePresentationBinder.InvalidateInlineIconStyleCache();
            }

            iconCatalog.InvalidateLookup();
            iconCatalogDirty = true;
            RefreshGlossaryOverrides();
            BumpRevision();
            payload = new { updated = true };
            return true;
        }

        private bool DispatchGlossaryLayout(JObject request, out object payload, out string error)
        {
            payload = null;
            error = null;
            EnsureGlossaryLoaded();
            var code = (request.Value<string>("code") ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(code))
            {
                error = "code 为空。";
                return false;
            }

            inlineIconStyle.Resolve(code, out var bx, out var by, out var scale);
            bx = request.Value<float?>("bearingX") ?? bx;
            by = request.Value<float?>("bearingY") ?? by;
            scale = request.Value<float?>("baseScale") ?? scale;
            inlineIconStyle.SetEntry(code, bx, by, scale);
            inlineIconStyleDirty = true;
            CardFacePresentationBinder.SetInlineIconStyleOverride(inlineIconStyle);
            CardFacePresentationBinder.InvalidateInlineIconStyleCache();
            BumpRevision();
            payload = new { updated = true };
            return true;
        }

        private bool DispatchGlossaryRemove(JObject request, out object payload, out string error)
        {
            payload = null;
            error = null;
            EnsureGlossaryLoaded();
            var index = request.Value<int?>("index") ?? -1;
            var entries = iconCatalog.Entries;
            if (index < 0 || index >= entries.Count)
            {
                error = "词条索引无效：" + index;
                return false;
            }

            iconCatalog.TryRemoveEntry(entries[index]);
            iconCatalogDirty = true;
            RefreshGlossaryOverrides();
            BumpRevision();
            payload = new { removed = true };
            return true;
        }

        private bool DispatchListSprites(JObject request, out object payload, out string error)
        {
            error = null;
            var query = (request.Value<string>("query") ?? string.Empty).Trim();
            var root = (request.Value<string>("root") ?? string.Empty).Trim().Replace('\\', '/');
            if (string.IsNullOrEmpty(root))
            {
                root = CardPresentationContentArt.RootAssetFolder;
            }

            if (!root.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                payload = null;
                error = "根目录须在 Assets 下。";
                return false;
            }

            var filter = string.IsNullOrEmpty(query) ? "t:Sprite" : "t:Sprite " + query;
            var guids = AssetDatabase.FindAssets(filter, new[] { root });
            var paths = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < guids.Length && paths.Count < 160; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!string.IsNullOrEmpty(path) && seen.Add(path))
                {
                    paths.Add(path);
                }
            }

            var sprites = new List<object>();
            for (var p = 0; p < paths.Count && sprites.Count < 400; p++)
            {
                var assets = AssetDatabase.LoadAllAssetsAtPath(paths[p]);
                if (assets == null)
                {
                    continue;
                }

                for (var a = 0; a < assets.Length && sprites.Count < 400; a++)
                {
                    if (assets[a] is Sprite sprite)
                    {
                        sprites.Add(new
                        {
                            @ref = CardPresentationMigration.AssetPathOrEmpty(sprite),
                            name = sprite.name,
                            path = paths[p],
                        });
                    }
                }
            }

            payload = new { sprites, truncated = sprites.Count >= 400 || paths.Count >= 160 };
            return true;
        }

        private bool DispatchAnimFrames(JObject request, out object payload, out string error)
        {
            payload = null;
            error = null;
            var contentId = request.Value<string>("contentId") ?? string.Empty;
            var slotId = request.Value<string>("slotId") ?? string.Empty;
            var entry = FindFace(contentId);
            var slots = entry?.Dto?.animations?.slots;
            CardPresentationAnimSlotDto slot = null;
            if (slots != null)
            {
                slot = slots.FirstOrDefault(s =>
                    s != null && string.Equals(s.id, slotId, StringComparison.OrdinalIgnoreCase));
            }

            if (slot == null)
            {
                error = "未找到动画槽：" + contentId + "/" + slotId;
                return false;
            }

            payload = new
            {
                frames = LoadFrameRefs(slot.sourceType, slot.path),
                fps = entry.Dto.animations?.defaultFps ?? 8f,
                offsetX = slot.offsetX,
                offsetY = slot.offsetY,
            };
            return true;
        }

        private bool DispatchVfxFrames(JObject request, out object payload, out string error)
        {
            payload = null;
            error = null;
            var id = request.Value<string>("id") ?? string.Empty;
            var row = session.VisualEffects.FirstOrDefault(v =>
                v != null && string.Equals(v.Id, id, StringComparison.Ordinal));
            if (row?.Dto == null)
            {
                error = "未找到特效：" + id;
                return false;
            }

            payload = new
            {
                frames = LoadFrameRefs("atlas", row.Dto.sheetPath),
                fps = row.Dto.defaultFps,
                scale = row.Dto.defaultScale,
            };
            return true;
        }

        private static object[] LoadFrameRefs(string sourceType, string path)
        {
            var frames = NineGrid.Cards.Anim.CardAnimFrameSource.LoadFrames(sourceType, path);
            if (frames == null || frames.Length == 0)
            {
                return Array.Empty<object>();
            }

            var result = new List<object>(frames.Length);
            for (var i = 0; i < frames.Length; i++)
            {
                var sprite = frames[i];
                if (sprite == null)
                {
                    continue;
                }

                result.Add(new
                {
                    @ref = CardPresentationMigration.AssetPathOrEmpty(sprite),
                    name = sprite.name,
                    w = sprite.rect.width,
                    h = sprite.rect.height,
                });
            }

            return result.ToArray();
        }

        internal CardPresentationEditorEntry FindFace(string contentId)
        {
            if (string.IsNullOrWhiteSpace(contentId))
            {
                return null;
            }

            return session.FaceEntries.FirstOrDefault(e =>
                e != null && string.Equals(e.ContentId, contentId, StringComparison.Ordinal));
        }

        internal CardPresentationEditorEntry FindDeck(string contentId)
        {
            if (string.IsNullOrWhiteSpace(contentId))
            {
                return null;
            }

            return session.DeckEntries.FirstOrDefault(e =>
                e != null && string.Equals(e.ContentId, contentId, StringComparison.Ordinal));
        }

        internal static string InferDefaultContainerType(string kind)
        {
            if (CardPresentationEditorSession.IsMonsterKind(kind))
            {
                return "MonsterSkill";
            }

            if (string.Equals(kind, "Trap", StringComparison.OrdinalIgnoreCase))
            {
                return "Trap";
            }

            if (string.Equals(kind, "Relic", StringComparison.OrdinalIgnoreCase))
            {
                return "Relic";
            }

            if (CardPresentationEditorSession.IsItemLikeKind(kind))
            {
                return "HelpCard";
            }

            return string.Empty;
        }

        private void EnsureGlossaryLoaded()
        {
            if (inlineIconStyle == null)
            {
                inlineIconStyle = AssetDatabase.LoadAssetAtPath<CardFaceDescriptionInlineIconStyleSO>(
                    CardChassisPaths.DescriptionInlineIconStyleAsset);
                if (inlineIconStyle == null)
                {
                    inlineIconStyle = ScriptableObject.CreateInstance<CardFaceDescriptionInlineIconStyleSO>();
                    inlineIconStyle.name = "CardFaceDescriptionInlineIconStyle";
                    AssetDatabase.CreateAsset(inlineIconStyle, CardChassisPaths.DescriptionInlineIconStyleAsset);
                    AssetDatabase.SaveAssets();
                }
            }

            if (iconCatalog == null)
            {
                iconCatalog = AssetDatabase.LoadAssetAtPath<CardFaceDescriptionIconCatalogSO>(
                    CardChassisPaths.DescriptionIconCatalogAsset);
                if (iconCatalog == null)
                {
                    iconCatalog = ScriptableObject.CreateInstance<CardFaceDescriptionIconCatalogSO>();
                    iconCatalog.name = "CardFaceDescriptionIconCatalog";
                    AssetDatabase.CreateAsset(iconCatalog, CardChassisPaths.DescriptionIconCatalogAsset);
                    AssetDatabase.SaveAssets();
                }
            }
        }

        private void RefreshGlossaryOverrides()
        {
            CardFacePresentationBinder.SetInlineIconStyleOverride(inlineIconStyle);
            CardFacePresentationBinder.SetDescriptionIconCatalogOverride(iconCatalog);
            CardFacePresentationBinder.InvalidateDescriptionIconCatalogCache();
        }

        private void PersistGlossaryIfDirty()
        {
            EnsureGlossaryLoaded();
            if (iconCatalogDirty)
            {
                var keep = new List<CardFaceDescriptionIconCatalogSO.Entry>();
                var seenCodes = new HashSet<string>(StringComparer.Ordinal);
                for (var i = 0; i < iconCatalog.Entries.Count; i++)
                {
                    var entry = iconCatalog.Entries[i];
                    if (entry == null)
                    {
                        continue;
                    }

                    var code = (entry.code ?? string.Empty).Trim();
                    var name = (entry.displayNameZh ?? string.Empty).Trim();
                    if (string.IsNullOrEmpty(code) && string.IsNullOrEmpty(name))
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(code)
                        && (CardFaceDescriptionIconCatalogSO.IsReservedAssemblySlotCode(code)
                            || !seenCodes.Add(code)))
                    {
                        continue;
                    }

                    entry.code = code;
                    keep.Add(entry);
                }

                iconCatalog.ReplaceEntries(keep);
                EditorUtility.SetDirty(iconCatalog);
                iconCatalogDirty = false;
                CardFacePresentationBinder.InvalidateDescriptionIconCatalogCache();
            }

            if (inlineIconStyleDirty)
            {
                EditorUtility.SetDirty(inlineIconStyle);
                inlineIconStyleDirty = false;
                CardFacePresentationBinder.InvalidateInlineIconStyleCache();
            }

            AssetDatabase.SaveAssets();
        }

        private void BumpRevision()
        {
            localRevision++;
        }

        private static void ApplyString(JObject source, string key, Action<string> apply)
        {
            if (source.TryGetValue(key, out var token))
            {
                apply(token.Type == JTokenType.Null ? string.Empty : token.Value<string>() ?? string.Empty);
            }
        }

        private static void ApplyInt(JObject source, string key, Action<int> apply)
        {
            if (source.TryGetValue(key, out var token) && token.Type != JTokenType.Null)
            {
                apply(token.Value<int>());
            }
        }

        private static void ApplyFloat(JObject source, string key, Action<float> apply)
        {
            if (source.TryGetValue(key, out var token) && token.Type != JTokenType.Null)
            {
                apply(token.Value<float>());
            }
        }

        private static void ApplyBool(JObject source, string key, Action<bool> apply)
        {
            if (source.TryGetValue(key, out var token) && token.Type != JTokenType.Null)
            {
                apply(token.Value<bool>());
            }
        }

        [Serializable]
        private sealed class RoomOpeningInjectListDto
        {
            public RoomOpeningInjectDto[] items;
        }

        private static string SerializeOpeningInjects(RoomOpeningInjectDto[] injects)
        {
            if (injects == null || injects.Length == 0)
            {
                return "[]";
            }

            return JsonUtility.ToJson(new RoomOpeningInjectListDto { items = injects }, true);
        }

        private static RoomOpeningInjectDto[] ParseOpeningInjects(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw) || raw.Trim() == "[]")
            {
                return Array.Empty<RoomOpeningInjectDto>();
            }

            var trimmed = raw.Trim();
            if (trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                trimmed = "{\"items\":" + trimmed + "}";
            }

            try
            {
                var wrapper = JsonUtility.FromJson<RoomOpeningInjectListDto>(trimmed);
                return wrapper?.items ?? Array.Empty<RoomOpeningInjectDto>();
            }
            catch
            {
                return Array.Empty<RoomOpeningInjectDto>();
            }
        }
    }
}
#endif
