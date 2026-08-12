using System;
using NineGrid.Core;
using NineGrid.Core.Localization;
using NineGrid.Presentation.Systems;
using TMPro;
using UnityEngine;

namespace NineGrid.Presentation.Ui
{
    /// <summary>
    /// MainScene 静态标签本地化（ADR-0046）：显式序列化 TMP 引用 + ui 键数组，禁止 Find。
    /// Awake 捕获场景内中文原文作为默认值（zh 写回原文、en 查 ui 表缺键回中文），
    /// 订阅 <see cref="ILanguageSettingsSystem.Changed"/> 即时刷新；失活面板下的 TMP 同样可写。
    /// 挂 MainScene 一处即可；条目跨主菜单 / 局内功能菜单 / 战斗信息预览等面板。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneTextLocalizer : MonoBehaviour
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("场景静态标签 TMP（显式引用，禁止 Find；PrefabInstance 覆写的实例 TMP 直接拖入）。")]
            public TMP_Text target;

            [Tooltip("ui.json 键（如 menu.start）。")]
            public string key;

            [NonSerialized]
            public string capturedZhText;
        }

        [SerializeField]
        private Entry[] entries = Array.Empty<Entry>();

        private ILanguageSettingsSystem mLanguage;
        private bool mCaptured;

        private void Awake()
        {
            CaptureDefaults();
        }

        private void OnEnable()
        {
            TrySubscribe();
            ApplyAll();
        }

        private void Start()
        {
            // WireHosts 在 SceneRoot OnBind 注册语言系统，可能晚于本组件 OnEnable；Start 再补挂。
            TrySubscribe();
            ApplyAll();
        }

        private void OnDisable()
        {
            if (mLanguage != null)
            {
                mLanguage.Changed -= OnLanguageChanged;
                mLanguage = null;
            }
        }

        private void TrySubscribe()
        {
            if (mLanguage != null)
            {
                return;
            }

            var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            var language = arch?.GetSystem<ILanguageSettingsSystem>();
            if (language == null)
            {
                return;
            }

            mLanguage = language;
            mLanguage.Changed += OnLanguageChanged;
        }

        private void OnLanguageChanged(string code)
        {
            ApplyAll();
        }

        private void CaptureDefaults()
        {
            if (mCaptured || entries == null)
            {
                return;
            }

            mCaptured = true;
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry?.target != null)
                {
                    entry.capturedZhText = entry.target.text;
                }
            }
        }

        private void ApplyAll()
        {
            CaptureDefaults();
            if (entries == null)
            {
                return;
            }

            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry?.target == null || string.IsNullOrEmpty(entry.key))
                {
                    continue;
                }

                var zhDefault = entry.capturedZhText ?? entry.target.text;
                entry.target.text = L10n.Tr(entry.key, zhDefault);
            }
        }
    }
}
