#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NineGrid.Content.Audio;
using NineGrid.Content.Editor.Ui;
using NineGrid.Core;
using NineGrid.Presentation.Systems;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NineGrid.Content.Editor
{
    public static class AudioBindingEditorPreview
    {
        private static MethodInfo playPreviewClip;
        private static MethodInfo stopAllPreviewClips;
        private static bool initialized;

        public static bool Play(AudioClip clip, float startOffsetSeconds)
        {
            EnsureMethods();
            if (clip == null || playPreviewClip == null)
            {
                return false;
            }

            var startSample = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Max(0f, startOffsetSeconds) * clip.frequency),
                0,
                Mathf.Max(0, clip.samples - 1));
            try
            {
                playPreviewClip.Invoke(null, new object[] { clip, startSample, false });
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[AudioBindingEditor] 试听失败：" + exception.Message);
                return false;
            }
        }

        public static void Stop()
        {
            EnsureMethods();
            try
            {
                if (stopAllPreviewClips != null)
                {
                    stopAllPreviewClips.Invoke(null, null);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[AudioBindingEditor] 停止试听失败：" + exception.Message);
            }
        }

        private static void EnsureMethods()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            var audioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            if (audioUtil == null)
            {
                return;
            }

            playPreviewClip = audioUtil.GetMethod(
                "PlayPreviewClip",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(AudioClip), typeof(int), typeof(bool) },
                null);
            stopAllPreviewClips = audioUtil.GetMethod(
                "StopAllPreviewClips",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);
        }
    }

    public sealed class AudioBindingEditorWindow : EditorWindow
    {
        private const string MenuPath = "NineGrid/音频/声音绑定调音工作台";

        private readonly AudioBindingEditorSession session = new AudioBindingEditorSession();
        private readonly MusicBindingEditorSession musicSession = new MusicBindingEditorSession();
        private readonly List<MusicHistoryRecord> musicHistory = new List<MusicHistoryRecord>();
        private MusicAuditResult musicAudit;
        private IReadOnlyList<MusicOverlapAnomaly> musicAnomalies = Array.Empty<MusicOverlapAnomaly>();
        private bool previewWithBindingDelay;
        private VisualElement listContainer;
        private VisualElement contentRoot;
        private TextField searchField;
        private HelpBox statusHelpBox;
        private double nextLiveRefresh;
        private string status = string.Empty;

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            var window = GetWindow<AudioBindingEditorWindow>();
            window.titleContent = new GUIContent("声音绑定调音");
            window.minSize = new Vector2(1160f, 720f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            try
            {
                session.ReloadFromDisk();
                musicSession.ReloadFromDisk();
            }
            catch (Exception exception)
            {
                status = "加载音频绑定失败：" + exception.Message;
                Debug.LogError("[AudioBindingEditor] " + status);
            }

            BuildShell();
            RefreshAll();
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            StopPreview();
            if ((session.DirtyCount > 0 || musicSession.DirtyCount > 0)
                && !EditorApplication.isCompiling)
            {
                EditorUtility.DisplayDialog(
                    "声音绑定未保存",
                    "关闭工作台不会自动保存。SFX 脏条目：" + session.DirtyCount
                    + "；BGM 脏条目：" + musicSession.DirtyCount + "。",
                    "知道了");
            }
        }

        private void OnBeforeAssemblyReload()
        {
            if (session.DirtyCount <= 0 && musicSession.DirtyCount <= 0)
            {
                return;
            }

            EditorUtility.DisplayDialog(
                "重载前存在未保存声音绑定",
                "Unity 即将重载程序集；未保存工作副本不会自动写回正式 JSON。SFX 脏条目："
                + session.DirtyCount + "；BGM 脏条目：" + musicSession.DirtyCount,
                "知道了");
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode
                && (session.DirtyCount > 0 || musicSession.DirtyCount > 0))
            {
                EditorUtility.DisplayDialog(
                    "退出 Play Mode 前存在未保存声音绑定",
                    "退出 Play Mode 不会自动保存。SFX 脏条目：" + session.DirtyCount
                    + "；BGM 脏条目：" + musicSession.DirtyCount,
                    "知道了");
            }

            RefreshLiveHistory();
            Repaint();
        }

        private void Update()
        {
            if (EditorApplication.timeSinceStartup < nextLiveRefresh)
            {
                return;
            }

            nextLiveRefresh = EditorApplication.timeSinceStartup + 0.25d;
            RefreshLiveHistory();
            RefreshList();
            RefreshContent();
            UpdateToolbarEnabledState();
        }

        private void BuildShell()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.flexGrow = 1;
            rootVisualElement.style.backgroundColor = ContentVisualWarmConsoleUi.Theme.RootBg;
            rootVisualElement.Add(ContentVisualWarmConsoleUi.BuildHeader(
                "声音绑定调音工作台",
                "非 Play Mode 编辑正式 audio_bindings.json；Play Mode 查看最近请求并定位最终解析绑定。工作副本不会因关闭或重载自动保存。"));
            rootVisualElement.Add(ContentVisualWarmConsoleUi.BuildToolbar(
                ("保存全部脏改动", SaveAll, "把全部 SFX/BGM 脏工作副本写入正式 JSON，并标记为人工确认"),
                ("回撤全部脏改动", RevertAll, "恢复最近一次成功保存的 SFX/BGM 磁盘快照"),
                ("从磁盘重载", ReloadFromDisk, "丢弃未保存工作副本并重新读取正式 JSON"),
                ("AI 初始绑定", RunAiBindPreserve, "高权限填充未确认条目；默认保留人工确认"),
                ("停止素材试听", StopPreview, "停止当前编辑器素材试听"),
                ("停止未知 BGM", StopUnknownMusic, "显式停止诊断发现的未认领 Music 来源")));
            UpdateToolbarEnabledState();

            var split = new TwoPaneSplitView(0, 320f, TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1;
            rootVisualElement.Add(split);
            split.Add(BuildSidebar());
            split.Add(BuildContentPane());
        }

        private void UpdateToolbarEnabledState()
        {
            var editMode = !Application.isPlaying;
            for (var i = 0; i < rootVisualElement.childCount; i++)
            {
                if (rootVisualElement[i] is UnityEngine.UIElements.VisualElement element
                    && element.ClassListContains("unity-toolbar"))
                {
                    element.SetEnabled(editMode);
                }
            }
        }

        private VisualElement BuildSidebar()
        {
            var sidebar = new VisualElement();
            sidebar.style.flexGrow = 1;
            sidebar.style.backgroundColor = ContentVisualWarmConsoleUi.Theme.SidebarBg;

            var searchWrap = new VisualElement();
            searchWrap.style.paddingLeft = 10;
            searchWrap.style.paddingRight = 10;
            searchWrap.style.paddingTop = 10;
            searchWrap.style.paddingBottom = 8;
            searchField = new TextField { value = session.SearchText };
            searchField.RegisterValueChangedCallback(evt =>
            {
                session.SearchText = evt.newValue;
                RefreshList();
                RefreshContent();
            });
            searchWrap.Add(ContentVisualWarmConsoleUi.WrapControl(
                "搜索",
                "匹配音效说明、cue ID、content ID、素材名、模块和绑定状态",
                searchField));
            sidebar.Add(searchWrap);

            var filterWrap = new VisualElement();
            filterWrap.style.paddingLeft = 10;
            filterWrap.style.paddingRight = 10;
            filterWrap.style.paddingBottom = 6;
            filterWrap.Add(CreateFilterToggle("仅未保存", session.FilterDirtyOnly, value =>
            {
                session.FilterDirtyOnly = value;
                RefreshList();
                RefreshContent();
            }));
            filterWrap.Add(CreateFilterToggle("仅未绑定", session.FilterUnboundOnly, value =>
            {
                session.FilterUnboundOnly = value;
                RefreshList();
                RefreshContent();
            }));
            filterWrap.Add(CreateFilterToggle("仅断链", session.FilterBrokenOnly, value =>
            {
                session.FilterBrokenOnly = value;
                RefreshList();
                RefreshContent();
            }));
            filterWrap.Add(CreateFilterToggle("仅失败", session.FilterFailedOnly, value =>
            {
                session.FilterFailedOnly = value;
                RefreshList();
                RefreshContent();
            }));
            sidebar.Add(filterWrap);

            var listTitle = ContentVisualWarmConsoleUi.CreateTitleLabel(
                "声音提示 / 绑定",
                10,
                true,
                ContentVisualWarmConsoleUi.Theme.TextTertiary);
            listTitle.style.paddingLeft = 10;
            listTitle.style.paddingTop = 8;
            listTitle.style.paddingBottom = 4;
            sidebar.Add(listTitle);

            var listScroll = new ScrollView();
            listScroll.style.flexGrow = 1;
            listScroll.style.paddingLeft = 10;
            listScroll.style.paddingRight = 10;
            listScroll.style.paddingBottom = 10;
            listContainer = new VisualElement();
            listScroll.Add(listContainer);
            sidebar.Add(listScroll);
            return sidebar;
        }

        private VisualElement BuildContentPane()
        {
            var scroll = ContentVisualWarmConsoleUi.CreateContentScroll(out contentRoot);
            statusHelpBox = ContentVisualWarmConsoleUi.CreateStatusHelpBox(string.Empty);
            contentRoot.Add(statusHelpBox);
            return scroll;
        }

        private static Toggle CreateFilterToggle(string text, bool initial, Action<bool> changed)
        {
            var toggle = new Toggle(text) { value = initial };
            toggle.style.marginBottom = 3;
            toggle.RegisterValueChangedCallback(evt => changed(evt.newValue));
            return toggle;
        }

        private void RefreshAll()
        {
            RefreshLiveHistory();
            RefreshList();
            RefreshContent();
        }

        private void RefreshLiveHistory()
        {
            musicHistory.Clear();
            musicAudit = null;
            musicAnomalies = Array.Empty<MusicOverlapAnomaly>();
            if (!Application.isPlaying)
            {
                return;
            }

            try
            {
                var architecture = NineGridArchitecture.Interface;
                var audio = architecture == null ? null : architecture.GetSystem<IAudioSystem>();
                if (audio != null)
                {
                    session.SetPlaybackHistory(audio.History);
                }

                var music = architecture == null ? null : architecture.GetSystem<IMusicSystem>();
                if (music != null)
                {
                    musicAudit = music.LastAudit;
                    musicAnomalies = music.OverlapAnomalies;
                    musicHistory.AddRange(music.History);
                }
            }
            catch (Exception exception)
            {
                status = "读取 Play Mode 音频历史失败：" + exception.Message;
            }
        }

        private void RefreshList()
        {
            if (listContainer == null)
            {
                return;
            }

            listContainer.Clear();
            var filtered = session.GetFilteredEntries().ToList();
            for (var i = 0; i < filtered.Count; i++)
            {
                listContainer.Add(CreateListRow(filtered[i]));
            }

            if (filtered.Count == 0)
            {
                listContainer.Add(ContentVisualWarmConsoleUi.CreateDescriptionLabel("无匹配条目。"));
            }
        }

        private VisualElement CreateListRow(AudioBindingEditorEntry entry)
        {
            var selected = ReferenceEquals(session.FocusedEntry, entry);
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.backgroundColor = selected
                ? ContentVisualWarmConsoleUi.Theme.NavSelectedBg
                : ContentVisualWarmConsoleUi.Theme.NavNormalBg;
            row.style.borderTopLeftRadius = 6;
            row.style.borderTopRightRadius = 6;
            row.style.borderBottomLeftRadius = 6;
            row.style.borderBottomRightRadius = 6;
            row.style.marginBottom = 5;
            row.style.overflow = Overflow.Hidden;

            var stripe = new VisualElement();
            stripe.style.width = 4;
            stripe.style.alignSelf = Align.Stretch;
            stripe.style.backgroundColor = selected
                ? ContentVisualWarmConsoleUi.Theme.AccentStrong
                : ContentVisualWarmConsoleUi.Theme.NavStripeNormal;
            row.Add(stripe);

            var body = new VisualElement();
            body.style.flexGrow = 1;
            body.style.paddingLeft = 8;
            body.style.paddingTop = 7;
            body.style.paddingBottom = 7;
            body.style.paddingRight = 6;
            body.Add(ContentVisualWarmConsoleUi.CreateTitleLabel(
                string.IsNullOrWhiteSpace(entry.Note) ? "（无音效说明）" : entry.Note,
                12,
                true,
                ContentVisualWarmConsoleUi.Theme.TextPrimary));

            var state = entry.IsUnbound
                ? "未绑定"
                : session.IsBroken(entry)
                    ? "断链"
                    : entry.IsDirty
                        ? "未保存"
                        : entry.IsDisabled ? "已禁用" : "已绑定";
            var clipName = entry.Dto == null
                ? string.Empty
                : Path.GetFileName(entry.Dto.clipKey ?? string.Empty);
            var subtitle = ContentVisualWarmConsoleUi.CreateTinyPathLabel(
                entry.CueId + " · " + (string.IsNullOrEmpty(clipName) ? state : clipName + " · " + state));
            subtitle.style.marginTop = 2;
            body.Add(subtitle);
            row.Add(body);
            row.RegisterCallback<ClickEvent>(_ =>
            {
                session.Focus(entry);
                RefreshList();
                RefreshContent();
            });
            return row;
        }

        private void RefreshContent()
        {
            if (contentRoot == null)
            {
                return;
            }

            contentRoot.Clear();
            contentRoot.Add(statusHelpBox);
            UpdateStatusBox();
            if (Application.isPlaying)
            {
                BuildPlayerSettingsSection();
                BuildMusicDiagnosticsSection();
            }

            if (!Application.isPlaying)
            {
                BuildMusicAuthoringSection();
            }
            contentRoot.Add(ContentVisualWarmConsoleUi.CreateStatsGrid(
                ("筛选结果", session.GetFilteredEntries().Count().ToString(), "当前列表可见条目"),
                ("未保存", (session.DirtyCount + musicSession.DirtyCount).ToString(), "SFX/BGM 保存后写入正式 JSON"),
                ("未绑定", session.UnboundCount.ToString(), "cue 已声明但尚无绑定"),
                ("断链", session.BrokenCount.ToString(), "素材键未在正式 manifest 中"),
                ("失败", session.FailedCount.ToString(), "Play Mode 最近后端失败")));

            if (Application.isPlaying)
            {
                BuildHistorySection();
            }

            var entry = session.FocusedEntry;
            if (entry == null)
            {
                contentRoot.Add(ContentVisualWarmConsoleUi.CreatePageHeader(
                    "选择声音提示",
                    "左侧列表的主标题使用音效说明；绑定 cue ID、内容选择器和素材等技术字段收在详情中。"));
                return;
            }

            BuildEntrySection(entry);
        }

        private void BuildPlayerSettingsSection()
        {
            var architecture = NineGridArchitecture.Interface;
            var playerSettings = architecture == null
                ? null
                : architecture.GetSystem<IPlayerAudioSettingsSystem>();
            if (playerSettings == null)
            {
                return;
            }

            var author = playerSettings.AuthorDefaults;
            var current = playerSettings.Current;
            contentRoot.Add(ContentVisualWarmConsoleUi.CreateSectionCard(
                "玩家音频偏好（只读）",
                "随包作者默认与 PlayerPrefs 本地值并列显示；本工作台只编辑作者 SFX/BGM 绑定，绝不写入玩家偏好。",
                column =>
                {
                    AddPlayerAudioBusRow(column, "Master", author, current, PlayerAudioBus.Master);
                    AddPlayerAudioBusRow(column, "BGM", author, current, PlayerAudioBus.Bgm);
                    AddPlayerAudioBusRow(column, "SFX", author, current, PlayerAudioBus.Sfx);
                }));
        }

        private static void AddPlayerAudioBusRow(
            VisualElement column,
            string label,
            PlayerAudioSettingsSnapshot author,
            PlayerAudioSettingsSnapshot current,
            PlayerAudioBus bus)
        {
            var authorText = ToPercent(author.Volume(bus)) + (author.IsMuted(bus) ? " · 静音" : string.Empty);
            var playerText = ToPercent(current.Volume(bus)) + (current.IsMuted(bus) ? " · 静音" : string.Empty);
            column.Add(ContentVisualWarmConsoleUi.CreateStatsGrid(
                (label + " 作者默认", authorText, "随包 MMSoundManagerSettings 默认值"),
                (label + " 玩家本地", playerText, "PlayerPrefs；请在运行时玩家设置面板修改")));
        }

        private static string ToPercent(float value)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
        }

        private void BuildEntrySection(AudioBindingEditorEntry entry)
        {
            contentRoot.Add(ContentVisualWarmConsoleUi.CreatePageHeader(
                string.IsNullOrWhiteSpace(entry.Note) ? "（无音效说明）" : entry.Note,
                entry.CueId + " · " + (string.IsNullOrEmpty(entry.Module) ? "未声明模块" : entry.Module)
                + (string.IsNullOrEmpty(entry.AuthoritativeEmitter) ? string.Empty : " · " + entry.AuthoritativeEmitter)
                + AuthoringStatusSuffix(entry)));

            if (Application.isPlaying)
            {
                contentRoot.Add(BuildRuntimeBindingSection(entry));
                return;
            }

            var actions = new List<Button>();
            if (entry.IsUnbound)
            {
                actions.Add(new Button(() =>
                {
                    entry.CreateDraftBinding();
                    RefreshList();
                    RefreshContent();
                }) { text = "创建工作绑定" });
            }
            else
            {
                actions.Add(new Button(() => SaveEntry(entry)) { text = "保存此条（确认）" });
                actions.Add(new Button(() =>
                {
                    session.Revert(entry);
                    RefreshList();
                    RefreshContent();
                }) { text = "回撤此条" });
                actions.Add(new Button(() => PreviewEntry(entry))
                {
                    text = previewWithBindingDelay ? "按真实延迟试听" : "试听当前素材",
                });
            }

            contentRoot.Add(ContentVisualWarmConsoleUi.CreateButtonRow(actions.ToArray()));
            if (!entry.IsUnbound)
            {
                var previewDelay = new Toggle("试听时应用真实绑定延迟") { value = previewWithBindingDelay };
                previewDelay.RegisterValueChangedCallback(evt =>
                {
                    previewWithBindingDelay = evt.newValue;
                    RefreshContent();
                });
                contentRoot.Add(previewDelay);
            }

            if (entry.IsUnbound)
            {
                contentRoot.Add(ContentVisualWarmConsoleUi.CreateSectionCard(
                    "声明信息",
                    "该 cue 已被机械扫描发现，但正式 audio_bindings.json 尚无对应绑定。创建工作绑定后，保存才会改变正式 JSON。",
                    column =>
                    {
                        column.Add(ContentVisualWarmConsoleUi.WrapControlRow("cue ID", new Label(entry.CueId)));
                        column.Add(ContentVisualWarmConsoleUi.WrapControlRow("音效说明", new Label(entry.Note)));
                        column.Add(ContentVisualWarmConsoleUi.WrapControlRow("模块", new Label(entry.Module)));
                        column.Add(ContentVisualWarmConsoleUi.WrapControlRow("权威发射者", new Label(entry.AuthoritativeEmitter)));
                    }));
                return;
            }

            contentRoot.Add(BuildAuthoringSection(entry));
            contentRoot.Add(BuildTechnicalSection(entry));
        }

        private VisualElement BuildRuntimeBindingSection(AudioBindingEditorEntry entry)
        {
            var dto = entry.Dto;
            return ContentVisualWarmConsoleUi.CreateSectionCard(
                "运行时绑定（只读）",
                "Play Mode 只用于观察最近声音请求和定位解析结果。作者编辑、试听、保存与回撤在退出 Play Mode 后进行，避免运行时继续使用旧 Catalog。",
                column =>
                {
                    column.Add(ContentVisualWarmConsoleUi.WrapControlRow("cue ID", new Label(entry.CueId), 140f));
                    column.Add(ContentVisualWarmConsoleUi.WrapControlRow("音效说明", new Label(entry.Note), 140f));
                    column.Add(ContentVisualWarmConsoleUi.WrapControlRow("素材", new Label(dto?.clipKey ?? "（未绑定）"), 140f));
                    column.Add(ContentVisualWarmConsoleUi.WrapControlRow("BindingKey", new Label(entry.BindingKey), 140f));
                    column.Add(ContentVisualWarmConsoleUi.WrapControlRow("内容 ID", new Label(dto?.selectorContentId ?? string.Empty), 140f));
                    column.Add(ContentVisualWarmConsoleUi.WrapControlRow("绑定状态", new Label(entry.IsUnbound ? "未绑定" : entry.IsDisabled ? "已禁用" : "已绑定"), 140f));
                });
        }


        private VisualElement BuildAuthoringSection(AudioBindingEditorEntry entry)
        {
            return ContentVisualWarmConsoleUi.CreateSectionCard(
                "作者调音",
                "这些字段属于绑定工作副本；修改不会触碰玩家 Master / BGM / SFX 本地音量设置。",
                column =>
                {
                    var dto = entry.Dto;
                    var clipField = new SearchableChoiceField();
                    var choices = new List<SearchableChoiceField.Choice>
                    {
                        new SearchableChoiceField.Choice
                        {
                            Label = "（无素材）",
                            Value = string.Empty,
                            SearchHaystack = "无素材",
                        },
                    };
                    for (var i = 0; i < session.ClipOptions.Count; i++)
                    {
                        var option = session.ClipOptions[i];
                        choices.Add(new SearchableChoiceField.Choice
                        {
                            Label = Path.GetFileName(option.AssetPath) + " · " + option.ResourcesKey,
                            Value = option.ResourcesKey,
                            SearchHaystack = option.ResourcesKey + " " + option.AssetPath,
                        });
                    }

                    clipField.SetChoices(choices, AudioAssetManifestLoader.NormalizeKey(dto.clipKey));
                    clipField.ValueChanged += value =>
                    {
                        dto.clipKey = value;
                        Changed(entry);
                    };
                    column.Add(ContentVisualWarmConsoleUi.WrapControlRow("素材", clipField, 110f));

                    var volume = new FloatField { value = dto.volumeDb };
                    volume.RegisterValueChangedCallback(evt =>
                    {
                        dto.volumeDb = evt.newValue;
                        Changed(entry);
                    });
                    column.Add(ContentVisualWarmConsoleUi.WrapControlRow("作者音量 dB", volume, 110f));

                    var startOffset = new FloatField { value = dto.startOffsetSeconds };
                    startOffset.RegisterValueChangedCallback(evt =>
                    {
                        dto.startOffsetSeconds = Mathf.Max(0f, evt.newValue);
                        Changed(entry);
                    });
                    column.Add(ContentVisualWarmConsoleUi.WrapControlRow("素材内部起播点", startOffset, 110f));

                    var delay = new FloatField { value = dto.bindingDelaySeconds };
                    delay.RegisterValueChangedCallback(evt =>
                    {
                        dto.bindingDelaySeconds = Mathf.Max(0f, evt.newValue);
                        Changed(entry);
                    });
                    column.Add(ContentVisualWarmConsoleUi.WrapControlRow("绑定延迟", delay, 110f));

                    var interval = new FloatField { value = dto.minimumIntervalSeconds };
                    interval.RegisterValueChangedCallback(evt =>
                    {
                        dto.minimumIntervalSeconds = Mathf.Max(0f, evt.newValue);
                        Changed(entry);
                    });
                    column.Add(ContentVisualWarmConsoleUi.WrapControlRow("最短播放间隔", interval, 110f));
                    var usePool = new Toggle("启用多素材随机池")
                    {
                        value = dto.variants != null && dto.variants.Length > 0,
                    };
                    usePool.RegisterValueChangedCallback(evt =>
                    {
                        dto.variants = evt.newValue
                            ? new[]
                            {
                                new AudioVariantDto
                                {
                                    variantId = "variant-a",
                                    clipKey = dto.clipKey,
                                    weight = 1f,
                                    volumeTrimDb = 0f,
                                    startOffsetSeconds = dto.startOffsetSeconds,
                                },
                            }
                            : Array.Empty<AudioVariantDto>();
                        Changed(entry);
                    });
                    column.Add(usePool);

                    if (dto.variants != null && dto.variants.Length > 0)
                    {
                        for (var i = 0; i < dto.variants.Length; i++)
                        {
                            BuildVariantEditor(column, entry, dto.variants[i], i);
                        }

                        column.Add(new Button(() =>
                        {
                            var variants = (dto.variants ?? Array.Empty<AudioVariantDto>()).ToList();
                            variants.Add(new AudioVariantDto
                            {
                                variantId = "variant-" + (variants.Count + 1),
                                weight = 1f,
                            });
                            dto.variants = variants.ToArray();
                            Changed(entry);
                        }) { text = "添加随机变体" });
                    }


                    var enabled = new Toggle("启用") { value = dto.enabled };
                    enabled.RegisterValueChangedCallback(evt =>
                    {
                        dto.enabled = evt.newValue;
                        Changed(entry);
                    });
                    column.Add(enabled);
                });
        }

        private void BuildVariantEditor(
            VisualElement column,
            AudioBindingEditorEntry entry,
            AudioVariantDto variant,
            int index)
        {
            if (variant == null)
            {
                return;
            }

            var box = ContentVisualWarmConsoleUi.CreateSectionCard(
                "随机变体 " + (index + 1),
                "权重仅在有效变体间计算；默认避免与上次立即重复。",
                body =>
                {
                    var id = new TextField { value = variant.variantId ?? string.Empty };
                    id.RegisterValueChangedCallback(evt =>
                    {
                        variant.variantId = evt.newValue ?? string.Empty;
                        Changed(entry);
                    });
                    body.Add(ContentVisualWarmConsoleUi.WrapControlRow("变体 ID", id, 110f));

                    var clip = new TextField { value = variant.clipKey ?? string.Empty };
                    clip.RegisterValueChangedCallback(evt =>
                    {
                        variant.clipKey = evt.newValue ?? string.Empty;
                        Changed(entry);
                    });
                    body.Add(ContentVisualWarmConsoleUi.WrapControlRow("素材键", clip, 110f));

                    var weight = new FloatField { value = variant.weight };
                    weight.RegisterValueChangedCallback(evt =>
                    {
                        variant.weight = Mathf.Max(0f, evt.newValue);
                        Changed(entry);
                    });
                    body.Add(ContentVisualWarmConsoleUi.WrapControlRow("权重", weight, 110f));

                    var trim = new FloatField { value = variant.volumeTrimDb };
                    trim.RegisterValueChangedCallback(evt =>
                    {
                        variant.volumeTrimDb = evt.newValue;
                        Changed(entry);
                    });
                    body.Add(ContentVisualWarmConsoleUi.WrapControlRow("音量 trim dB", trim, 110f));

                    var offset = new FloatField { value = variant.startOffsetSeconds };
                    offset.RegisterValueChangedCallback(evt =>
                    {
                        variant.startOffsetSeconds = Mathf.Max(0f, evt.newValue);
                        Changed(entry);
                    });
                    body.Add(ContentVisualWarmConsoleUi.WrapControlRow("素材内部起播点", offset, 110f));
                    var previewVariant = new Button(() => PreviewClipKey(variant.clipKey, variant.startOffsetSeconds))
                    {
                        text = "试听此变体",
                    };
                    body.Add(ContentVisualWarmConsoleUi.WrapControlRow("试听", previewVariant, 110f));

                    body.Add(new Button(() =>
                    {
                        var variants = (entry.Dto.variants ?? Array.Empty<AudioVariantDto>()).ToList();
                        if (index >= 0 && index < variants.Count)
                        {
                            variants.RemoveAt(index);
                            entry.Dto.variants = variants.ToArray();
                            Changed(entry);
                        }
                    }) { text = "删除此变体" });
                });
            column.Add(box);
        }

        private VisualElement BuildTechnicalSection(AudioBindingEditorEntry entry)
        {
            return ContentVisualWarmConsoleUi.CreateSectionCard(
                "技术字段与内容专属覆盖",
                "默认收起。选择器决定最终解析到哪一条绑定；运行时使用稳定内容标识，不使用卡牌运行时 UID。",
                column =>
                {
                    var dto = entry.Dto;
                    column.Add(ContentVisualWarmConsoleUi.WrapControlRow("cue ID", new Label(dto.cueId), 140f));
                    column.Add(ContentVisualWarmConsoleUi.WrapControlRow("模块", new Label(dto.module), 140f));
                    AddSelectorField(column, entry, "卡牌 content ID", () => dto.selectorCardDefId, value => dto.selectorCardDefId = value);
                    AddSelectorField(column, entry, "技能 ID", () => dto.selectorSkillId, value => dto.selectorSkillId = value);
                    AddSelectorField(column, entry, "房间 ID", () => dto.selectorRoomId, value => dto.selectorRoomId = value);
                    AddSelectorField(column, entry, "道具 ID", () => dto.selectorItemDefId, value => dto.selectorItemDefId = value);
                    AddSelectorField(column, entry, "内容 ID", () => dto.selectorContentId, value => dto.selectorContentId = value);
                },
                expanded: false);
        }

        private void AddSelectorField(
            VisualElement column,
            AudioBindingEditorEntry entry,
            string label,
            Func<string> read,
            Action<string> write)
        {
            var field = new TextField { value = read() ?? string.Empty };
            field.RegisterValueChangedCallback(evt =>
            {
                write(evt.newValue ?? string.Empty);
                Changed(entry);
            });
            column.Add(ContentVisualWarmConsoleUi.WrapControlRow(label, field, 140f));
        }

        private void BuildHistorySection()
        {
            contentRoot.Add(ContentVisualWarmConsoleUi.CreateSectionCard(
                "Play Mode 最近声音请求",
                "最新请求在前；Played / Cooldown / Unbound / BackendFailure 均保留。点击定位会跳到最终解析的绑定。",
                column =>
                {
                    var history = session.PlaybackHistory;
                    if (history.Count == 0)
                    {
                        column.Add(ContentVisualWarmConsoleUi.CreateDescriptionLabel("当前没有可见的声音请求历史。"));
                        return;
                    }

                    var limit = Mathf.Min(history.Count, 48);
                    for (var i = history.Count - 1; i >= history.Count - limit; i--)
                    {
                        var record = history[i];
                        var row = new VisualElement();
                        row.style.flexDirection = FlexDirection.Row;
                        row.style.alignItems = Align.Center;
                        row.style.marginBottom = 3;
                        var summary = ContentVisualWarmConsoleUi.CreateTinyPathLabel(
                            record.Outcome + " · " + record.CueId
                            + (string.IsNullOrEmpty(record.CueNote) ? string.Empty : " · " + record.CueNote)
                            + (string.IsNullOrEmpty(record.VariantId) ? string.Empty : " · 变体 " + record.VariantId)
                            + (string.IsNullOrEmpty(record.ActualClipKey) ? string.Empty : " · " + record.ActualClipKey));
                        summary.style.flexGrow = 1;
                        row.Add(summary);
                        var target = session.FindEntryForHistory(record);
                        var locate = new Button(() =>
                        {
                            if (target != null)
                            {
                                session.Focus(target);
                                RefreshList();
                                RefreshContent();
                            }
                        }) { text = target == null ? "—" : "定位" };
                        locate.SetEnabled(target != null);
                        row.Add(locate);
                        column.Add(row);
                    }
                }));
        }

        private void BuildMusicDiagnosticsSection()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var hasAnomaly = musicAudit != null && musicAudit.HasUnknownSources;
            var warning = hasAnomaly
                ? "幽灵 BGM 警报：Music 轨存在 " + musicAudit.UnknownSources.Count + " 条未认领来源。默认未自动停止。"
                : "Music 轨无未认领来源。";
            var box = ContentVisualWarmConsoleUi.CreateStatusHelpBox(
                warning,
                hasAnomaly ? HelpBoxMessageType.Error : HelpBoxMessageType.Info);
            if (hasAnomaly)
            {
                box.style.color = new Color(1f, 0.35f, 0.28f);
            }

            var section = ContentVisualWarmConsoleUi.CreateSectionCard(
                "幽灵 BGM 诊断",
                "审计实际 Music 轨与当前、淡出和 Editor Preview 认领集合；未知来源只报警，停止必须由人工操作。",
                column =>
                {
                    column.Add(box);
                    if (hasAnomaly)
                    {
                        column.Add(new Button(() =>
                        {
                            StopUnknownMusic();
                        }) { text = "停止未知 Music 来源" });
                    }

                    if (musicAudit != null)
                    {
                        column.Add(ContentVisualWarmConsoleUi.WrapControlRow(
                            "审计触发",
                            new Label(musicAudit.Trigger),
                            110f));
                        column.Add(ContentVisualWarmConsoleUi.WrapControlRow(
                            "实际 / 已认领",
                            new Label(musicAudit.ActualSources.Count + " / " + musicAudit.ClaimedSourceIds.Count),
                            110f));
                        column.Add(ContentVisualWarmConsoleUi.WrapControlRow(
                            "未知来源",
                            new Label(musicAudit.UnknownSources.Count.ToString()),
                            110f));
                    }

                    var limit = Mathf.Min(musicHistory.Count, 12);
                    for (var i = musicHistory.Count - 1; i >= musicHistory.Count - limit; i--)
                    {
                        var record = musicHistory[i];
                        column.Add(ContentVisualWarmConsoleUi.CreateTinyPathLabel(
                            record.Outcome + " · " + record.State
                            + " · " + record.StableSource
                            + " · gen=" + record.MusicGeneration
                            + (string.IsNullOrEmpty(record.ActualClipKey)
                                ? string.Empty
                                : " · " + record.ActualClipKey)));
                    }

                    for (var i = 0; i < (musicAnomalies?.Count ?? 0) && i < 8; i++)
                    {
                        var anomaly = musicAnomalies[musicAnomalies.Count - 1 - i];
                        column.Add(ContentVisualWarmConsoleUi.CreateTinyPathLabel(
                            "gen=" + anomaly.MusicGeneration
                            + " · state=" + anomaly.DesiredState
                            + " · binding=" + anomaly.FinalBindingClipKey
                            + " · source=" + anomaly.RequestSource
                            + " · scene=" + anomaly.SceneName
                            + " · chain=" + anomaly.ChainId
                            + " · batch=" + anomaly.BatchId
                            + " · unknown=" + string.Join(", ", anomaly.UnknownSources.Select(source => source.DisplayName))));
                    }
                });
            contentRoot.Add(section);
