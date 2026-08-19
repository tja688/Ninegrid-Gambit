using System;
using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using TMPro;
using UnityEngine;

namespace NineGrid.Presentation.Ui
{
    /// <summary>
    /// 主菜单「继续游戏」加载弹窗：绑定场景预置 <c>MainPanel/加载的UI槽位</c>（默认失活，
    /// 与 ContinueGame 按钮解挂、1x 缩放），列出已有存档（自动档优先 + 3 手动槽，至多 4 条），
    /// 点击条目即读档；打开时 Acquire 半黑屏。
    /// 「关闭面板 (1)」/ Esc 关闭。无任何存档时 <see cref="RequestOpen"/> 返回 false（调用方播拒绝反馈）。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class MainMenuLoadPanel : MonoBehaviour
    {
        public const string PanelNodeName = "加载的UI槽位";
        private const string CloseButtonName = "关闭面板 (1)";
        private const string RowNamePrefix = "加载条目模板";
        private const string DimmerReason = "main-menu-load";

        private static readonly Color RowHoverTint = new Color(1f, 0.93f, 0.72f, 1f);

        private static MainMenuLoadPanel sInstance;

        private sealed class Row
        {
            public Transform Node;
            public TMP_Text Text;
            public SpriteRenderer Sprite;
            public Color BaseColor = Color.white;
            public string SlotId;
        }

        private readonly List<Row> mRows = new List<Row>(4);
        private bool mBound;
        private bool mDimmerHeld;

        public static bool IsOpen =>
            sInstance != null && sInstance.gameObject.activeSelf;

        /// <summary>存在任意存档（自动档或手动槽）才可打开。</summary>
        public static bool HasAnySave()
        {
            if (RunSaveService.TryReadSlot(RunSaveService.AutoSlotId, out _))
            {
                return true;
            }

            for (var i = 1; i <= RunSaveService.ManualSlotCount; i++)
            {
                if (RunSaveService.TryReadSlot(RunSaveService.ManualSlotId(i), out _))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>打开加载弹窗；场景缺预置或无任何存档时返回 false。</summary>
        public static bool RequestOpen()
        {
            var live = FindSceneInstance() ?? EnsureFromScene();
            if (live == null || !HasAnySave())
            {
                return false;
            }

            live.EnsureBound();
            live.Refresh();
            live.SetOpen(true);

            InteractionAudioCues.Pulse(
                InteractionAudioCues.UiConfirm,
                "MainMenuLoadPanel.RequestOpen",
                "main_menu.continue.open");
            return true;
        }

        public static void CloseIfOpen()
        {
            if (sInstance != null)
            {
                sInstance.SetOpen(false);
            }
        }

        private void Awake()
        {
            sInstance = this;
            if (Application.isPlaying)
            {
                EnsureBound();
            }
        }

        private void OnDestroy()
        {
            if (sInstance == this)
            {
                sInstance = null;
            }

            ReleaseDimmer();
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            // 只服务主菜单相位；读档启动流程后自动收起。
            var shell = NineGridArchitecture.Interface?.GetSystem<IGameFlowShellSystem>()
                        ?? NineGridArchitecture.Current?.GetSystem<IGameFlowShellSystem>();
            if (shell != null && shell.State.Value != GameFlowShellState.MainMenu)
            {
                SetOpen(false);
                return;
            }

            if (KeyboardUtility.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }

        private void EnsureBound()
        {
            if (mBound)
            {
                return;
            }

            mRows.Clear();
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                if (child.name.StartsWith(RowNamePrefix, StringComparison.Ordinal))
                {
                    var row = new Row
                    {
                        Node = child,
                        Text = child.GetComponentInChildren<TMP_Text>(true),
                        Sprite = child.GetComponent<SpriteRenderer>(),
                    };
                    if (row.Sprite != null)
                    {
                        row.BaseColor = row.Sprite.color;
                    }

                    mRows.Add(row);
                    continue;
                }

                if (string.Equals(child.name, CloseButtonName, StringComparison.Ordinal))
                {
                    WireButton(child, Close, BattleUiDimmerOverlay.CloseHitSort + 2, hoverScale: 1.1f);
                }
            }

            // 行按视觉从上到下排序（本地 Y 降序）。
            mRows.Sort((a, b) => b.Node.localPosition.y.CompareTo(a.Node.localPosition.y));
            for (var i = 0; i < mRows.Count; i++)
            {
                WireRow(mRows[i]);
            }

            WireBlocker();
            mBound = true;
        }

        private void WireRow(Row row)
        {
            if (row?.Node == null)
            {
                return;
            }

            var collider = row.Node.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = row.Node.gameObject.AddComponent<BoxCollider2D>();
            }

            if (collider.size.x < 0.01f || collider.size.y < 0.01f)
            {
                collider.size = row.Sprite != null && row.Sprite.drawMode != SpriteDrawMode.Simple
                    ? row.Sprite.size
                    : new Vector2(4.6f, 0.5f);
            }

            collider.isTrigger = false;
            collider.enabled = true;

            var button = row.Node.GetComponent<WorldUiHitButton>();
            if (button == null)
            {
                button = row.Node.gameObject.AddComponent<WorldUiHitButton>();
            }

            button.Configure(
                () => OnRowClicked(row),
                BattleUiDimmerOverlay.CloseHitSort + 1,
                PointerHitSurfacePriorities.Overlay,
                hoverScale: 1f,
                onHoverEnter: () =>
                {
                    if (row.Sprite != null)
                    {
                        row.Sprite.color = RowHoverTint;
                    }

                    InteractionAudioCues.Pulse(
                        InteractionAudioCues.MainMenuHover,
                        "MainMenuLoadPanel.RowHover",
                        "main_menu.continue.row");
                },
                onHoverExit: () =>
                {
                    if (row.Sprite != null)
                    {
                        row.Sprite.color = row.BaseColor;
                    }
                });
        }

        private void WireButton(Transform target, Action onClick, int hitSort, float hoverScale)
        {
            var collider = target.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = target.gameObject.AddComponent<BoxCollider2D>();
            }

            if (collider.size.x < 0.01f || collider.size.y < 0.01f)
            {
                collider.size = new Vector2(0.6f, 0.6f);
            }

            collider.isTrigger = false;
            collider.enabled = true;

            var button = target.GetComponent<WorldUiHitButton>();
            if (button == null)
            {
                button = target.gameObject.AddComponent<WorldUiHitButton>();
            }

            button.Configure(
                onClick,
                hitSort,
                PointerHitSurfacePriorities.Overlay,
                hoverScale,
                onHoverEnter: () => InteractionAudioCues.Pulse(
                    InteractionAudioCues.MainMenuHover,
                    "MainMenuLoadPanel.ButtonHover",
                    "main_menu.continue"));
        }

        /// <summary>弹窗底板吞点击，避免点缝隙误触其下的主菜单按钮。</summary>
        private void WireBlocker()
        {
            var collider = GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = gameObject.AddComponent<BoxCollider2D>();
            }

            var renderer = GetComponent<SpriteRenderer>();
            collider.size = renderer != null && renderer.drawMode != SpriteDrawMode.Simple
                ? renderer.size
                : new Vector2(6f, 4f);
            collider.offset = Vector2.zero;
            collider.isTrigger = false;
            collider.enabled = true;

            var blocker = GetComponent<WorldUiHitButton>();
            if (blocker == null)
            {
                blocker = gameObject.AddComponent<WorldUiHitButton>();
            }

            blocker.Configure(null, BattleUiDimmerOverlay.HitSort, PointerHitSurfacePriorities.Overlay);
        }

