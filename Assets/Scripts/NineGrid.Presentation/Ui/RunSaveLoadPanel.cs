using System;
using System.Collections.Generic;
using System.Globalization;
using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using TMPro;
using UnityEngine;

namespace NineGrid.Presentation.Ui
{
    /// <summary>
    /// 局内功能菜单「存档/读档模块」接线：
    /// 默认展示保存面板（保存钮激活 / 加载钮失活），「加载」切换到读档列表；
    /// 保存视图恒四行：槽位一为默认存档（自动档，系统维护），槽位二至四手动可存——
    /// 空位直接存，已有存档先经「提示框」确认覆盖；
    /// 加载视图只列出已有存档（自动档 + 手动槽，至多 4 条），点击即读档。
    /// 条目按场景预置的「保存条目模板」「加载条目模板」克隆。
    /// 存档颗粒度 = 当前/最近一场战斗的开始（<see cref="RunSaveService"/> 检查点）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunSaveLoadPanel : MonoBehaviour
    {
        public const string ModuleName = "存档/读档模块";
        private const string SaveButtonName = "保存";
        private const string LoadButtonName = "加载";
        private const string SlotPanelName = "UI槽位";
        private const string SaveEntryTemplateName = "保存条目模板";
        private const string LoadEntryTemplateName = "加载条目模板";
        private const string FunctionModuleName = "功能模块";

        // UI槽位面板本地可视区 Y∈[-1.5,+1.5]（Play 实测）；行高 0.5，4 行正好铺满。
        private const float RowTopLocalY = 1.05f;
        private const float RowSpacingLocalY = 0.75f;

        private static readonly Color HoverTint = new Color(1f, 0.93f, 0.72f, 1f);
        private static readonly Color DisabledTint = new Color(0.62f, 0.62f, 0.62f, 0.85f);
        private static readonly Color InactiveModeTint = new Color(0.55f, 0.55f, 0.55f, 0.9f);

        private enum PanelMode
        {
            Save,
            Load,
        }

        private bool mWired;
        private PanelMode mMode = PanelMode.Save;
        private bool mModeChosenThisOpen;

        private Transform mSaveButton;
        private Transform mLoadButton;
        private Transform mSlotPanel;
        private GameObject mSaveEntryTemplate;
        private GameObject mLoadEntryTemplate;
        private UiConfirmPrompt mPrompt;
        private readonly List<GameObject> mRows = new List<GameObject>(8);

        /// <summary>由 <see cref="PlayerAudioSettingsPanel"/> 在功能菜单接线时调用。</summary>
        public static void EnsureBound(Transform moduleRoot)
        {
            if (moduleRoot == null)
            {
                return;
            }

            if (moduleRoot.GetComponent<RunSaveLoadPanel>() == null)
            {
                moduleRoot.gameObject.AddComponent<RunSaveLoadPanel>();
            }
        }

        private void OnEnable()
        {
            EnsureWired();
            // 每次打开菜单默认展示保存面板（保存钮激活 / 加载钮失活）。
            if (!mModeChosenThisOpen)
            {
                mMode = PanelMode.Save;
            }

            Refresh();
        }

        private void OnDisable()
        {
            mModeChosenThisOpen = false;
        }

        private void EnsureWired()
        {
            if (mWired)
            {
                return;
            }

            mSaveButton = FindDirectChild(transform, SaveButtonName);
            mLoadButton = FindDirectChild(transform, LoadButtonName);
            mSlotPanel = FindDirectChild(transform, SlotPanelName);
            if (mSlotPanel != null)
            {
                var saveTemplate = FindDirectChild(mSlotPanel, SaveEntryTemplateName);
                var loadTemplate = FindDirectChild(mSlotPanel, LoadEntryTemplateName);
                mSaveEntryTemplate = saveTemplate != null ? saveTemplate.gameObject : null;
                mLoadEntryTemplate = loadTemplate != null ? loadTemplate.gameObject : null;
                mSaveEntryTemplate?.SetActive(false);
                mLoadEntryTemplate?.SetActive(false);
            }

            WireModeButton(mSaveButton, () => SetMode(PanelMode.Save));
            WireModeButton(mLoadButton, () => SetMode(PanelMode.Load));

            // 覆盖确认提示框在兄弟模块「功能模块/提示框」（与退出/回主菜单共用）。
            var functionModule = FindDirectChild(transform.parent, FunctionModuleName);
            mPrompt = UiConfirmPrompt.Attach(FindDirectChild(functionModule, UiConfirmPrompt.NodeName));

            if (mSlotPanel == null || mSaveEntryTemplate == null || mLoadEntryTemplate == null)
            {
                Debug.LogWarning(
                    "[RunSave] 存档/读档模块结构不完整（UI槽位/条目模板缺失），面板不可用。");
            }

            mWired = true;
        }

        private void SetMode(PanelMode mode)
        {
            mModeChosenThisOpen = true;
            if (mMode == mode)
            {
                Refresh();
                return;
            }

            mMode = mode;
            InteractionAudioCues.Pulse(
                InteractionAudioCues.UiPress,
                "RunSaveLoadPanel.SetMode",
                mode == PanelMode.Save ? "run_save.mode.save" : "run_save.mode.load");
            Refresh();
        }

        private void Refresh()
        {
            ClearRows();
            ApplyModeButtonVisuals();
            if (mSlotPanel == null || mSaveEntryTemplate == null || mLoadEntryTemplate == null)
            {
                return;
            }

            if (mMode == PanelMode.Save)
            {
                BuildSaveRows();
            }
            else
            {
                BuildLoadRows();
            }
        }

        private void BuildSaveRows()
        {
            var checkpoint = RunSaveService.CurrentCheckpoint;
            var row = 0;

            // 槽位一：默认存档（自动档，进入战斗自动写入，不可手动覆盖）。
            RunSaveSnapshot auto;
            var hasAuto = RunSaveService.TryReadSlot(RunSaveService.AutoSlotId, out auto);
            SpawnRow(
                mSaveEntryTemplate,
                row++,
                hasAuto
                    ? string.Format(
                        NineGrid.Core.Localization.L10n.Tr("save.default_entry", "默认存档　{0}"),
                        FormatEntry(auto))
                    : NineGrid.Core.Localization.L10n.Tr(
                        "save.default_none",
                        "默认存档（进入战斗自动写入）"),
                clickable: false,
                null);

            // 槽位二至四：手动槽。空位直接存；已有存档点击先确认覆盖。
            for (var i = 1; i <= RunSaveService.ManualSlotCount; i++)
            {
                var slotIndex = i;
                RunSaveSnapshot existing;
                var has = RunSaveService.TryReadSlot(RunSaveService.ManualSlotId(slotIndex), out existing);
                var label = has
                    ? FormatEntry(existing)
                    : string.Format(
                        NineGrid.Core.Localization.L10n.Tr("save.empty_slot", "空存档位 {0}"),
                        slotIndex);
                var clickable = checkpoint != null;
                SpawnRow(
                    mSaveEntryTemplate,
                    row++,
                    label,
                    clickable,
                    () => OnSaveRowClicked(slotIndex, has));
            }
        }

        private void BuildLoadRows()
        {
            var row = 0;
            RunSaveSnapshot auto;
            if (RunSaveService.TryReadSlot(RunSaveService.AutoSlotId, out auto))
            {
                SpawnRow(
                    mLoadEntryTemplate,
                    row++,
                    string.Format(
                        NineGrid.Core.Localization.L10n.Tr("save.auto_entry", "自动 {0}"),
                        FormatEntry(auto)),
                    clickable: true,
                    () => OnLoadRowClicked(RunSaveService.AutoSlotId));
            }

            for (var i = 1; i <= RunSaveService.ManualSlotCount; i++)
            {
                var slotId = RunSaveService.ManualSlotId(i);
                RunSaveSnapshot existing;
                if (!RunSaveService.TryReadSlot(slotId, out existing))
                {
                    continue;
                }

                SpawnRow(
                    mLoadEntryTemplate,
                    row++,
                    FormatEntry(existing),
                    clickable: true,
                    () => OnLoadRowClicked(slotId));
            }

            if (row == 0)
            {
                SpawnRow(
                    mLoadEntryTemplate,
                    0,
                    NineGrid.Core.Localization.L10n.Tr("save.none", "暂无存档"),
                    clickable: false,
                    null);
            }
        }

        private void OnSaveRowClicked(int slotIndex, bool occupied)
        {
            if (RunSaveService.CurrentCheckpoint == null)
            {
                return;
            }

            if (occupied && mPrompt != null)
            {
                // 已有存档：先经提示框确认覆盖，同意才写入。
                mPrompt.Show(
                    UiConfirmPrompt.OverwriteSaveMessage,
                    () => SaveToSlot(slotIndex));
                return;
            }

            SaveToSlot(slotIndex);
        }

        private void SaveToSlot(int slotIndex)
        {
            var ok = RunSaveService.SaveCheckpointToSlot(slotIndex);
            InteractionAudioCues.Pulse(
                ok ? InteractionAudioCues.UiConfirm : InteractionAudioCues.UiCancel,
                "RunSaveLoadPanel.Save",
                "run_save.slot." + slotIndex);
            Refresh();
        }

        private void OnLoadRowClicked(string slotId)
        {
            if (RunSaveService.IsLoading)
            {
                return;
            }

            InteractionAudioCues.Pulse(
                InteractionAudioCues.UiConfirm,
                "RunSaveLoadPanel.Load",
                "run_save.load." + slotId);
            PlayerAudioSettingsPanel.CloseIfOpen();
            RunSaveService.RequestLoadSlot(slotId);
        }

        private void SpawnRow(
            GameObject template,
            int rowIndex,
            string label,
            bool clickable,
            Action onClick)
        {
            var row = Instantiate(template, mSlotPanel);
            row.name = template.name + "_行" + rowIndex;
            var local = template.transform.localPosition;
            local.y = RowTopLocalY - rowIndex * RowSpacingLocalY;
            row.transform.localPosition = local;
            row.transform.localScale = template.transform.localScale;
            row.SetActive(true);
            mRows.Add(row);

            var text = row.GetComponentInChildren<TextMeshPro>(true);
            if (text != null)
            {
                text.text = label ?? string.Empty;
            }

            var sprite = row.GetComponent<SpriteRenderer>();
            var baseColor = sprite != null ? sprite.color : Color.white;
            if (!clickable)
            {
                if (sprite != null)
                {
                    sprite.color = DisabledTint;
                }

                if (text != null)
                {
                    var c = text.color;
                    c.a *= 0.55f;
                    text.color = c;
                }

                var idleCollider = row.GetComponent<BoxCollider2D>();
                if (idleCollider != null)
                {
                    idleCollider.enabled = false;
                }

                return;
            }

            var collider = row.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = row.AddComponent<BoxCollider2D>();
                var size = sprite != null && sprite.drawMode != SpriteDrawMode.Simple
                    ? sprite.size
                    : new Vector2(4f, 0.5f);
                collider.size = size;
            }

            collider.isTrigger = false;
            collider.enabled = true;

            var proxy = row.GetComponent<RowHitProxy>();
            if (proxy == null)
            {
                proxy = row.AddComponent<RowHitProxy>();
            }

            proxy.Configure(
                onClick,
                hovering =>
                {
                    if (sprite != null)
                    {
                        sprite.color = hovering ? HoverTint : baseColor;
                    }

                    if (hovering)
                    {
                        InteractionAudioCues.Pulse(
                            InteractionAudioCues.MainMenuHover,
                            "RunSaveLoadPanel.RowHover",
                            "run_save.row.hover");
                    }
                },
                BattleUiDimmerOverlay.CloseHitSort,
                PointerHitSurfacePriorities.Overlay);
        }

