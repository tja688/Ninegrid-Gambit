using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 局内金币获取演出：飞入预制体 → 图标吞噬缩放 → 数值缓冲跳动。
    /// 与 PlayerInfoHudPresenter 协作：增益走本模块缓冲，扣减/首刷可瞬时对齐。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GoldGainFxManagerSingleton : MonoBehaviour
    {
        private const string DefaultIconName = "金币图标";

        private static GoldGainFxManagerSingleton _instance;

        [Header("装配")]
        [Tooltip("飞入用的金币预制体（世界空间 Sprite）。留空则无法播放飞入演出，仍会按节奏跳数值。")]
        [SerializeField] private GameObject coinPrefab;

        [Tooltip("场地内金币图标（吞噬目标）。留空则运行时按名查找「金币图标」。")]
        [SerializeField] private Transform sinkIcon;

        [Tooltip("金币数值 TMP（世界或 UGUI）。留空则优先「玩家信息/金币/金币数值」。")]
        [SerializeField] private TMP_Text goldText;

        [Tooltip("留空则使用 Camera.main；也可手动拖入主相机。")]
        [SerializeField] private Camera targetCamera;

        [Tooltip("生成点世界 Z；正交 2D 默认 0，与场景 Sprite 同平面。")]
        [SerializeField] private float spawnWorldZ;

        [Tooltip("飞入金币 Sorting Layer 名称。")]
        [SerializeField] private string sortingLayerName = "Main";

        [Tooltip("飞入金币 sortingOrder，需高于场地背景。")]
        [SerializeField] private int sortingOrder = 20;

        [Header("飞入节奏")]
        [Tooltip("单枚金币飞向图标的基础时长（秒）。")]
        [SerializeField] private float flyDuration = 0.45f;

        [Tooltip("相邻金币起飞的基础间隔（秒）；数量多时会被窗口上限压缩。")]
        [SerializeField] private float spawnStagger = 0.07f;

        [Tooltip("出生点相对 origin 的随机散布半径（世界单位）。")]
        [SerializeField] private float spawnScatterRadius = 0.55f;

        [Tooltip("单次增益最多生成的可视金币数；超额时把数值摊到这些币上。")]
        [SerializeField] private int maxVisualCoins = 24;

        [Tooltip("整段飞入—吞噬演出的软窗口上限（秒）。超过则明显加速。")]
        [SerializeField] private float presentationWindow = 1.35f;

        [Tooltip("硬上限（秒）；再多金币也不会拖过此时长。")]
        [SerializeField] private float maxPresentationDuration = 2.2f;

        [Header("图标吞噬")]
        [Tooltip("每吞一枚叠加的缩放增量（相对标准大小）。")]
        [SerializeField] private float iconPunchPerCoin = 0.12f;

        [Tooltip("图标缩放上限（相对标准大小）。")]
        [SerializeField] private float iconMaxScale = 1.85f;

        [Tooltip("短时间连续吞噬的叠加窗口（秒）；窗口内未衰减完会越涨越大。")]
        [SerializeField] private float iconPunchStackWindow = 0.55f;

        [Tooltip("图标回缩到标准大小的时长（秒）。")]
        [SerializeField] private float iconSettleDuration = 0.28f;

        [Header("数值缓冲")]
        [Tooltip("每吞一枚后，数值跳变的闪色时长（秒）。")]
        [SerializeField] private float valueFlashDuration = 0.1f;

        private readonly Queue<PendingGain> _queue = new();
        private readonly List<GameObject> _liveCoins = new();

        private bool _hasDisplay;
        private int _displayedGold;
        private int _targetGold;
        private bool _playing;
        private Coroutine _playRoutine;
        private Coroutine _iconSettleRoutine;
        private Vector3 _iconBaseScale = Vector3.one;
        private bool _hasIconBaseScale;
        private float _iconPunch;
        private float _lastAbsorbUnscaledTime = -999f;
        private Tween _goldFlashTween;
        private Color _goldBaseColor = Color.white;
        private bool _hasGoldBaseColor;

        public static GoldGainFxManagerSingleton Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<GoldGainFxManagerSingleton>();
                }

                return _instance;
            }
        }

        public static bool TryGetInstance(out GoldGainFxManagerSingleton manager)
        {
            manager = Instance;
            return manager != null;
        }

        /// <summary>当前 UI 已显示的金币（可能落后于内核目标值）。</summary>
        public int DisplayedGold => _displayedGold;

        /// <summary>缓冲追赶中的目标金币。</summary>
        public int TargetGold => _targetGold;

        /// <summary>是否正在播放飞入/吞噬，或数值尚未追平。</summary>
        public bool IsPresenting => _playing || (_hasDisplay && _displayedGold != _targetGold);

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
            EnsureBindings();
            CaptureIconBaseScale();
        }

        private void OnDestroy()
        {
            KillGoldFlash();
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// 由 HUD Sync 调用：增益走演出缓冲，扣减/静默刷入瞬时对齐。
        /// </summary>
        /// <returns>true 表示本模块已接管金币文本。</returns>
        public bool TryHandleGoldSync(int coreGold, bool animate)
        {
            EnsureBindings();
            coreGold = Mathf.Max(0, coreGold);

            if (!_hasDisplay)
            {
                SnapDisplay(coreGold);
                return goldText != null;
            }

            var presenting = IsPresenting || _queue.Count > 0;

            // 静默 Sync：演出中只抬目标，避免 Snap 抢戏造成瞬间跳变。
            if (!animate)
            {
                if (presenting && coreGold >= _displayedGold)
                {
                    _targetGold = Mathf.Max(_targetGold, coreGold);
                    return true;
                }

                SnapDisplay(coreGold);
                return true;
            }

            if (coreGold <= _displayedGold)
            {
                SnapDisplay(coreGold);
                return true;
            }

            // 已有更高/相同目标在追赶，或队列已覆盖该目标时，只抬目标，避免重复开演。
            if (coreGold <= _targetGold && presenting)
            {
                _targetGold = Mathf.Max(_targetGold, coreGold);
                return true;
            }

            var delta = coreGold - Mathf.Max(_displayedGold, _targetGold);
            if (delta <= 0)
            {
                _targetGold = Mathf.Max(_targetGold, coreGold);
                return true;
            }

            PlayGain(delta, coreGold, ResolveDefaultOriginWorld());
            return true;
        }

        /// <summary>
        /// 播放金币获取演出。
        /// </summary>
        /// <param name="delta">本次增加量（&lt;=0 则只对齐 targetCoins）。</param>
        /// <param name="targetCoins">内核最终金币。</param>
        /// <param name="originWorld">出生世界坐标；null 则用默认（屏幕中心）。</param>
        public void PlayGain(int delta, int targetCoins, Vector3? originWorld = null)
        {
            EnsureBindings();
            targetCoins = Mathf.Max(0, targetCoins);

            if (!_hasDisplay)
            {
                // 首刷：显示值先落在增益前，再演出追到目标。
                var before = Mathf.Max(0, targetCoins - Mathf.Max(0, delta));
                _displayedGold = before;
                _targetGold = before;
                _hasDisplay = true;
                ApplyGoldText(before, flash: false);
            }

            if (delta <= 0)
            {
                SnapDisplay(targetCoins);
                return;
            }

            // 只入队「相对当前目标」的新增量，避免 PresentGold + HUD Sync 双通道重复飞币。
            var raise = targetCoins - _targetGold;
            if (raise <= 0)
            {
                _targetGold = Mathf.Max(_targetGold, targetCoins);
                return;
            }

            _targetGold = targetCoins;
            var origin = originWorld ?? ResolveDefaultOriginWorld();
            origin.z = spawnWorldZ;

            _queue.Enqueue(new PendingGain
            {
                Delta = raise,
                TargetCoins = targetCoins,
                OriginWorld = origin,
            });

            if (!_playing)
            {
                _playRoutine = StartCoroutine(PlayQueueRoutine());
            }
        }

        /// <summary>
        /// DevTest：在屏幕中心生成指定数量金币飞入图标，并缓冲 +count 到显示值。
        /// </summary>
        public void PlayVisualGainAtScreenCenter(int coinCount)
        {
            coinCount = Mathf.Max(0, coinCount);
            if (coinCount <= 0)
            {
                return;
            }

            EnsureBindings();
            if (!_hasDisplay)
            {
                _displayedGold = 0;
                _targetGold = 0;
                _hasDisplay = true;
                ApplyGoldText(0, flash: false);
            }

            var target = _displayedGold + coinCount;
            PlayGain(coinCount, target, GetScreenCenterWorldPosition());
        }

        public void SnapToCore(int coreGold)
        {
            SnapDisplay(Mathf.Max(0, coreGold));
        }

        private IEnumerator PlayQueueRoutine()
        {
            _playing = true;
            while (_queue.Count > 0)
            {
                var batch = _queue.Dequeue();
                yield return PlayBatchRoutine(batch);
            }

            // 兜底：所有币回调后若仍落后目标，瞬时追平。
            if (_displayedGold != _targetGold)
            {
                ApplyGoldText(_targetGold, flash: false);
                _displayedGold = _targetGold;
            }

            _playing = false;
            _playRoutine = null;
            SettleIconToBase();
        }

        private IEnumerator PlayBatchRoutine(PendingGain batch)
        {
            var visualCount = Mathf.Clamp(batch.Delta, 1, Mathf.Max(1, maxVisualCoins));
            var values = DistributeValues(batch.Delta, visualCount);
            ComputeTiming(visualCount, out var stagger, out var fly);

            CaptureIconBaseScale();
            var sink = ResolveSinkWorldPosition();
            var pendingAbsorbs = visualCount;

            for (var i = 0; i < visualCount; i++)
            {
                var coinValue = values[i];
                var spawnPos = batch.OriginWorld + (Vector3)(Random.insideUnitCircle * spawnScatterRadius);
                spawnPos.z = spawnWorldZ;
                SpawnAndFlyCoin(spawnPos, sink, fly, coinValue, stagger * i, () => pendingAbsorbs--);
            }

            var wait = fly + stagger * Mathf.Max(0, visualCount - 1) + 0.08f;
            var elapsed = 0f;
            while (pendingAbsorbs > 0 && elapsed < wait + 0.5f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _targetGold = Mathf.Max(_targetGold, batch.TargetCoins);
        }

        private void SpawnAndFlyCoin(
            Vector3 from,
            Vector3 to,
            float duration,
            int valueOnAbsorb,
            float delay,
            System.Action onDone)
        {
            if (coinPrefab == null)
            {
                DOVirtual.DelayedCall(delay + duration, () =>
                    {
                        OnCoinAbsorbed(valueOnAbsorb);
                        onDone?.Invoke();
                    })
                    .SetUpdate(true)
                    .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
                return;
            }

            var coin = Instantiate(coinPrefab, from, Quaternion.identity);
            _liveCoins.Add(coin);
            ApplySorting(coin);

            var tr = coin.transform;
            tr.localScale = Vector3.one;

            var seq = DOTween.Sequence().SetUpdate(true).SetLink(coin, LinkBehaviour.KillOnDestroy);
            if (delay > 0f)
            {
                seq.AppendInterval(delay);
            }

            seq.Append(tr.DOMove(to, Mathf.Max(0.05f, duration)).SetEase(Ease.InCubic));
            seq.Join(tr.DOScale(0.65f, Mathf.Max(0.05f, duration)).SetEase(Ease.InQuad));
            seq.OnComplete(() =>
            {
                _liveCoins.Remove(coin);
                if (coin != null)
                {
                    Destroy(coin);
                }

                OnCoinAbsorbed(valueOnAbsorb);
                onDone?.Invoke();
            });
        }

        private void OnCoinAbsorbed(int value)
        {
            if (value > 0)
            {
                _displayedGold = Mathf.Min(_targetGold, _displayedGold + value);
                ApplyGoldText(_displayedGold, flash: true);
            }

            PunchIcon();
        }

        private void PunchIcon()
        {
            if (sinkIcon == null)
            {
                return;
            }

            CaptureIconBaseScale();
            var now = Time.unscaledTime;
            if (now - _lastAbsorbUnscaledTime > iconPunchStackWindow)
            {
                _iconPunch = 0f;
            }

            _lastAbsorbUnscaledTime = now;
            _iconPunch = Mathf.Min(iconMaxScale - 1f, _iconPunch + iconPunchPerCoin);
            sinkIcon.DOKill();
            sinkIcon.localScale = _iconBaseScale * (1f + _iconPunch);

            if (_iconSettleRoutine != null)
            {
                StopCoroutine(_iconSettleRoutine);
            }

            _iconSettleRoutine = StartCoroutine(SettleIconAfterWindow());
        }

        private IEnumerator SettleIconAfterWindow()
        {
            yield return new WaitForSecondsRealtime(iconPunchStackWindow);
            SettleIconToBase();
            _iconSettleRoutine = null;
        }

        private void SettleIconToBase()
        {
            if (sinkIcon == null)
            {
                _iconPunch = 0f;
                return;
            }

            CaptureIconBaseScale();
            _iconPunch = 0f;
            sinkIcon.DOKill();
            sinkIcon
                .DOScale(_iconBaseScale, Mathf.Max(0.05f, iconSettleDuration))
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetLink(sinkIcon.gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() =>
                {
                    if (sinkIcon != null)
                    {
                        sinkIcon.localScale = _iconBaseScale;
                    }
                });
        }

        private void SnapDisplay(int gold)
        {
            EnsureBindings();
            _displayedGold = gold;
            _targetGold = gold;
            _hasDisplay = true;
            ApplyGoldText(gold, flash: false);

            if (sinkIcon != null && _hasIconBaseScale)
            {
                sinkIcon.DOKill();
                sinkIcon.localScale = _iconBaseScale;
            }

            _iconPunch = 0f;
        }

        private void ApplyGoldText(int gold, bool flash)
        {
            if (goldText == null)
            {
                return;
            }

            goldText.text = gold.ToString();
            if (!_hasGoldBaseColor)
            {
                _goldBaseColor = goldText.color;
                _hasGoldBaseColor = true;
            }

            if (!flash)
            {
                KillGoldFlash();
                goldText.color = _goldBaseColor;
                return;
            }

            KillGoldFlash();
            var flashColor = Color.Lerp(_goldBaseColor, Color.white, 0.55f);
            goldText.color = flashColor;
            var duration = Mathf.Max(0.02f, valueFlashDuration);
            _goldFlashTween = DOTween
                .To(() => goldText.color, c => goldText.color = c, _goldBaseColor, duration)
                .SetUpdate(true)
                .SetLink(goldText.gameObject, LinkBehaviour.KillOnDestroy);
        }

        private void KillGoldFlash()
        {
            if (_goldFlashTween != null && _goldFlashTween.IsActive())
            {
                _goldFlashTween.Kill();
            }

            _goldFlashTween = null;
        }

        private void ComputeTiming(int visualCount, out float stagger, out float fly)
        {
            fly = Mathf.Max(0.08f, flyDuration);
            stagger = Mathf.Max(0f, spawnStagger);
            if (visualCount <= 1)
            {
                return;
            }

            var softCap = Mathf.Max(fly + 0.05f, presentationWindow);
            var hardCap = Mathf.Max(softCap, maxPresentationDuration);
            var natural = fly + stagger * (visualCount - 1);
            if (natural <= softCap)
            {
                return;
            }

            // 超出软窗口后压缩间隔；逼近硬上限时进一步加速飞入。
            stagger = Mathf.Max(0.01f, (hardCap - fly) / (visualCount - 1));
            var compressed = fly + stagger * (visualCount - 1);
            if (compressed > hardCap)
            {
                var scale = hardCap / compressed;
                fly *= scale;
                stagger *= scale;
            }
        }

        private static int[] DistributeValues(int total, int parts)
        {
            var values = new int[parts];
            if (parts <= 0 || total <= 0)
            {
                return values;
            }

            // total 可能小于 parts（被 maxVisualCoins 截断前已 clamp，但兜底）。
            var coinCount = Mathf.Min(total, parts);
            var baseValue = total / coinCount;
            var remainder = total % coinCount;
            for (var i = 0; i < coinCount; i++)
            {
                values[i] = baseValue + (i < remainder ? 1 : 0);
            }

            return values;
        }

        private void EnsureBindings()
        {
            if (sinkIcon == null)
            {
                sinkIcon = FindPreferredTransform(DefaultIconName, preferredRootName: "玩家信息")
                           ?? FindNamedTransform(DefaultIconName);
            }

            if (goldText == null)
            {
                goldText = FindPreferredTmp("金币数值", preferredRootName: "玩家信息");
                if (goldText == null)
                {
                    var overlay = GameObject.Find("TableNine Text Overlay UI")
                                  ?? GameObject.Find("Text Overlay UI");
                    if (overlay != null)
                    {
                        foreach (var tmp in overlay.GetComponentsInChildren<TMP_Text>(true))
                        {
                            if (tmp != null && tmp.name == "GoldText")
                            {
                                goldText = tmp;
                                break;
                            }
                        }
                    }
                }
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        private static Transform FindPreferredTransform(string objectName, string preferredRootName)
        {
            Transform preferred = null;
            Transform fallback = null;
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t == null || t.name != objectName || !t.gameObject.scene.IsValid())
                {
                    continue;
                }

                fallback ??= t;
                if (IsUnderNamedRoot(t, preferredRootName))
                {
                    preferred = t;
                    break;
                }
            }

            return preferred != null ? preferred : fallback;
        }

        private static TMP_Text FindPreferredTmp(string objectName, string preferredRootName)
        {
            var t = FindPreferredTransform(objectName, preferredRootName);
            return t != null ? t.GetComponent<TMP_Text>() : null;
        }

        private static bool IsUnderNamedRoot(Transform t, string rootName)
        {
            while (t != null)
            {
                if (t.name == rootName)
                {
                    return true;
                }

                t = t.parent;
            }

            return false;
        }

        private void CaptureIconBaseScale()
        {
            if (sinkIcon == null || _hasIconBaseScale)
            {
                return;
            }

            _iconBaseScale = sinkIcon.localScale;
            if (_iconBaseScale == Vector3.zero)
            {
                _iconBaseScale = Vector3.one;
            }

            _hasIconBaseScale = true;
        }

        private Vector3 ResolveSinkWorldPosition()
        {
            if (sinkIcon != null)
            {
                var p = sinkIcon.position;
                p.z = spawnWorldZ;
                return p;
            }

            return GetScreenCenterWorldPosition();
        }

        private Vector3 ResolveDefaultOriginWorld()
        {
            return GetScreenCenterWorldPosition();
        }

        private Vector3 GetScreenCenterWorldPosition()
        {
            return ScreenToWorld(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        }

        private Vector3 ScreenToWorld(Vector2 screenPosition)
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera == null)
            {
                return new Vector3(0f, 0f, spawnWorldZ);
            }

            var depth = targetCamera.orthographic
                ? Mathf.Abs(targetCamera.transform.position.z - spawnWorldZ)
                : targetCamera.nearClipPlane;
            var world = targetCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth));
            world.z = spawnWorldZ;
            return world;
        }

        private void ApplySorting(GameObject coin)
        {
            if (coin == null)
            {
                return;
            }

            var sortingLayerId = SortingLayer.NameToID(sortingLayerName);
            foreach (var renderer in coin.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sortingLayerID = sortingLayerId;
                renderer.sortingOrder = sortingOrder;
            }
        }

        private static Transform FindNamedTransform(string objectName)
        {
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t != null
                    && t.name == objectName
                    && t.gameObject.scene.IsValid())
                {
                    return t;
                }
            }

            return null;
        }

        private struct PendingGain
        {
            public int Delta;
            public int TargetCoins;
            public Vector3 OriginWorld;
        }
    }
}
