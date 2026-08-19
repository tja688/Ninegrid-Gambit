using System;
using System.IO;
using MoreMountains.Tools;
using NineGrid.Presentation.Systems;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NineGrid.Presentation.Editor
{
    /// <summary>
    /// 打包前置强校验：确保任何打包流程（菜单、CLI、CI、BuildPipeline）中，
    /// MMSoundManagerSettingsSO 资源均严格保持出厂预设（Master 100%, BGM 50%, SFX 75%, AutoLoad/AutoSave=false）。
    /// </summary>
    public sealed class PlayerAudioBuildPreprocessor : IPreprocessBuildWithReport
    {
        public const float FactoryMasterVolume = 1f;
        public const bool FactoryMasterOn = true;
        public const float FactoryMusicVolume = 0.5f;
        public const bool FactoryMusicOn = true;
        public const float FactorySfxVolume = 0.75f;
        public const bool FactorySfxOn = true;
        public const bool FactoryAutoLoad = false;
        public const bool FactoryAutoSave = false;

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            EnforceFactoryAudioSettings();
        }

        [MenuItem("NineGrid/Audio/验证并重置出厂音频预设 (Enforce Factory Audio Defaults)")]
        public static void EnforceFactoryAudioSettingsFromMenu()
        {
            var modifiedCount = EnforceFactoryAudioSettings();
            Debug.Log($"[PlayerAudioBuildPreprocessor] 出厂音频预设校验完成（重置/修正了 {modifiedCount} 处资产）。BGM: 50%, SFX: 75%, Master: 100%。");
        }

        /// <summary>
        /// 扫描并强校验所有 MMSoundManagerSettingsSO 资产，若有偏差则修正并保存。
        /// </summary>
        /// <returns>修正的资产数量。</returns>
        public static int EnforceFactoryAudioSettings()
        {
            var modifiedCount = 0;
            var guids = AssetDatabase.FindAssets("t:MMSoundManagerSettingsSO");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                var settingsSo = AssetDatabase.LoadAssetAtPath<MMSoundManagerSettingsSO>(path);
                if (settingsSo == null || settingsSo.Settings == null)
                {
                    continue;
                }

                var settings = settingsSo.Settings;
                var changed = false;

                if (!Mathf.Approximately(settings.MasterVolume, FactoryMasterVolume))
                {
                    settings.MasterVolume = FactoryMasterVolume;
                    changed = true;
                }

                if (settings.MasterOn != FactoryMasterOn)
                {
                    settings.MasterOn = FactoryMasterOn;
                    changed = true;
                }

                if (!Mathf.Approximately(settings.MusicVolume, FactoryMusicVolume))
                {
                    settings.MusicVolume = FactoryMusicVolume;
                    changed = true;
                }

                if (settings.MusicOn != FactoryMusicOn)
                {
                    settings.MusicOn = FactoryMusicOn;
                    changed = true;
                }

                if (!Mathf.Approximately(settings.SfxVolume, FactorySfxVolume))
                {
                    settings.SfxVolume = FactorySfxVolume;
                    changed = true;
                }

                if (settings.SfxOn != FactorySfxOn)
                {
                    settings.SfxOn = FactorySfxOn;
                    changed = true;
                }

                if (settings.AutoLoad != FactoryAutoLoad)
                {
                    settings.AutoLoad = FactoryAutoLoad;
                    changed = true;
                }

                if (settings.AutoSave != FactoryAutoSave)
                {
                    settings.AutoSave = FactoryAutoSave;
                    changed = true;
                }

                if (changed)
                {
                    EditorUtility.SetDirty(settingsSo);
                    modifiedCount++;
                    Debug.Log($"[PlayerAudioBuildPreprocessor] 已修正音频配置资产至出厂预设：{path}");
                }
            }

            if (modifiedCount > 0)
            {
                AssetDatabase.SaveAssets();
            }

            return modifiedCount;
        }
    }
}