        private void ClearRows()
        {
            for (var i = 0; i < mRows.Count; i++)
            {
                if (mRows[i] != null)
                {
                    Destroy(mRows[i]);
                }
            }

            mRows.Clear();

            // 域重载会丢跟踪列表；按行名兜底扫掉残留克隆，避免叠行。
            if (mSlotPanel == null)
            {
                return;
            }

            for (var i = mSlotPanel.childCount - 1; i >= 0; i--)
            {
                var child = mSlotPanel.GetChild(i);
                if (child != null && child.name.Contains("_行"))
                {
                    Destroy(child.gameObject);
                }
            }
        }

        private void ApplyModeButtonVisuals()
        {
            ApplyModeButtonVisual(mSaveButton, mMode == PanelMode.Save);
            ApplyModeButtonVisual(mLoadButton, mMode == PanelMode.Load);
        }

        private static void ApplyModeButtonVisual(Transform button, bool active)
        {
            if (button == null)
            {
                return;
            }

            var sprite = button.GetComponent<SpriteRenderer>();
            if (sprite != null)
            {
                sprite.color = active ? Color.white : InactiveModeTint;
            }

            var text = button.GetComponentInChildren<TextMeshPro>(true);
            if (text != null)
            {
                var c = text.color;
                c.a = active ? 1f : 0.55f;
                text.color = c;
            }
        }

