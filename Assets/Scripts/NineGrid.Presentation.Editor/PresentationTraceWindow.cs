using System.Collections.Generic;
using NineGrid.Presentation.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Presentation.Editor
{
    public sealed class PresentationTraceWindow : EditorWindow
    {
        private readonly List<PresentationTraceEntry> mEntries = new();
        private Vector2 mScroll;
        private PresentationTraceChannel mChannelFilter = PresentationTraceChannel.All;
        private PresentationTraceLevel mMinLevel = PresentationTraceLevel.Trace;
        private int mBatchFilter;
        private bool mAutoScroll = true;
        private string mStatus = string.Empty;

        [MenuItem("NineGrid/Presentation Trace")]
        public static void Open()
        {
            var window = GetWindow<PresentationTraceWindow>("Presentation Trace");
            window.minSize = new Vector2(480f, 320f);
            window.Show();
        }

        private void OnEnable()
        {
            PresentationTrace.EntryRecorded += HandleEntryRecorded;
            EditorApplication.playModeStateChanged += HandlePlayModeChanged;
            RefreshEntries();
        }

        private void OnDisable()
        {
            PresentationTrace.EntryRecorded -= HandleEntryRecorded;
            EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        }

        private void HandlePlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
                RefreshEntries();
                Repaint();
            }
        }

        private void HandleEntryRecorded(PresentationTraceEntry entry)
        {
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawFilters();

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("进入 Play Mode 后查看实时 trace。", MessageType.Info);
            }

            RefreshEntries();
            mScroll = EditorGUILayout.BeginScrollView(mScroll);
            for (var i = 0; i < mEntries.Count; i++)
            {
                var entry = mEntries[i];
                if (!PassesFilter(entry))
                {
                    continue;
                }

                var color = ColorForLevel(entry.Level);
                var previous = GUI.contentColor;
                GUI.contentColor = color;
                EditorGUILayout.LabelField(entry.FormatConsoleLine(), EditorStyles.wordWrappedLabel);
                GUI.contentColor = previous;
            }

            EditorGUILayout.EndScrollView();

            if (mAutoScroll && mEntries.Count > 0)
            {
                mScroll.y = float.MaxValue;
            }

            if (!string.IsNullOrEmpty(mStatus))
            {
                EditorGUILayout.HelpBox(mStatus, MessageType.None);
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
            {
                RefreshEntries();
            }

            if (GUILayout.Button("Copy Buffer JSON", EditorStyles.toolbarButton))
            {
                EditorGUIUtility.systemCopyBuffer = PresentationTrace.DumpRingBufferJson();
                mStatus = "Ring buffer JSON copied.";
            }

            if (GUILayout.Button("Copy Last Stall Dump Path", EditorStyles.toolbarButton))
            {
                var path = PresentationTrace.LastStallDumpPath;
                EditorGUIUtility.systemCopyBuffer = path;
                mStatus = string.IsNullOrEmpty(path) ? "No stall dump yet." : "Copied: " + path;
            }

            mAutoScroll = GUILayout.Toggle(mAutoScroll, "Auto Scroll", EditorStyles.toolbarButton);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawFilters()
        {
            EditorGUILayout.BeginHorizontal();
            mChannelFilter = (PresentationTraceChannel)EditorGUILayout.EnumFlagsField("Channels", mChannelFilter);
            mMinLevel = (PresentationTraceLevel)EditorGUILayout.EnumPopup("Min Level", mMinLevel);
            mBatchFilter = EditorGUILayout.IntField("Batch #", mBatchFilter);
            EditorGUILayout.EndHorizontal();
        }

        private void RefreshEntries()
        {
            PresentationTrace.Buffer.CopyTo(mEntries);
        }

        private bool PassesFilter(PresentationTraceEntry entry)
        {
            if ((mChannelFilter & entry.Channel) == 0)
            {
                return false;
            }

            if (entry.Level < mMinLevel)
            {
                return false;
            }

            if (mBatchFilter > 0 && entry.BatchId != mBatchFilter)
            {
                return false;
            }

            return true;
        }

        private static Color ColorForLevel(PresentationTraceLevel level)
        {
            switch (level)
            {
                case PresentationTraceLevel.Warn:
                    return new Color(1f, 0.85f, 0.2f);
                case PresentationTraceLevel.Error:
                case PresentationTraceLevel.Stall:
                    return new Color(1f, 0.35f, 0.35f);
                case PresentationTraceLevel.Info:
                    return new Color(0.75f, 0.95f, 1f);
                default:
                    return Color.gray;
            }
        }
    }
}
