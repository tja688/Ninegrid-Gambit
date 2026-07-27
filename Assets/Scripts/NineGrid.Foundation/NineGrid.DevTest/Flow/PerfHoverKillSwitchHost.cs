#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Text;
using NineGrid.Presentation.Diagnostics;
using UnityEngine;

namespace NineGrid.DevTest.Flow
{
    /// <summary>
    /// 热区掉帧 A/B 隔离热键宿主：自动挂载，无需改场景。
    /// F6 下一模式 / F8 上一模式 / F7 采 3s FPS / F5 帮助。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PerfHoverKillSwitchHost : MonoBehaviour
    {
        private const float SampleSeconds = 3f;
        private const int RingSize = 240;

        private static PerfHoverKillSwitchHost sInstance;

        private readonly float[] _dtRing = new float[RingSize];
        private int _dtWrite;
        private int _dtCount;

        private bool _sampling;
        private float _sampleElapsed;
        private int _sampleFrames;
        private float _sampleDtSum;
        private float _sampleDtMax;
        private readonly float[] _sampleDts = new float[4096];

        private float _overlayUntil = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (sInstance != null)
            {
                return;
            }

            var existing = Object.FindFirstObjectByType<PerfHoverKillSwitchHost>();
            if (existing != null)
            {
                sInstance = existing;
                return;
            }

            var go = new GameObject("[Dev] PerfHoverKillSwitchHost");
            DontDestroyOnLoad(go);
            sInstance = go.AddComponent<PerfHoverKillSwitchHost>();
            Debug.Log(
                "[PerfHoverKill] host ready | F5=帮助 F6=下一模式 F8=上一模式 F7=采3sFPS | "
                + "模式互斥：0基线 A光标 B全Collider C禁OnMouseHover D无Tween E槽+Relic");
        }

        private GUIStyle _overlayStyle;

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            PushDt(dt);
            PerfHoverKillSwitch.TickColliderPolicy();

            if (_sampling)
            {
                AccumulateSample(dt);
            }

            if (Input.GetKeyDown(KeyCode.F5))
            {
                LogHelpAndStatus();
                FlashOverlay();
            }

            if (Input.GetKeyDown(KeyCode.F6))
            {
                CycleMode(+1);
            }

            if (Input.GetKeyDown(KeyCode.F8))
            {
                CycleMode(-1);
            }

