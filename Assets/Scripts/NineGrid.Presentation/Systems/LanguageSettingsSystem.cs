using System;
using NineGrid.Core;
using NineGrid.Core.Localization;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// 玩家语言偏好唯一读写接口（ADR-0046）：PlayerPrefs 持久化 + 翻译表重载 + Changed 通知。
    /// 切换只发生在主菜单；订阅者（场景标签 / 主菜单按钮 label）收到 Changed 自刷。
    /// </summary>
    public interface ILanguageSettingsSystem : ISystem
    {
        /// <summary>当前语言码（LanguageId.Zh / LanguageId.En）。</summary>
        string Current { get; }

        event Action<string> Changed;

        void SetLanguage(string code);

        /// <summary>zh ↔ en 互切；返回切换后的语言码。</summary>
        string Toggle();
    }

    public sealed class LanguageSettingsSystem : AbstractSystem, ILanguageSettingsSystem
    {
        private const string PreferencesKey = "NineGrid.LanguagePreference.v1";

        private readonly IPlayerAudioSettingsStore mStore;
        private string mCurrent;

        public LanguageSettingsSystem(IPlayerAudioSettingsStore store = null)
        {
            mStore = store ?? new PlayerPrefsAudioSettingsStore();
            mCurrent = LoadOrDefault();
            LocalizationCatalog.ConfigureRuntime(
                path => Resources.Load<TextAsset>(path)?.text,
                Debug.LogWarning);
            LocalizationCatalog.SetLanguage(mCurrent);
        }

        public string Current => mCurrent;

        public event Action<string> Changed;

        public static ILanguageSettingsSystem EnsureRegistered(IArchitecture architecture = null)
        {
            var arch = architecture ?? NineGridArchitecture.Interface;
            if (arch == null)
            {
                throw new InvalidOperationException("Architecture is not available for LanguageSettingsSystem.");
            }

            var existing = arch.GetSystem<ILanguageSettingsSystem>();
            if (existing != null)
            {
                return existing;
            }

            var created = new LanguageSettingsSystem();
            arch.RegisterSystem<ILanguageSettingsSystem>(created);
            return created;
        }

        public void SetLanguage(string code)
        {
            var normalized = LanguageId.Normalize(code);
            if (string.Equals(normalized, mCurrent, StringComparison.Ordinal))
            {
                return;
            }

            // 时序（spec §1）：写 PlayerPrefs → 重载表 → 发 Changed → 订阅者自刷。
            mCurrent = normalized;
            mStore.SetString(PreferencesKey, normalized);
            mStore.Save();
            LocalizationCatalog.SetLanguage(normalized);
            Changed?.Invoke(normalized);
        }

        public string Toggle()
        {
            SetLanguage(LanguageId.IsSource(mCurrent) ? LanguageId.En : LanguageId.Zh);
            return mCurrent;
        }

        protected override void OnInit()
        {
        }

        private string LoadOrDefault()
        {
            if (mStore.TryGetString(PreferencesKey, out var stored) && !string.IsNullOrWhiteSpace(stored))
            {
                return LanguageId.Normalize(stored);
            }

            return LanguageId.Zh;
        }
    }
}