#endif
        }

        private void BuildMusicAuthoringSection()
        {
            contentRoot.Add(ContentVisualWarmConsoleUi.CreateSectionCard(
                "BGM 绑定与安全试听",
                "BGM 绑定独立于 SFX 工作副本；试听只允许 MusicSystem 持有一个 Preview 来源，切换前会释放旧 Preview。",
                column =>
                {
                    if (musicSession.Entries.Count == 0)
                    {
                        column.Add(ContentVisualWarmConsoleUi.CreateDescriptionLabel("audio_music.json 没有可编辑绑定。"));
                        return;
                    }

                    for (var i = 0; i < musicSession.Entries.Count; i++)
                    {
                        BuildMusicEntry(column, musicSession.Entries[i]);
                    }
                }));
        }

        private void BuildMusicEntry(VisualElement column, MusicBindingEditorEntry entry)
        {
            var dto = entry.Dto;
            var row = new VisualElement();
            row.style.paddingTop = 6;
            row.style.paddingBottom = 8;
            row.style.marginBottom = 6;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = ContentVisualWarmConsoleUi.Theme.Divider;

            row.Add(ContentVisualWarmConsoleUi.CreateTitleLabel(
                entry.State + (entry.IsDirty ? " · 未保存" : string.Empty),
                13,
                true,
                ContentVisualWarmConsoleUi.Theme.TextPrimary));

            var actions = new List<Button>
            {
                new Button(() => PreviewMusic(entry)) { text = "试听 BGM" },
                new Button(() => SaveMusicEntry(entry)) { text = "保存" },
                new Button(() => RevertMusicEntry(entry)) { text = "回撤" },
            };
            row.Add(ContentVisualWarmConsoleUi.CreateButtonRow(actions.ToArray()));

            var clip = new TextField { value = dto.clipKey ?? string.Empty };
            clip.RegisterValueChangedCallback(evt =>
            {
                dto.clipKey = evt.newValue ?? string.Empty;
                ChangedMusicEntry();
            });
            row.Add(ContentVisualWarmConsoleUi.WrapControlRow("素材键", clip, 110f));

            var volume = new FloatField { value = dto.volumeDb };
            volume.RegisterValueChangedCallback(evt =>
            {
                dto.volumeDb = evt.newValue;
                ChangedMusicEntry();
            });
            row.Add(ContentVisualWarmConsoleUi.WrapControlRow("作者音量 dB", volume, 110f));

            var startOffset = new FloatField { value = dto.startOffsetSeconds };
            startOffset.RegisterValueChangedCallback(evt =>
            {
                dto.startOffsetSeconds = Mathf.Max(0f, evt.newValue);
                ChangedMusicEntry();
            });
            row.Add(ContentVisualWarmConsoleUi.WrapControlRow("素材内部起播点", startOffset, 110f));

            var fadeIn = new FloatField { value = dto.fadeInSeconds };
            fadeIn.RegisterValueChangedCallback(evt =>
            {
                dto.fadeInSeconds = Mathf.Max(0f, evt.newValue);
                ChangedMusicEntry();
            });
            row.Add(ContentVisualWarmConsoleUi.WrapControlRow("淡入", fadeIn, 110f));

            var fadeOut = new FloatField { value = dto.fadeOutSeconds };
            fadeOut.RegisterValueChangedCallback(evt =>
            {
                dto.fadeOutSeconds = Mathf.Max(0f, evt.newValue);
                ChangedMusicEntry();
            });
            row.Add(ContentVisualWarmConsoleUi.WrapControlRow("淡出", fadeOut, 110f));

            var enabled = new Toggle("启用") { value = dto.enabled };
            enabled.RegisterValueChangedCallback(evt =>
            {
                dto.enabled = evt.newValue;
                ChangedMusicEntry();
            });
            row.Add(enabled);

            var loop = new Toggle("循环") { value = dto.loop };
            loop.RegisterValueChangedCallback(evt =>
            {
                dto.loop = evt.newValue;
                ChangedMusicEntry();
            });
            row.Add(loop);
            column.Add(row);
        }

        private void SaveMusicEntry(MusicBindingEditorEntry entry)
        {
            if (!musicSession.TrySave(entry, out var error))
            {
                status = error ?? "BGM 保存失败。";
                EditorUtility.DisplayDialog("BGM 保存失败", status, "确定");
                return;
            }

            status = "已保存 BGM：" + entry.State;
            RefreshContent();
        }

        private void RevertMusicEntry(MusicBindingEditorEntry entry)
        {
            musicSession.Revert(entry);
            status = "已回撤 BGM：" + entry.State;
            RefreshContent();
        }

        private void ChangedMusicEntry()
        {
            status = "BGM 工作副本已修改；尚未写入正式 JSON。";
            RefreshContent();
        }

        private void Changed(AudioBindingEditorEntry entry)
        {
            status = "工作副本已修改；尚未写入正式 JSON。";
            RefreshList();
            RefreshContent();
        }

        private void SaveEntry(AudioBindingEditorEntry entry)
        {
            if (!session.TrySave(entry, out var error))
            {
                status = error ?? "保存失败。";
                EditorUtility.DisplayDialog("保存失败", status, "确定");
                return;
            }

            status = "已保存并人工确认：" + entry.Note;
            RefreshList();
            RefreshContent();
        }

        private void RunAiBindPreserve()
        {
            if (session.DirtyCount > 0 || musicSession.DirtyCount > 0)
            {
                if (!EditorUtility.DisplayDialog(
                        "AI 初始绑定",
                        "当前有未保存脏改动。继续将先写入 AI 绑定结果并重载磁盘（脏改动会丢失）。",
                        "继续绑定",
                        "取消"))
                {
                    return;
                }
            }

            var result = AudioAiInitialBinder.RunAndWrite(forceRebindAll: false);
            session.ReloadFromDisk();
            musicSession.ReloadFromDisk();
            status = result?.Report?.summary ?? "AI 绑定完成。";
            RefreshList();
            RefreshContent();
        }

        private static string AuthoringStatusSuffix(AudioBindingEditorEntry entry)
        {
            if (entry == null || entry.Dto == null)
            {
                return string.Empty;
            }

            if (AudioBindingAuthoringStatuses.IsHumanConfirmed(entry.Dto.authoringStatus))
            {
                return " · 人工确认";
            }

            if (AudioBindingAuthoringStatuses.IsAiDraft(entry.Dto.authoringStatus))
            {
                return " · AI 草稿";
            }

            return string.Empty;
        }

        private void SaveAll()
        {
            string sfxError;
            string musicError;
            var sfxSaved = session.TrySaveAll(out sfxError);
            var musicSaved = musicSession.TrySaveAll(out musicError);
            if (!sfxSaved || !musicSaved)
            {
                status = sfxError ?? musicError ?? "保存失败。";
                EditorUtility.DisplayDialog("保存失败", status, "确定");
                return;
            }

            status = "已保存全部 SFX/BGM 脏绑定（人工确认）。";
            RefreshList();
            RefreshContent();
        }

        private void RevertAll()
        {
            if ((session.DirtyCount > 0 || musicSession.DirtyCount > 0)
                && !EditorUtility.DisplayDialog(
                    "回撤全部脏绑定",
                    "将恢复最近一次成功保存的 SFX/BGM 磁盘快照；Play Mode 播放历史不会清除。继续？",
                    "回撤",
                    "取消"))
            {
                return;
            }

            session.RevertAllDirty();
            musicSession.RevertAllDirty();
            status = "已回撤全部脏工作副本；播放历史保留。";
            RefreshList();
            RefreshContent();
        }

        private void ReloadFromDisk()
        {
            if ((session.DirtyCount > 0 || musicSession.DirtyCount > 0)
                && !EditorUtility.DisplayDialog(
                    "从磁盘重载",
                    "将丢弃 SFX 脏条目 " + session.DirtyCount + " 条、BGM 脏条目 "
                    + musicSession.DirtyCount + " 条。Play Mode 播放历史也会随会话重载清空。继续？",
                    "丢弃并重载",
                    "取消"))
            {
                return;
            }

            try
            {
                session.ReloadFromDisk();
                musicSession.ReloadFromDisk();
                status = "已从正式 JSON 重载。";
                RefreshAll();
            }
            catch (Exception exception)
            {
                status = "加载失败：" + exception.Message;
                EditorUtility.DisplayDialog("加载失败", status, "确定");
            }
        }

        private void PreviewEntry(AudioBindingEditorEntry entry)
        {
            var option = session.FindClipOption(entry.Dto?.clipKey);
            var clip = option == null ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(option.AssetPath);
            var offset = entry.Dto?.startOffsetSeconds ?? 0f;
            var delay = previewWithBindingDelay ? Math.Max(0f, entry.Dto?.bindingDelaySeconds ?? 0f) : 0f;
            if (delay > 0f)
            {
                var startedAt = EditorApplication.timeSinceStartup;
                void WaitForDelay()
                {
                    if (EditorApplication.timeSinceStartup - startedAt < delay)
                    {
                        EditorApplication.delayCall += WaitForDelay;
                        return;
                    }

                    AudioBindingEditorPreview.Play(clip, offset);
                }

                EditorApplication.delayCall += WaitForDelay;
                status = "等待真实绑定延迟后试听：" + delay.ToString("0.###") + "s · " + entry.Note;
                RefreshContent();
                return;
            }

            if (!AudioBindingEditorPreview.Play(clip, offset))
            {
                status = "无法试听：素材未导入或 AudioUtil 不可用。";
            }
            else
            {
                status = "正在试听：" + entry.Note;
            }

            RefreshContent();
        }

        private void PreviewClipKey(string clipKey, float startOffsetSeconds)
        {
            var option = session.FindClipOption(clipKey);
            var clip = option == null ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(option.AssetPath);
            if (!AudioBindingEditorPreview.Play(clip, startOffsetSeconds))
            {
                status = "无法试听：素材未导入或 AudioUtil 不可用。";
            }
            else
            {
                status = "正在试听变体素材：" + clipKey;
            }

            RefreshContent();
        }


        private void StopPreview()
        {
            AudioBindingEditorPreview.Stop();
            var architecture = NineGridArchitecture.Interface;
            var music = architecture?.GetSystem<IMusicSystem>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            music?.EndPreview("AudioBindingEditorWindow.StopPreview");
#endif
        }

        private void PreviewMusic(MusicBindingEditorEntry entry)
        {
            if (entry?.Dto == null)
            {
                return;
            }

            var architecture = NineGridArchitecture.Interface;
            var music = architecture?.GetSystem<IMusicSystem>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (music == null)
            {
                status = "BGM 试听不可用：MusicSystem 未注册。";
                RefreshContent();
                return;
            }

            var result = music.BeginPreview(new MusicPreviewRequest(
                entry.Dto.clipKey,
                entry.Dto.volumeDb,
                entry.Dto.startOffsetSeconds,
                entry.Dto.fadeInSeconds,
                entry.Dto.loop));
            status = result.Succeeded ? "正在试听 BGM：" + entry.State : result.Reason;
            RefreshContent();
#endif
        }

        private void StopMusicPreview()
        {
            var architecture = NineGridArchitecture.Interface;
            var music = architecture?.GetSystem<IMusicSystem>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            music?.EndPreview("AudioBindingEditorWindow.Close");
#endif
        }

        private void StopUnknownMusic()
        {
            var architecture = NineGridArchitecture.Interface;
            var music = architecture?.GetSystem<IMusicSystem>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (music == null)
            {
                status = "无法停止未知 BGM：MusicSystem 未注册。";
            }
            else
            {
                var result = music.StopUnknownMusic("AudioBindingEditorWindow.StopUnknownMusic");
                status = result.HasUnknownSources
                    ? "仍有 " + result.UnknownSources.Count + " 条未知 Music 来源。"
                    : "未知 Music 来源已收口。";
            }

            RefreshContent();
#endif
        }

        private void UpdateStatusBox()
        {
            if (statusHelpBox == null)
            {
                return;
            }

            statusHelpBox.text = string.IsNullOrEmpty(status)
                ? "工作副本与正式 JSON 分离。保存按钮成功后才会写入 Assets/Resources/audio/audio_bindings.json。"
                : status;
        }
    }
}
#endif
