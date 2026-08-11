using UnityEngine;
using UnityEngine.InputSystem;

namespace NineGrid.VisualFxLab
{
    /// <summary>
    /// 画面实验室唯一控制入口（仅 Editor / Development Build 自举，正式包不存在）。
    ///
    /// 热键：
    ///   F9  —— 一键装配 / 一键还原（关掉即回到原汁原味的画面）
    ///   F10 —— 循环全屏 Look：原版 → CRT 显像管 → 烛光酒馆 → 辉光绽放 → 深渊地牢 → 老式胶片
    ///   F11 —— 循环全屏 Look 强度：100% → 70% → 40%
    ///   F6  —— 机关卡光环环绕 开/关
    ///   F7  —— 氛围浮尘与余烬 开/关
    ///   F8  —— 卡面流光 开/关
    ///
    /// F1 已被伤害日志、F12 已被作弊面板占用，本实验室只用 F6–F11。
    /// </summary>
    public sealed class FxLabHotkeyHost : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindFirstObjectByType<FxLabHotkeyHost>() != null)
            {
                return;
            }

            var go = new GameObject("FxLab_ControlHost");
            DontDestroyOnLoad(go);
            go.AddComponent<FxLabHotkeyHost>();
        }
#endif

        static readonly float[] StrengthSteps = { 1f, 0.7f, 0.4f };

        FxLabTrapHalo _halo;
        FxLabAmbientDust _dust;
        FxLabCardFoil _foil;
        float _bootAt;
        GUIStyle _boxStyle;
        GUIStyle _labelStyle;

        void Awake()
        {
            _bootAt = Time.unscaledTime;
            _halo = gameObject.AddComponent<FxLabTrapHalo>();
            _dust = gameObject.AddComponent<FxLabAmbientDust>();
            _foil = gameObject.AddComponent<FxLabCardFoil>();
            _halo.enabled = false;
            _dust.enabled = false;
            _foil.enabled = false;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.f9Key.wasPressedThisFrame)
            {
                ToggleInstalled();
            }

            if (!FxLabState.Installed)
            {
                return;
            }

            if (keyboard.f10Key.wasPressedThisFrame)
            {
                CycleLook();
            }

            if (keyboard.f11Key.wasPressedThisFrame)
            {
                CycleStrength();
            }

            if (keyboard.f6Key.wasPressedThisFrame)
            {
                FxLabState.TrapHalo = !FxLabState.TrapHalo;
                SyncModules();
            }

            if (keyboard.f7Key.wasPressedThisFrame)
            {
                FxLabState.AmbientDust = !FxLabState.AmbientDust;
                SyncModules();
            }

            if (keyboard.f8Key.wasPressedThisFrame)
            {
                FxLabState.CardFoil = !FxLabState.CardFoil;
                SyncModules();
            }
        }

        void ToggleInstalled()
        {
            FxLabState.Installed = !FxLabState.Installed;
            if (FxLabState.Installed && FxLabState.Look == FxLabLook.None)
            {
                FxLabState.Look = FxLabLook.CrtDeluxe;
            }

            SyncModules();
        }

        void SyncModules()
        {
            bool on = FxLabState.Installed;
            _halo.enabled = on && FxLabState.TrapHalo;
            _dust.enabled = on && FxLabState.AmbientDust;
            _foil.enabled = on && FxLabState.CardFoil;
        }

        static void CycleLook()
        {
            int next = ((int)FxLabState.Look + 1) % 6;
            FxLabState.Look = (FxLabLook)next;
        }

        static void CycleStrength()
        {
            float current = FxLabState.LookStrength;
            int index = 0;
            for (int i = 0; i < StrengthSteps.Length; i++)
            {
                if (Mathf.Approximately(StrengthSteps[i], current))
                {
                    index = i;
                    break;
                }
            }

            FxLabState.LookStrength = StrengthSteps[(index + 1) % StrengthSteps.Length];
        }

        void OnGUI()
        {
            EnsureStyles();

            if (!FxLabState.Installed)
            {
                // 开场提示 12 秒，之后彻底安静
                if (Time.unscaledTime - _bootAt < 12f)
                {
                    GUI.Box(new Rect(Screen.width - 336f, 8f, 328f, 26f),
                        "F9 开启画面实验室（视觉效果试装，可一键还原）", _boxStyle);
                }

                return;
            }

            string featureLine = FxLabState.FeatureLoaded
                ? $"全屏 (F10): {FxLabState.LookDisplayName(FxLabState.Look)}   强度 (F11): {Mathf.RoundToInt(FxLabState.LookStrength * 100f)}%"
                : "全屏 Look 未加载：请经菜单 NineGrid/画面实验室 安装渲染特性";

            string moduleLine =
                $"光环 (F6): {OnOff(FxLabState.TrapHalo)}   粒子 (F7): {OnOff(FxLabState.AmbientDust)}   流光 (F8): {OnOff(FxLabState.CardFoil)}";

            var rect = new Rect(Screen.width - 356f, 8f, 348f, 66f);
            GUI.Box(rect, string.Empty, _boxStyle);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 4f, rect.width - 20f, 20f),
                "画面实验室 [已装配]   F9 一键还原", _labelStyle);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 24f, rect.width - 20f, 20f),
                featureLine, _labelStyle);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 44f, rect.width - 20f, 20f),
                moduleLine, _labelStyle);
        }

        void EnsureStyles()
        {
            if (_boxStyle != null)
            {
                return;
            }

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
            };
            _boxStyle.normal.textColor = new Color(1f, 0.92f, 0.7f);

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
            };
            _labelStyle.normal.textColor = new Color(1f, 0.92f, 0.7f);
        }

        static string OnOff(bool value)
        {
            return value ? "开" : "关";
        }
    }
}