        private void WireModeButton(Transform button, Action onClick)
        {
            if (button == null)
            {
                return;
            }

            var collider = button.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = button.gameObject.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(0.5f, 0.5f);
            }

            collider.isTrigger = false;
            collider.enabled = true;

            var proxy = button.GetComponent<RowHitProxy>();
            if (proxy == null)
            {
                proxy = button.gameObject.AddComponent<RowHitProxy>();
            }

            proxy.Configure(
                onClick,
                hovering =>
                {
                    if (hovering)
                    {
                        InteractionAudioCues.Pulse(
                            InteractionAudioCues.MainMenuHover,
                            "RunSaveLoadPanel.ModeButtonHover",
                            "run_save.mode.hover");
                    }
                },
                BattleUiDimmerOverlay.CloseHitSort,
                PointerHitSurfacePriorities.Overlay);
        }

        /// <summary>存档条目展示文案（主菜单「继续游戏」加载面板共用）。</summary>
        internal static string FormatEntry(RunSaveSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return string.Empty;
            }

            var when = string.Empty;
            var ticks = snapshot.SavedAtTicksValue;
            if (ticks > 0)
            {
                var time = new DateTime(ticks);
                when = time.ToString(
                    NineGrid.Core.Localization.L10n.Tr("save.date_format", "M月d日 HH:mm"),
                    CultureInfo.InvariantCulture);
            }

            var name = string.IsNullOrEmpty(snapshot.avatarDisplayName)
                ? NineGrid.Core.Localization.L10n.Tr("save.default_hero_name", "冒险者")
                : snapshot.avatarDisplayName;
            return string.Format(
                NineGrid.Core.Localization.L10n.Tr("save.entry_format", "{0} {1} 层{2}·{3}"),
                when,
                name,
                snapshot.floor,
                snapshot.DisplayNode);
        }

        private static Transform FindDirectChild(Transform parent, string childName)
        {
            if (parent == null)
            {
                return null;
            }

            // 名字可能含 '/'（如「存档/读档模块」），不能用 transform.Find 的路径语义。
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child != null && string.Equals(child.name, childName, StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }

        /// <summary>存读档条目/按钮命中代理（Overlay 面，压过功能菜单面板 Swallow）。</summary>
        [DisallowMultipleComponent]
        [RequireComponent(typeof(BoxCollider2D))]
        private sealed class RowHitProxy : MonoBehaviour, IPointerHitTarget
        {
            private BoxCollider2D mCollider;
            private Action mOnClick;
            private Action<bool> mOnHover;
            private int mHitSort;
            private int mTypePriority;

            public Collider2D HitCollider =>
                mCollider != null ? mCollider : (mCollider = GetComponent<BoxCollider2D>());

            public int HitSortOrder => mHitSort;

            public int HitTypePriority => mTypePriority;

            public void Configure(Action onClick, Action<bool> onHover, int hitSort, int typePriority)
            {
                mOnClick = onClick;
                mOnHover = onHover;
                mHitSort = hitSort;
                mTypePriority = typePriority;
            }

            private void Awake()
            {
                mCollider = GetComponent<BoxCollider2D>();
                if (mCollider != null)
                {
                    mCollider.isTrigger = false;
                }
            }

            private void OnEnable() => PointerHitRegistry.Register(this);

            private void OnDisable()
            {
                mOnHover?.Invoke(false);
                PointerHitRegistry.Unregister(this);
            }

            public void HandlePointerEnter() => mOnHover?.Invoke(true);

            public void HandlePointerExit() => mOnHover?.Invoke(false);

            public void HandlePointerDown() => mOnClick?.Invoke();
        }
    }
}
