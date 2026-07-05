using System;
using MoreMountains.Tools;
using NineGrid.GameFlow;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NineGrid.Battle
{
    /// <summary>
    /// 敌人血量条：驱动 Feel <see cref="MMProgressBar"/> 与战斗 <see cref="Combat.CombatModel"/> 同步。
    /// 位置固定在预制体调好的 Canvas 锚点（与序章一致），不在运行时做世界坐标跟随。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyHpBarController : MonoBehaviour
    {
        [Header("引用（留空自动解析）")]
        [SerializeField] RectTransform barRoot;
        [SerializeField] MMProgressBar progressBar;

        bool _resolved;
        bool _wantVisible;
        bool _suspended;
        bool _resumeAfterSuspend;
        bool _battleBound;
        Vector2 _defaultAnchoredPosition;
        bool _hasDefaultAnchoredPosition;
        Canvas _canvas;
        BattleController _battle;

        public bool IsVisible => _wantVisible && !_suspended;

        void Awake()
        {
            ResolveRefs();
            CaptureDefaultLayout();
            ConfigureProgressBarText();
            HideImmediate();
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            ResolveRefs();
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnbindBattle();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!string.Equals(scene.name, GameFlowScenes.Main, StringComparison.Ordinal))
            {
                return;
            }

            _resolved = false;
            _canvas = null;
            ResolveRefs();
            EnsureCanvasCamera();
            if (IsVisible)
            {
                ApplyLayout();
            }
        }

        /// <summary>战斗入场完成、敌舰已就位后显示。</summary>
        public void Reveal()
        {
            ResolveRefs();
            EnsureCanvasCamera();
            _wantVisible = true;

            if (_suspended)
            {
                _resumeAfterSuspend = true;
                return;
            }

            ApplyVisible(true);
            ApplyLayout();
        }

        /// <summary>新战斗开始前：解绑上一场战斗事件并重置布局。</summary>
        public void PrepareForBattle()
        {
            UnbindBattle();
            _resolved = false;
            _canvas = null;
            EnsureCanvasCamera();
            ApplyLayout();
        }

        /// <summary>战斗模型就绪后同步当前血量（无动画，避免入场闪条）。</summary>
        public void SyncFromCombat(Combat.CombatModel combat)
        {
            if (combat == null)
            {
                return;
            }

            BindBattleIfNeeded();
            ApplyHpInstant(combat.State.EnemyHp, combat.State.EnemyMaxHp);
        }

        /// <summary>立即隐藏血条。</summary>
        public void HideImmediate()
        {
            _wantVisible = false;
            _resumeAfterSuspend = false;
            ApplyVisible(false);
        }

        /// <summary>锻造开启：临时隐藏，保留应显示意图。</summary>
        public void SuspendImmediate()
        {
            if (_suspended)
            {
                return;
            }

            _suspended = true;
            _resumeAfterSuspend = _wantVisible;
            ApplyVisible(false);
        }

        /// <summary>锻造退出：恢复显示。</summary>
        public void ResumeImmediate()
        {
            if (!_suspended)
            {
                return;
            }

            var shouldShow = _resumeAfterSuspend;
            _suspended = false;
            _resumeAfterSuspend = false;

            if (!shouldShow)
            {
                return;
            }

            _wantVisible = true;
            ApplyVisible(true);
            ApplyLayout();
        }

        void BindBattleIfNeeded()
        {
            if (_battleBound)
            {
                return;
            }

            _battle = BattleController.Instance
                ?? FindFirstObjectByType<BattleController>(FindObjectsInactive.Include);
            if (_battle == null)
            {
                return;
            }

            _battle.EnemyHpChanged += OnEnemyHpChanged;
            _battleBound = true;
        }

        void UnbindBattle()
        {
            if (_battle != null)
            {
                _battle.EnemyHpChanged -= OnEnemyHpChanged;
            }

            _battle = null;
            _battleBound = false;
        }

        void OnEnemyHpChanged(int current, int max)
        {
            ApplyHpAnimated(current, max);
            if (current <= 0)
            {
                HideImmediate();
            }
        }

        void ApplyHpInstant(int current, int max)
        {
            if (progressBar == null || max <= 0)
            {
                return;
            }

            progressBar.TextValueMultiplier = max;
            progressBar.SetBar(current, 0, max);
        }

        void ApplyHpAnimated(int current, int max)
        {
            if (progressBar == null || max <= 0)
            {
                return;
            }

            progressBar.TextValueMultiplier = max;
            progressBar.UpdateBar(current, 0, max);
        }

        void ApplyVisible(bool visible)
        {
            ResolveRefs();
            if (barRoot != null)
            {
                barRoot.gameObject.SetActive(visible);
            }
        }

        void ApplyLayout()
        {
            if (barRoot == null)
            {
                return;
            }

            EnsureCanvasCamera();
            barRoot.localRotation = Quaternion.identity;
            barRoot.localScale = Vector3.one;

            if (_hasDefaultAnchoredPosition)
            {
                barRoot.anchoredPosition = _defaultAnchoredPosition;
            }
        }

        void CaptureDefaultLayout()
        {
            if (barRoot == null)
            {
                return;
            }

            _defaultAnchoredPosition = barRoot.anchoredPosition;
            _hasDefaultAnchoredPosition = true;
        }

        void EnsureCanvasCamera()
        {
            if (barRoot == null)
            {
                return;
            }

            if (_canvas == null)
            {
                _canvas = barRoot.GetComponentInParent<Canvas>();
            }

            if (_canvas == null || _canvas.renderMode != RenderMode.ScreenSpaceCamera)
            {
                return;
            }

            var mainCamera = Camera.main;
            if (mainCamera != null && _canvas.worldCamera != mainCamera)
            {
                _canvas.worldCamera = mainCamera;
            }
        }

        void ConfigureProgressBarText()
        {
            if (progressBar == null)
            {
                return;
            }

            progressBar.TextPrefix = string.Empty;
            progressBar.TextSuffix = string.Empty;
            progressBar.DisplayTotal = true;
            progressBar.TotalSeparator = " / ";
            progressBar.TextFormat = "0";
        }

        void ResolveRefs()
        {
            if (_resolved)
            {
                return;
            }

            if (barRoot == null)
            {
                barRoot = transform as RectTransform;
                CaptureDefaultLayout();
            }

            if (progressBar == null)
            {
                progressBar = GetComponentInChildren<MMProgressBar>(true);
            }

            _resolved = true;
        }
    }
}