        private void Refresh()
        {
            var entries = BuildEntries();
            for (var i = 0; i < mRows.Count; i++)
            {
                var row = mRows[i];
                if (row?.Node == null)
                {
                    continue;
                }

                if (i < entries.Count)
                {
                    row.SlotId = entries[i].SlotId;
                    if (row.Text != null)
                    {
                        row.Text.text = entries[i].Label;
                    }

                    if (row.Sprite != null)
                    {
                        row.Sprite.color = row.BaseColor;
                    }

                    row.Node.gameObject.SetActive(true);
                }
                else
                {
                    row.SlotId = null;
                    row.Node.gameObject.SetActive(false);
                }
            }
        }

        private readonly struct Entry
        {
            public Entry(string slotId, string label)
            {
                SlotId = slotId;
                Label = label;
            }

            public string SlotId { get; }

            public string Label { get; }
        }

        private static List<Entry> BuildEntries()
        {
            var entries = new List<Entry>(4);
            RunSaveSnapshot auto;
            if (RunSaveService.TryReadSlot(RunSaveService.AutoSlotId, out auto))
            {
                entries.Add(new Entry(
                    RunSaveService.AutoSlotId,
                    string.Format(
                        NineGrid.Core.Localization.L10n.Tr("save.auto_entry", "自动 {0}"),
                        RunSaveLoadPanel.FormatEntry(auto))));
            }

            for (var i = 1; i <= RunSaveService.ManualSlotCount; i++)
            {
                var slotId = RunSaveService.ManualSlotId(i);
                RunSaveSnapshot snapshot;
                if (RunSaveService.TryReadSlot(slotId, out snapshot))
                {
                    entries.Add(new Entry(slotId, RunSaveLoadPanel.FormatEntry(snapshot)));
                }
            }

            return entries;
        }