            if (Input.GetKeyDown(KeyCode.F7))
            {
                BeginSample(manual: true);
            }
        }

        private void OnGUI()
        {
            if (Time.unscaledTime > _overlayUntil && !_sampling
                && PerfHoverKillSwitch.ActiveMode == PerfHoverKillMode.Off)
            {
                return;
            }

            var liveFps = EstimateLiveFps(out var liveMin);
            var mode = PerfHoverKillSwitch.ActiveMode;
            var line1 = $"[PerfHover] {PerfHoverKillSwitch.Describe(mode)}";
            var line2 = $"FPS≈{liveFps:0} (min~{liveMin:0})  enabledCol={PerfHoverKillSwitch.CountEnabledColliders2D()}";
            var line3 = _sampling
                ? $"采样中… {_sampleElapsed:0.0}/{SampleSeconds:0}s  F5帮助 F6/F8切模式 F7重采"
                : "F5帮助  F6下一模式  F8上一模式  F7采3sFPS";

            if (_overlayStyle == null)
            {
                _overlayStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 14,
                    normal = { textColor = Color.white },
                };
            }

            var text = line1 + "\n" + line2 + "\n" + line3;
            GUI.Box(new Rect(12, 12, 620, 72), text, _overlayStyle);
        }

        private void CycleMode(int delta)
        {
            var values = (PerfHoverKillMode[])System.Enum.GetValues(typeof(PerfHoverKillMode));
            var index = 0;
            for (var i = 0; i < values.Length; i++)
            {
                if (values[i] == PerfHoverKillSwitch.ActiveMode)
                {
                    index = i;
                    break;
                }
            }

            index = (index + delta + values.Length) % values.Length;
            ApplyMode(values[index]);
        }

        private void ApplyMode(PerfHoverKillMode mode)
        {
            PerfHoverKillSwitch.SetMode(mode, out var detail);
            var liveFps = EstimateLiveFps(out var liveMin);
            Debug.Log(
                $"[PerfHoverKill] APPLY mode={mode} | {PerfHoverKillSwitch.Describe(mode)} | "
                + $"{detail} | liveFps≈{liveFps:0.#} min≈{liveMin:0.#} "
                + $"enabledCol={PerfHoverKillSwitch.CountEnabledColliders2D()}");
            FlashOverlay();
            BeginSample(manual: false);
        }

        private void BeginSample(bool manual)
        {
            _sampling = true;
            _sampleElapsed = 0f;
            _sampleFrames = 0;
            _sampleDtSum = 0f;
            _sampleDtMax = 0f;
            FlashOverlay();
            Debug.Log(
                $"[PerfHoverKill] SAMPLE begin mode={PerfHoverKillSwitch.ActiveMode} "
                + $"({PerfHoverKillSwitch.Describe(PerfHoverKillSwitch.ActiveMode)}) "
                + $"duration={SampleSeconds:0.#}s manual={manual} — 请在热区快扫鼠标");
        }

        private void AccumulateSample(float dt)
        {
            if (dt <= 0f)
            {
                return;
            }

            _sampleElapsed += dt;
            if (_sampleFrames < _sampleDts.Length)
            {
                _sampleDts[_sampleFrames] = dt;
            }

            _sampleFrames++;
            _sampleDtSum += dt;
            if (dt > _sampleDtMax)
            {
                _sampleDtMax = dt;
            }

            if (_sampleElapsed < SampleSeconds)
            {
                return;
            }

            FinishSample();
        }

        private void FinishSample()
        {
            _sampling = false;
            if (_sampleFrames <= 0 || _sampleDtSum <= 0f)
            {
                Debug.LogWarning("[PerfHoverKill] SAMPLE abort: no frames");
                return;
            }

            var avgFps = _sampleFrames / _sampleDtSum;
            var minFps = _sampleDtMax > 0f ? 1f / _sampleDtMax : 0f;

            // 近似 1% low：按 dt 降序取最慢 1% 帧的平均 FPS
            var n = Mathf.Min(_sampleFrames, _sampleDts.Length);
            System.Array.Sort(_sampleDts, 0, n);
            // 升序后最慢在末尾
            var worstCount = Mathf.Max(1, n / 100);
            var worstSum = 0f;
            for (var i = n - worstCount; i < n; i++)
            {
                worstSum += _sampleDts[i];
            }

            var p1Fps = worstCount / worstSum;

            var sb = new StringBuilder(192);
            sb.Append("[PerfHoverKill] SAMPLE end mode=").Append(PerfHoverKillSwitch.ActiveMode);
            sb.Append(" | ").Append(PerfHoverKillSwitch.Describe(PerfHoverKillSwitch.ActiveMode));
            sb.Append(" | frames=").Append(_sampleFrames);
            sb.Append(" avgFps=").Append(avgFps.ToString("0.0"));
            sb.Append(" minFps=").Append(minFps.ToString("0.0"));
            sb.Append(" p1lowFps=").Append(p1Fps.ToString("0.0"));
            sb.Append(" maxDtMs=").Append((_sampleDtMax * 1000f).ToString("0.0"));
            sb.Append(" enabledCol=").Append(PerfHoverKillSwitch.CountEnabledColliders2D());
            Debug.Log(sb.ToString());
            FlashOverlay();
        }

        private void LogHelpAndStatus()
        {
            var liveFps = EstimateLiveFps(out var liveMin);
            Debug.Log(
                "[PerfHoverKill] HELP\n"
                + "  F6 = 下一模式（互斥）  F8 = 上一模式  F7 = 立刻采 3s FPS  F5 = 本帮助\n"
                + "  0 Off 基线\n"
                + "  A SoftCursorAuto — 若掉帧消失 → 软件光标主因/放大器\n"
                + "  B DisableAllColliders — 若消失 → SendMouseEvents/命中面主因\n"
                + "  C SuppressGroundOnMouseHover — 若消失 → OnMouseEnter/Exit 回调主因\n"
                + "  D InstantHoverNoTween — 若消失 → DOTween hover 风暴主因\n"
                + "  E DisableSlotAndRelicColliders — 若消失 → 主菜单空槽+Relic 盒主因\n"
                + "  建议：每模式切完后在热区快扫；等 SAMPLE end 日志再切下一档。\n"
                + $"  当前: {PerfHoverKillSwitch.Describe(PerfHoverKillSwitch.ActiveMode)} "
                + $"FPS≈{liveFps:0.#} min≈{liveMin:0.#} "
                + $"enabledCol={PerfHoverKillSwitch.CountEnabledColliders2D()} sampling={_sampling}");
        }

        private void PushDt(float dt)
        {
            _dtRing[_dtWrite] = dt;
            _dtWrite = (_dtWrite + 1) % RingSize;
            if (_dtCount < RingSize)
            {
                _dtCount++;
            }
        }

        private float EstimateLiveFps(out float minFps)
        {
            minFps = 0f;
            if (_dtCount <= 0)
            {
                return 0f;
            }

            var sum = 0f;
            var maxDt = 0f;
            for (var i = 0; i < _dtCount; i++)
            {
                var dt = _dtRing[i];
                sum += dt;
                if (dt > maxDt)
                {
                    maxDt = dt;
                }
            }

            minFps = maxDt > 0f ? 1f / maxDt : 0f;
            return _dtCount / sum;
        }

        private void FlashOverlay()
        {
            _overlayUntil = Time.unscaledTime + 8f;
        }
    }
}

#endif
