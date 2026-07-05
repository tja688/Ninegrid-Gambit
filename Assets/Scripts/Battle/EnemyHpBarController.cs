using System;
using MoreMountains.Tools;
using NineGrid.GameFlow;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NineGrid.Battle
{
    /// <summary>
    /// 敌人血量条：驱动 Feel <see cref="MMProgressBar"/> 与战斗 <see cref="Combat.CombatModel"/> 同步，
    /// 跟随敌舰位移，锻造时挂起，敌舰被击败后隐藏。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyHpBarController : MonoBehaviour
    {
        const float StayPoseEpsilon = 0.15f;

        [Header("引用（留空自动解析）")]
        [SerializeField] RectTransform barRoot;
        [SerializeField] MMProgressBar progressBar;
        [SerializeField] Transform enemy;

        [Header("跟随")]
        [Tooltip("敌舰尚未就位时使用的世界偏移；就位后会按预制体锚点重新校准。")]
        [SerializeField] Vector3 fallbackWorldOffset = new Vector3(0f, 1.5f, 0f);

        bool _resolved;
        bool _wantVisible;
        bool _suspended;
        bool _resumeAfterSuspend;
        bool _battleBound;
        Vector3 _worldOffset;
        bool _offsetCached;
        Vector2 _defaultAnchoredPosition;
        bool _hasDefaultAnchoredPosition;
        Canvas _canvas;
        BattleController _battle;

        public bool IsVisible => _wantVisible && !_suspended;

        void Awake()
        {
            ResolveRefs();
            CaptureDefaultAnchoredPosition();
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
            UnbindPerformance();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!string.Equals(scene.name, GameFlowScenes.Main, StringComparison.Ordinal))
            {
                return;
            }

            _resolved = false;
            enemy = null;
            _offsetCached = false;
            _canvas = null;
            UnbindPerformance();
            ResolveRefs();
            EnsureCanvasCamera();
            if (IsVisible)
            {
                CacheWorldOffset();
            }
        }

        void LateUpdate()
        {
            if (!IsVisible)
            {
                return;
            }

            SyncWorldPosition();
        }

        /// <summary>敌舰驶入 stay 后显示（战斗入场演出末段）。</summary>
        public void Reveal()
        {
            ResolveRefs();
            CacheWorldOffset();
            _wantVisible = true;

            if (_suspended)
            {
                _resumeAfterSuspend = true;
                return;
            }

            ApplyVisible(true);
            SyncWorldPosition();
        }

        /// <summary>新战斗开始前：解绑上一场战斗事件。</summary>
        public void PrepareForBattle()
        {
            UnbindBattle();
            _offsetCached = false;
            EnsureCanvasCamera();
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
            _offsetCached = false;
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
            SyncWorldPosition();
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

        void BindPerformance()
        {
            var performance = ProloguePerformance.Instance
                ?? FindFirstObjectByType<ProloguePerformance>(FindObjectsInactive.Include);
            if (performance == null)
            {
                return;
            }

            performance.EnemySailedIn -= OnEnemyArrivedAtStay;
            performance.EnemySailedIn += OnEnemyArrivedAtStay;
        }

        void UnbindPerformance()
        {
            var performance = ProloguePerformance.Instance
                ?? FindFirstObjectByType<ProloguePerformance>(FindObjectsInactive.Include);
            if (performance != null)
            {
                performance.EnemySailedIn -= OnEnemyArrivedAtStay;
            }
        }

        void OnEnemyArrivedAtStay()
        {
            Reveal();
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

        void SyncWorldPosition()
        {
            if (!_offsetCached)
            {
                CacheWorldOffset();
            }

            if (barRoot == null || enemy == null || !_offsetCached)
            {
                return;
            }

            EnsureCanvasCamera();
            var targetWorld = enemy.position + _worldOffset;

            if (_canvas != null
                && _canvas.renderMode == RenderMode.ScreenSpaceCamera
                && _canvas.worldCamera != null)
            {
                var canvasRect = _canvas.transform as RectTransform;
                var screenPoint = _canvas.worldCamera.WorldToScreenPoint(targetWorld);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        canvasRect,
                        screenPoint,
                        _canvas.worldCamera,
                        out var localPoint))
                {
                    ResetBarVisualTransform();
                    barRoot.anchoredPosition = localPoint;
                }

                return;
            }

            ResetBarVisualTransform();
            barRoot.position = targetWorld;
        }

        void CacheWorldOffset()
        {
            ResolveRefs();
            if (enemy == null)
            {
                return;
            }

            if (TryCalibrateOffsetFromStayPose())
            {
                return;
            }

            _worldOffset = fallbackWorldOffset;
            _offsetCached = true;
        }

        bool TryCalibrateOffsetFromStayPose()
        {
            if (barRoot == null || enemy == null || !IsEnemyAtStayPose())
            {
                return false;
            }

            EnsureCanvasCamera();
            ResetBarVisualTransform();

            if (_hasDefaultAnchoredPosition)
            {
                barRoot.anchoredPosition = _defaultAnchoredPosition;
            }

            _worldOffset = barRoot.position - enemy.position;
            _offsetCached = true;
            return true;
        }

        bool IsEnemyAtStayPose()
        {
            if (enemy == null)
            {
                return false;
            }

            var stay = GameObject.Find("enemy stay");
            return stay != null
                && Vector3.Distance(enemy.position, stay.transform.position) <= StayPoseEpsilon;
        }

        void CaptureDefaultAnchoredPosition()
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

        void ResetBarVisualTransform()
        {
            if (barRoot == null)
            {
                return;
            }

            barRoot.localRotation = Quaternion.identity;
            barRoot.localScale = Vector3.one;
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
                CaptureDefaultAnchoredPosition();
            }

            if (progressBar == null)
            {
                progressBar = GetComponentInChildren<MMProgressBar>(true);
            }

            if (enemy == null)
            {
                var enemyGo = GameObject.Find("enemy");
                enemy = enemyGo != null ? enemyGo.transform : null;
            }

            BindPerformance();
            _resolved = true;
        }
    }
}