        private void OnRowClicked(Row row)
        {
            if (row == null || string.IsNullOrEmpty(row.SlotId) || RunSaveService.IsLoading)
            {
                return;
            }

            InteractionAudioCues.Pulse(
                InteractionAudioCues.UiConfirm,
                "MainMenuLoadPanel.Load",
                "main_menu.continue.load." + row.SlotId);
            SetOpen(false);
            RunSaveService.RequestLoadSlot(row.SlotId);
        }

        private void Close()
        {
            InteractionAudioCues.Pulse(
                InteractionAudioCues.UiCancel,
                "MainMenuLoadPanel.Close",
                "main_menu.continue.close");
            SetOpen(false);
        }

        private void SetOpen(bool open)
        {
            if (open)
            {
                if (!gameObject.activeSelf)
                {
                    HoldDimmer();
                    gameObject.SetActive(true);
                }

                return;
            }

            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
            else
            {
                ReleaseDimmer();
            }
        }

        private void OnDisable()
        {
            ReleaseDimmer();
        }

        private void HoldDimmer()
        {
            if (mDimmerHeld)
            {
                return;
            }

            if (BattleUiDimmerOverlay.TryAcquire(DimmerReason))
            {
                mDimmerHeld = true;
            }
        }

        private void ReleaseDimmer()
        {
            if (!mDimmerHeld)
            {
                return;
            }

            mDimmerHeld = false;
            BattleUiDimmerOverlay.Release(DimmerReason);
        }

        private static MainMenuLoadPanel FindSceneInstance()
        {
            var all = Resources.FindObjectsOfTypeAll<MainMenuLoadPanel>();
            for (var i = 0; i < all.Length; i++)
            {
                var panel = all[i];
                if (panel != null && IsSceneObject(panel.gameObject))
                {
                    return panel;
                }
            }

            return null;
        }

        private static MainMenuLoadPanel EnsureFromScene()
        {
            var all = Resources.FindObjectsOfTypeAll<Transform>();
            for (var i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null || t.name != PanelNodeName || !IsSceneObject(t.gameObject))
                {
                    continue;
                }

                var panel = t.GetComponent<MainMenuLoadPanel>();
                return panel != null ? panel : t.gameObject.AddComponent<MainMenuLoadPanel>();
            }

            return null;
        }

        private static bool IsSceneObject(GameObject go)
        {
            return go != null && go.scene.IsValid() && go.scene.isLoaded;
        }
    }
}
