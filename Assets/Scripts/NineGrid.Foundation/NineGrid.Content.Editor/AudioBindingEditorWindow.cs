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
            AudioBindingEditorPreview.Stop();
            if (session.DirtyCount > 0 && !EditorApplication.isCompiling)
            {
                EditorUtility.DisplayDialog(
                    "声音绑定未保存",
                    "关闭工作台不会自动保存。当前有 " + session.DirtyCount + " 条未保存修改。",
                    "知道了");
            }
        }

        private void OnBeforeAssemblyReload()
        {
            if (session.DirtyCount <= 0)
            {
                return;
            }

            EditorUtility.DisplayDialog(
                "重载前存在未保存声音绑定",
                "Unity 即将重载程序集；未保存的声音绑定工作副本不会自动写回正式 JSON。脏条目："
                + session.DirtyCount,
                "知道了");
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode && session.DirtyCount > 0)
            {
                EditorUtility.DisplayDialog(
                    "退出 Play Mode 前存在未保存声音绑定",
                    "退出 Play Mode 不会自动保存声音绑定。当前工作副本会保留在本窗口；脏条目："
                    + session.DirtyCount,
                    "知道了");
            }

            RefreshLiveHistory();
            Repaint();
        }

        private void Update()
        {
            if (!Application.isPlaying || EditorApplication.timeSinceStartup < nextLiveRefresh)
            {
                return;
            }

            nextLiveRefresh = EditorApplication.timeSinceStartup + 0.25d;
            RefreshLiveHistory();
            RefreshList();
            RefreshContent();
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
                ("保存全部脏改动", SaveAll, "把全部脏工作副本写入正式 JSON"),
                ("回撤全部脏改动", RevertAll, "恢复最近一次成功保存的磁盘快照，不清除播放历史"),
                ("从磁盘重载", ReloadFromDisk, "丢弃未保存工作副本并重新读取正式 JSON"),
                ("停止试听", StopPreview, "停止当前编辑器素材试听")));

            var split = new TwoPaneSplitView(0, 320f, TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1;
            rootVisualElement.Add(split);
            split.Add(BuildSidebar());
            split.Add(BuildContentPane());
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
            contentRoot.Add(ContentVisualWarmConsoleUi.CreateStatsGrid(
                ("筛选结果", session.GetFilteredEntries().Count().ToString(), "当前列表可见条目"),
                ("未保存", session.DirtyCount.ToString(), "保存后写入正式 JSON"),
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

        private void BuildEntrySection(AudioBindingEditorEntry entry)
        {
            contentRoot.Add(ContentVisualWarmConsoleUi.CreatePageHeader(
                string.IsNullOrWhiteSpace(entry.Note) ? "（无音效说明）" : entry.Note,
                entry.CueId + " · " + (string.IsNullOrEmpty(entry.Module) ? "未声明模块" : entry.Module)
                + (string.IsNullOrEmpty(entry.AuthoritativeEmitter) ? string.Empty : " · " + entry.AuthoritativeEmitter)));

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
                actions.Add(new Button(() => SaveEntry(entry)) { text = "保存此条" });
                actions.Add(new Button(() =>
                {
                    session.Revert(entry);
                    RefreshList();
                    RefreshContent();
                }) { text = "回撤此条" });
                actions.Add(new Button(() => PreviewEntry(entry)) { text = "试听当前素材" });
            }

            contentRoot.Add(ContentVisualWarmConsoleUi.CreateButtonRow(actions.ToArray()));

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

                    var enabled = new Toggle("启用") { value = dto.enabled };
                    enabled.RegisterValueChangedCallback(evt =>
                    {
                        dto.enabled = evt.newValue;
                        Changed(entry);
                    });
                    column.Add(enabled);
                });
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

            status = "已保存：" + entry.Note;
            RefreshList();
            RefreshContent();
        }

        private void SaveAll()
        {
            if (!session.TrySaveAll(out var error))
            {
                status = error ?? "保存失败。";
                EditorUtility.DisplayDialog("保存失败", status, "确定");
                return;
            }

            status = "已保存全部脏绑定。";
            RefreshList();
            RefreshContent();
        }

        private void RevertAll()
        {
            if (session.DirtyCount > 0
                && !EditorUtility.DisplayDialog(
                    "回撤全部脏绑定",
                    "将恢复最近一次成功保存的磁盘快照；Play Mode 播放历史不会清除。继续？",
                    "回撤",
                    "取消"))
            {
                return;
            }

            session.RevertAllDirty();
            status = "已回撤全部脏工作副本；播放历史保留。";
            RefreshList();
            RefreshContent();
        }

        private void ReloadFromDisk()
        {
            if (session.DirtyCount > 0
                && !EditorUtility.DisplayDialog(
                    "从磁盘重载",
                    "将丢弃 " + session.DirtyCount + " 条未保存声音绑定改动；播放历史也会随会话重载清空。继续？",
                    "丢弃并重载",
                    "取消"))
            {
                return;
            }

            try
            {
                session.ReloadFromDisk();
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
            if (!AudioBindingEditorPreview.Play(clip, entry.Dto?.startOffsetSeconds ?? 0f))
            {
                status = "无法试听：素材未导入或 AudioUtil 不可用。";
                RefreshContent();
            }
            else
            {
                status = "正在试听：" + entry.Note;
                RefreshContent();
            }
        }

        private static void StopPreview()
        {
            AudioBindingEditorPreview.Stop();
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
