using System.Collections;
using DG.Tweening;
using NineGrid.Core;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using TMPro;
using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 局内「玩家信息」HUD：血槽血管 + 当前/最大血量、基础护甲、金币。
    /// 血槽长度随 MaxHp 相对基础上限伸长（每点 +0.019），总宽封顶 3.2；
    /// 同时常驻显示当前血量与满血上限（图标+数值）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInfoHudPresenter : MonoBehaviour
    {
        private const string PlayerInfoRootName = "玩家信息";
        private const string BloodBarName = "血条";
        private const string BloodSlotName = "血槽";
        private const string BloodFillName = "真实血量条";
        private const string CurrentHpName = "血量数值（当前）";
        private const string CurrentHpIconName = "血量图标（当前）";
        private const string MaxHpName = "血量数值（满血）";
        private const string MaxHpIconName = "血量图标（满血）";
        private const string ArmorRootName = "基础护甲";
        private const string ArmorValueName = "护甲数值";
        private const string GoldValueName = "金币数值";
        private const string GoldIconName = "金币图标";

        // 场景默认：血槽 size.x=1.8、血条≈1.48；布局可伸至 3.2 / 2.9（共增约 1.4）。
        // 旧参 0.05×封顶 5.5 约 74 点 MaxHp 增量触顶；新增长位 1.4 → 1.4/74≈0.019，触顶 MaxHp≈84。
        private const float SlotWidthPerMaxHp = 0.019f;
        private const float MaxVesselWidth = 3.2f;
        private const float DefaultBaseMaxHp = 10f;
        private const float HpFillAnimDuration = 0.38f;
        private const float SlotGrowDuration = 0.45f;
        private const float NumberFlashDuration = 0.2f;
        private const float MaxHpFlashHalfPeriod = 0.12f;
        private const int MaxHpFlashPulses = 2;

        private static readonly Color HpNormalColor = new Color32(0xFF, 0xFF, 0xFF, 0xBF);
        private static readonly Color HpFullColor = new Color32(0xC8, 0xFF, 0xD0, 0xFF);
        private static readonly Color HpDamageFlash = new Color32(0xFF, 0x6A, 0x6A, 0xFF);
        private static readonly Color HpHealFlash = new Color32(0x7C, 0xFF, 0x9A, 0xFF);
        private static readonly Color FillNormalColor = Color.white;
        private static readonly Color FillDamageColor = new Color32(0xFF, 0x8A, 0x8A, 0xFF);
        private static readonly Color FillHealColor = new Color32(0xB8, 0xFF, 0xC4, 0xFF);

        private static PlayerInfoHudPresenter _instance;

        [Header("根与文本")]
        [Tooltip("玩家信息根；留空则按名查找「玩家信息」。")]
        [SerializeField] private Transform playerInfoRoot;

        [Tooltip("当前血量 TMP（世界空间 TextMeshPro）。")]
        [SerializeField] private TMP_Text currentHpText;

        [Tooltip("当前血量图标 SpriteRenderer。")]
        [SerializeField] private SpriteRenderer currentHpIcon;

        [Tooltip("最大血量 TMP。")]
        [SerializeField] private TMP_Text maxHpText;

        [Tooltip("满血图标 SpriteRenderer。")]
        [SerializeField] private SpriteRenderer maxHpIcon;

        [Tooltip("基础护甲（有效护甲，ADR-0028）TMP；与玩家卡面显示的真实护甲（当前护甲）区分。")]
        [SerializeField] private TMP_Text armorText;

        [Tooltip("金币 TMP；增益数字由本 HUD 独占（#199 时间窗），不由 VFX 直写。")]
        [SerializeField] private TMP_Text goldText;

        [Tooltip("金币图标（吞噬终点/缩放复位目标）；留空则按「玩家信息」根下子节点查找。")]
        [SerializeField] private Transform goldIcon;

        [Header("血槽血管")]
        [Tooltip("血槽（血管）SpriteRenderer；Sliced，左 pivot。")]
        [SerializeField] private SpriteRenderer bloodSlot;

        [Tooltip("真实血量条 SpriteRenderer；Sliced，左 pivot。")]
        [SerializeField] private SpriteRenderer bloodFill;

        [Tooltip("血条根（抖动/脉搏挂点）。")]
        [SerializeField] private Transform bloodBarRoot;

        [Tooltip("编辑器现状对应的基础血量上限；血槽现状宽度即此上限下的基础长度。")]
        [SerializeField] private float baseMaxHp = DefaultBaseMaxHp;

        private readonly StatSlot _armor = new();
        private readonly StatSlot _gold = new();

        private int _displayedGold;
        private int _targetGold;
        private bool _hasGoldDisplay;
        private bool _goldWindowActive;
        private Coroutine _goldWindowRoutine;
        private int _goldWindowAmountBefore;
        private float _goldWindowFirstDelay;
        private float _goldWindowLastDelay;
        private float _goldWindowElapsed;
        private Tween _goldFlashTween;
        private Color _goldBaseColor = Color.white;
        private bool _hasGoldBaseColor;

        private bool _hasSnapshot;
        private bool _wasAtMaxHp;
        private bool _capturedVesselBase;
        private float _baseSlotWidth;
        private float _baseFillWidth;
        private float _fillLocalX;
        private float _displayedHp;
        private float _animatedFillWidth;
        private float _animatedSlotWidth;
        private int _coreHp;
        private int _coreMaxHp;
        private Vector3 _bloodBarBasePos;
        private Vector3 _bloodBarBaseScale = Vector3.one;
        private bool _hasBloodBarBasePose;
        private Tween _fillTween;
        private Tween _slotTween;
        private Tween _hpNumberTween;
        private Tween _fillColorTween;
        private Tween _hpTextColorTween;
        private Tween _barShakeTween;
        private Coroutine _maxHpPulseRoutine;

        public static PlayerInfoHudPresenter Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<PlayerInfoHudPresenter>();
                    if (_instance == null)
                    {
                        var go = new GameObject(nameof(PlayerInfoHudPresenter));
                        _instance = go.AddComponent<PlayerInfoHudPresenter>();
                    }
                }

                return _instance;
            }
        }

        public static PlayerInfoHudPresenter TryGetInstance()
        {
            if (_instance != null)
            {
                return _instance;
            }

            _instance = FindFirstObjectByType<PlayerInfoHudPresenter>();
            return _instance;
        }

        /// <summary>当前 HUD 已显示的金币（可能落后于内核目标）。</summary>
        public int DisplayedGold => _displayedGold;

        /// <summary>数字窗追赶中的目标金币。</summary>
        public int TargetGold => _targetGold;

        /// <summary>是否正在按首达→末达窗口推进金币数字。</summary>
        public bool IsGoldWindowActive => _goldWindowActive;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
            EnsureBindings();
            CaptureVesselBaseIfNeeded();
        }

        private void OnDestroy()
        {
            KillHpTweens();
            StopGoldWindow(snapToTarget: false);
            KillGoldFlash();
            if (_maxHpPulseRoutine != null)
            {
                StopCoroutine(_maxHpPulseRoutine);
                _maxHpPulseRoutine = null;
            }

            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// 从内核刷新玩家信息；仅开局 / 作弊等白名单路径使用。
        /// 战中血甲金改由结算指令经 <see cref="NineGrid.Flow.Presentation.PlayerInfoHudBeatHandler"/> /
        /// <see cref="NineGrid.Flow.Presentation.GoldGainBeatHandler"/> 在表演锚点驱动。
        /// </summary>
        public void SyncFromCore(bool animate = true)
        {
            EnsureBindings();
            CaptureVesselBaseIfNeeded();

            if (!TryReadAvatarHud(out var hp, out var maxHp, out var armor, out var gold))
            {
                return;
            }

            var firstPaint = !_hasSnapshot;
            var shouldAnimate = animate && !firstPaint;
            _hasSnapshot = true;

            ApplyHpVessel(hp, maxHp, shouldAnimate, firstPaint);
            ApplyIntStat(_armor, armorText, armor, armor.ToString(), shouldAnimate);
            ApplyGold(gold, shouldAnimate);
        }

        /// <summary>指令驱动：写入当前血量（保留已显示的 MaxHp）。</summary>
        public void ApplyHp(int hp, bool animate = true)
        {
            EnsureBindings();
            CaptureVesselBaseIfNeeded();
            var maxHp = _hasSnapshot && _coreMaxHp > 0 ? _coreMaxHp : Mathf.Max(1, hp);
            var firstPaint = !_hasSnapshot;
            var shouldAnimate = animate && !firstPaint;
            _hasSnapshot = true;
            ApplyHpVessel(hp, maxHp, shouldAnimate, firstPaint);
        }

        /// <summary>指令驱动：写入最大血量（保留已显示的当前血）。</summary>
        public void ApplyMaxHp(int maxHp, bool animate = true)
        {
            EnsureBindings();
            CaptureVesselBaseIfNeeded();
            var hp = _hasSnapshot ? _coreHp : Mathf.Max(0, maxHp);
            var firstPaint = !_hasSnapshot;
            var shouldAnimate = animate && !firstPaint;
            _hasSnapshot = true;
            ApplyHpVessel(hp, Mathf.Max(1, maxHp), shouldAnimate, firstPaint);
        }

        /// <summary>
        /// 指令驱动：同时写入当前血与上限（MaxHp 耦合加血的原子刷新，避免只长槽不涨血）。
        /// </summary>
        public void ApplyHpAndMaxHp(int hp, int maxHp, bool animate = true)
        {
            EnsureBindings();
            CaptureVesselBaseIfNeeded();
            var firstPaint = !_hasSnapshot;
            var shouldAnimate = animate && !firstPaint;
            _hasSnapshot = true;
            ApplyHpVessel(Mathf.Max(0, hp), Mathf.Max(1, maxHp), shouldAnimate, firstPaint);
        }

        /// <summary>指令驱动：写入有效护甲。</summary>
        public void ApplyArmor(int armor, bool animate = true)
        {
            EnsureBindings();
            var firstPaint = !_hasSnapshot;
            var shouldAnimate = animate && !firstPaint;
            _hasSnapshot = true;
            ApplyIntStat(_armor, armorText, Mathf.Max(0, armor), Mathf.Max(0, armor).ToString(), shouldAnimate);
        }

        /// <summary>扣金 / 失败收敛 / 首刷：瞬时对齐金币数字与图标缩放。</summary>
        public void SnapGold(int amountAfter)
        {
            EnsureBindings();
            amountAfter = Mathf.Max(0, amountAfter);
            StopGoldWindow(snapToTarget: false);
            _displayedGold = amountAfter;
            _targetGold = amountAfter;
            _hasGoldDisplay = true;
            _gold.Value = amountAfter;
            _gold.HasValue = true;
            ApplyGoldText(amountAfter, flash: false);
            GoldHudDomainHost.Instance?.SnapIconToBase();
        }

        /// <summary>
        /// 按 VFX 批次首达→末达窗口推进金币数字；末端精确收敛到 amountAfter。
        /// 无效计划时立即 Snap。并行批次合并：抬目标并扩展末达，不中断进行中的单调整数曲线。
        /// </summary>
        public void PresentGoldGainWindow(
            int delta,
            int amountAfter,
            NineGrid.Presentation.Systems.VfxPresentationPlan plan)
        {
            EnsureBindings();
            amountAfter = Mathf.Max(0, amountAfter);
            delta = Mathf.Max(0, delta);
            if (!plan.IsValid || !GoldHudNumberWindow.IsValidWindow(plan.FirstArrivalDelay, plan.LastArrivalDelay))
            {
                SnapGold(amountAfter);
                return;
            }

            if (!_hasGoldDisplay)
            {
                _displayedGold = Mathf.Max(0, amountAfter - delta);
                _targetGold = _displayedGold;
                _hasGoldDisplay = true;
                ApplyGoldText(_displayedGold, flash: false);
            }

            _targetGold = Mathf.Max(_targetGold, amountAfter);
            _gold.Value = _targetGold;
            _gold.HasValue = true;

            if (_goldWindowActive)
            {
                // 相对当前已过时间合并新批次：末达取更晚者；若尚未首达，首达取更早者。
                _goldWindowLastDelay = Mathf.Max(
                    _goldWindowLastDelay,
                    _goldWindowElapsed + plan.LastArrivalDelay);
                if (_goldWindowElapsed < _goldWindowFirstDelay)
                {
                    _goldWindowFirstDelay = Mathf.Min(
                        _goldWindowFirstDelay,
                        _goldWindowElapsed + plan.FirstArrivalDelay);
                }

                return;
            }

            _goldWindowAmountBefore = _displayedGold;
            _goldWindowFirstDelay = plan.FirstArrivalDelay;
            _goldWindowLastDelay = plan.LastArrivalDelay;
            _goldWindowElapsed = 0f;
            _goldWindowRoutine = StartCoroutine(GoldGainWindowRoutine());
        }

        public void ClearSnapshot()
        {
            _hasSnapshot = false;
            _wasAtMaxHp = false;
            _displayedHp = 0f;
            _coreHp = 0;
            _coreMaxHp = 0;
            _armor.Reset();
            _gold.Reset();
            KillHpTweens();
            StopGoldWindow(snapToTarget: false);
            _hasGoldDisplay = false;
            _displayedGold = 0;
            _targetGold = 0;
        }

        private void EnsureBindings()
        {
            if (playerInfoRoot == null)
            {
                var rootGo = GameObject.Find(PlayerInfoRootName);
                if (rootGo != null)
                {
                    playerInfoRoot = rootGo.transform;
                }
            }

            if (playerInfoRoot == null)
            {
                return;
            }

            // Unity fake-null：Destroyed/Missing 对 ??= 仍算「有值」，DisableDomainReload 下会卡死旧引用。
            // 一律用 Unity 的 == null 再解析。
            if (bloodBarRoot == null)
            {
                bloodBarRoot = FindChild(playerInfoRoot, BloodBarName);
            }

            if (bloodSlot == null)
            {
                bloodSlot = FindChild(playerInfoRoot, BloodSlotName)?.GetComponent<SpriteRenderer>();
            }

            if (bloodFill == null)
            {
                bloodFill = FindChild(playerInfoRoot, BloodFillName)?.GetComponent<SpriteRenderer>();
            }

            if (currentHpText == null)
            {
                currentHpText = FindTmp(playerInfoRoot, CurrentHpName);
            }

            if (currentHpIcon == null)
            {
                currentHpIcon = FindChild(playerInfoRoot, CurrentHpIconName)?.GetComponent<SpriteRenderer>();
            }

            if (maxHpText == null)
            {
                maxHpText = FindTmp(playerInfoRoot, MaxHpName);
            }

            if (maxHpIcon == null)
            {
                maxHpIcon = FindChild(playerInfoRoot, MaxHpIconName)?.GetComponent<SpriteRenderer>();
            }

            if (armorText == null)
            {
                armorText = FindTmp(playerInfoRoot, ArmorValueName);
            }

            if (armorText == null)
            {
                var armorRoot = FindChild(playerInfoRoot, ArmorRootName);
                armorText = FindTmp(armorRoot, "数值");
            }

            if (goldText == null)
            {
                goldText = FindTmp(playerInfoRoot, GoldValueName);
            }

            if (goldIcon == null)
            {
                goldIcon = FindChild(playerInfoRoot, GoldIconName);
            }

            GoldHudDomainHost.Install(playerInfoRoot, goldIcon);
            EnsureAllHpHudVisible();
            CaptureBloodBarBasePoseIfNeeded();
        }

        private void EnsureAllHpHudVisible()
        {
            if (currentHpText != null)
            {
                currentHpText.enabled = true;
            }

            if (currentHpIcon != null)
            {
                currentHpIcon.enabled = true;
            }

            if (maxHpText != null)
            {
                maxHpText.enabled = true;
                var color = maxHpText.color;
                if (color.a < 0.99f)
                {
                    color.a = 1f;
                    maxHpText.color = color;
                }
            }

            if (maxHpIcon != null)
            {
                maxHpIcon.enabled = true;
            }
        }

        /// <summary>
        /// 记住场景里血条根的位姿（含 UI 描边加粗后的 scale×2），避免 tween 收尾写回 (1,1,1)。
        /// </summary>
        private void CaptureBloodBarBasePoseIfNeeded()
        {
            if (bloodBarRoot == null || _hasBloodBarBasePose)
            {
                return;
            }

            _bloodBarBasePos = bloodBarRoot.localPosition;
            _bloodBarBaseScale = bloodBarRoot.localScale;
            _hasBloodBarBasePose = true;
        }

        private void RestoreBloodBarBasePose()
        {
            if (bloodBarRoot == null || !_hasBloodBarBasePose)
            {
                return;
            }

            bloodBarRoot.localPosition = _bloodBarBasePos;
            bloodBarRoot.localScale = _bloodBarBaseScale;
        }

        private void CaptureVesselBaseIfNeeded()
        {
            if (_capturedVesselBase || bloodSlot == null || bloodFill == null)
            {
                return;
            }

            _baseSlotWidth = Mathf.Max(0.01f, bloodSlot.size.x);
            _baseFillWidth = Mathf.Max(0.01f, bloodFill.size.x);
            _fillLocalX = bloodFill.transform.localPosition.x;
            _animatedSlotWidth = _baseSlotWidth;
            _animatedFillWidth = _baseFillWidth;
            _capturedVesselBase = true;
        }

        private void ApplyHpVessel(int hp, int maxHp, bool animate, bool firstPaint)
        {
            hp = Mathf.Max(0, hp);
            maxHp = Mathf.Max(1, maxHp);
            _coreHp = hp;
            _coreMaxHp = maxHp;

            var atMax = hp >= maxHp;
            var prevHp = firstPaint ? hp : _displayedHp;
            var hpDelta = hp - prevHp;
            var reachedMax = animate && atMax && !firstPaint && !_wasAtMaxHp;
            _wasAtMaxHp = atMax;

            var targetSlot = ResolveSlotWidth(maxHp);
            var fullFill = ResolveFullFillWidth(maxHp);
            var targetFill = fullFill * Mathf.Clamp01(hp / (float)maxHp);

            if (maxHpText != null)
            {
                maxHpText.text = maxHp.ToString();
            }

            if (!animate || bloodSlot == null || bloodFill == null)
            {
                KillHpTweens();
                ApplySlotWidth(targetSlot);
                ApplyFillWidth(targetFill);
                _animatedSlotWidth = targetSlot;
                _animatedFillWidth = targetFill;
                _displayedHp = hp;
                if (currentHpText != null)
                {
                    currentHpText.text = hp.ToString();
                    currentHpText.color = atMax ? HpFullColor : HpNormalColor;
                }

                return;
            }

            PlayVesselMotion(targetSlot, targetFill, hp, hpDelta, atMax, reachedMax);
        }

        private void PlayVesselMotion(
            float targetSlot,
            float targetFill,
            int hp,
            float hpDelta,
            bool atMax,
            bool reachedMax)
        {
            KillHpTweens(keepDisplayedNumber: true);

            var slotChanged = Mathf.Abs(targetSlot - _animatedSlotWidth) > 0.001f;
            if (slotChanged)
            {
                _slotTween = DOTween
                    .To(() => _animatedSlotWidth, w =>
                    {
                        _animatedSlotWidth = w;
                        ApplySlotWidth(w);
                    }, targetSlot, SlotGrowDuration)
                    .SetEase(Ease.OutBack)
                    .SetUpdate(true)
                    .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            }
            else
            {
                ApplySlotWidth(targetSlot);
                _animatedSlotWidth = targetSlot;
            }

            var fillEase = hpDelta < 0f ? Ease.OutCubic : Ease.OutBack;
            var fillDuration = HpFillAnimDuration;
            if (hpDelta < 0f)
            {
                fillDuration = 0.32f;
                FlashFill(FillDamageColor);
                PunchBloodBar(damage: true);
                FlashHpText(HpDamageFlash, atMax ? HpFullColor : HpNormalColor);
            }
            else if (hpDelta > 0f)
            {
                FlashFill(FillHealColor);
                PunchBloodBar(damage: false);
                FlashHpText(HpHealFlash, atMax ? HpFullColor : HpNormalColor);
            }

            _fillTween = DOTween
                .To(() => _animatedFillWidth, w =>
                {
                    _animatedFillWidth = w;
                    ApplyFillWidth(w);
                }, targetFill, fillDuration)
                .SetEase(fillEase)
                .SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

            if (currentHpText != null)
            {
                _hpNumberTween = DOTween
                    .To(() => _displayedHp, v =>
                    {
                        _displayedHp = v;
                        currentHpText.text = Mathf.RoundToInt(v).ToString();
                    }, hp, fillDuration)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true)
                    .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                    .OnComplete(() =>
                    {
                        _displayedHp = hp;
                        currentHpText.text = hp.ToString();
                        currentHpText.color = atMax ? HpFullColor : HpNormalColor;
                    });
            }
            else
            {
                _displayedHp = hp;
            }

            if (reachedMax)
            {
                if (_maxHpPulseRoutine != null)
                {
                    StopCoroutine(_maxHpPulseRoutine);
                }

                _maxHpPulseRoutine = StartCoroutine(FlashMaxHpReached());
            }
        }

        private float ResolveSlotWidth(int maxHp)
        {
            return Mathf.Clamp(_baseSlotWidth + ResolveVesselGrowth(maxHp), 0.05f, MaxVesselWidth);
        }

        private float ResolveFullFillWidth(int maxHp)
        {
            return Mathf.Max(0.02f, _baseFillWidth + ResolveVesselGrowth(maxHp));
        }

        /// <summary>
        /// 相对基础上限的血管伸长量；与血槽共用封顶，避免血量再涨时 fill 继续变宽。
        /// </summary>
        private float ResolveVesselGrowth(int maxHp)
        {
            var delta = maxHp - baseMaxHp;
            var uncapped = delta * SlotWidthPerMaxHp;
            var maxGrowth = MaxVesselWidth - _baseSlotWidth;
            if (maxGrowth < 0f)
            {
                maxGrowth = 0f;
            }

            if (uncapped <= 0f)
            {
                return uncapped;
            }

            return Mathf.Min(uncapped, maxGrowth);
        }

        private void ApplySlotWidth(float width)
        {
            if (bloodSlot == null)
            {
                return;
            }

            var size = bloodSlot.size;
            size.x = width;
            bloodSlot.size = size;
        }

        private void ApplyFillWidth(float width)
        {
            if (bloodFill == null)
            {
                return;
            }

            var size = bloodFill.size;
            size.x = Mathf.Max(0.001f, width);
            bloodFill.size = size;

            var lp = bloodFill.transform.localPosition;
            lp.x = _fillLocalX;
            bloodFill.transform.localPosition = lp;
        }

        private void FlashFill(Color flash)
        {
            if (bloodFill == null)
            {
                return;
            }

            _fillColorTween?.Kill();
            bloodFill.color = flash;
            _fillColorTween = DOTween
                .To(() => bloodFill.color, c => bloodFill.color = c, FillNormalColor, 0.28f)
                .SetUpdate(true)
                .SetLink(bloodFill.gameObject, LinkBehaviour.KillOnDestroy);
        }

        private void FlashHpText(Color flash, Color settle)
        {
            if (currentHpText == null)
            {
                return;
            }

            _hpTextColorTween?.Kill();
            currentHpText.color = flash;
            _hpTextColorTween = DOTween
                .To(() => currentHpText.color, c => currentHpText.color = c, settle, NumberFlashDuration)
                .SetUpdate(true)
                .SetLink(currentHpText.gameObject, LinkBehaviour.KillOnDestroy);
        }

        private void PunchBloodBar(bool damage)
        {
            if (bloodBarRoot == null)
            {
                return;
            }

            CaptureBloodBarBasePoseIfNeeded();
            _barShakeTween?.Kill();
            RestoreBloodBarBasePose();
            if (damage)
            {
                _barShakeTween = bloodBarRoot
                    .DOShakePosition(0.22f, new Vector3(0.06f, 0.04f, 0f), 18, 90f, false, true)
                    .SetUpdate(true)
                    .SetLink(bloodBarRoot.gameObject, LinkBehaviour.KillOnDestroy)
                    .OnComplete(RestoreBloodBarBasePose);
            }
            else
            {
                _barShakeTween = bloodBarRoot
                    .DOPunchScale(new Vector3(0.06f, 0.1f, 0f), 0.28f, 8, 0.6f)
                    .SetUpdate(true)
                    .SetLink(bloodBarRoot.gameObject, LinkBehaviour.KillOnDestroy)
                    .OnComplete(RestoreBloodBarBasePose);
            }
        }

        private IEnumerator FlashMaxHpReached()
        {
            if (currentHpText == null)
            {
                yield break;
            }

            var settle = HpFullColor;
            for (var pulse = 0; pulse < MaxHpFlashPulses; pulse++)
            {
                var elapsed = 0f;
                var half = MaxHpFlashHalfPeriod;
                while (elapsed < half * 2f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    var rising = elapsed < half;
                    var t = rising
                        ? Mathf.Clamp01(elapsed / half)
                        : Mathf.Clamp01((elapsed - half) / half);
                    var blend = rising ? t : 1f - t;
                    currentHpText.color = Color.Lerp(settle, Color.white, blend);
                    yield return null;
                }
            }

            currentHpText.color = settle;
            _maxHpPulseRoutine = null;
        }

        private void ApplyGold(int gold, bool animate)
        {
            gold = Mathf.Max(0, gold);
            _gold.Value = gold;
            _gold.HasValue = true;

            // 数字窗推进中：只抬目标，避免 Sync 双写抢戏。
            if (_goldWindowActive && gold >= _displayedGold)
            {
                _targetGold = Mathf.Max(_targetGold, gold);
                return;
            }

            // 扣金或静默 / 无飞币窗口的 Sync：即时对齐（飞币增益由 Binder 驱动窗口）。
            if (!animate || gold <= _displayedGold || !_hasGoldDisplay)
            {
                SnapGold(gold);
                return;
            }

            SnapGold(gold);
        }

        private IEnumerator GoldGainWindowRoutine()
        {
            _goldWindowActive = true;
            var lastWritten = _goldWindowAmountBefore;

            while (_goldWindowElapsed < _goldWindowLastDelay)
            {
                _goldWindowElapsed += Time.unscaledDeltaTime;
                var sample = GoldHudNumberWindow.SampleDisplayed(
                    _goldWindowAmountBefore,
                    _targetGold,
                    _goldWindowFirstDelay,
                    _goldWindowLastDelay,
                    _goldWindowElapsed);
                if (sample != lastWritten)
                {
                    _displayedGold = sample;
                    ApplyGoldText(sample, flash: sample > lastWritten);
                    lastWritten = sample;
                }

                yield return null;
            }

            _displayedGold = _targetGold;
            ApplyGoldText(_targetGold, flash: false);
            _goldWindowActive = false;
            _goldWindowRoutine = null;
        }

        private void StopGoldWindow(bool snapToTarget)
        {
            if (_goldWindowRoutine != null)
            {
                StopCoroutine(_goldWindowRoutine);
                _goldWindowRoutine = null;
            }

            _goldWindowActive = false;
            if (snapToTarget && _hasGoldDisplay)
            {
                _displayedGold = _targetGold;
                ApplyGoldText(_targetGold, flash: false);
            }
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
            _goldFlashTween = DOTween
                .To(() => goldText.color, c => goldText.color = c, _goldBaseColor, 0.1f)
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

        private void ApplyIntStat(
            StatSlot slot,
            TMP_Text label,
            int value,
            string text,
            bool animate)
        {
            if (label == null)
            {
                return;
            }

            var changed = !slot.HasValue || slot.Value != value || label.text != text;
            slot.Value = value;
            slot.HasValue = true;
            if (!changed)
            {
                return;
            }

            label.text = text;
            if (!animate)
            {
                return;
            }

            StopPunch(slot, label);
            slot.PunchRoutine = StartCoroutine(FlashColor(label, slot, label.color));
        }

        private void StopPunch(StatSlot slot, TMP_Text label)
        {
            if (slot.PunchRoutine != null)
            {
                StopCoroutine(slot.PunchRoutine);
                slot.PunchRoutine = null;
            }

            if (label != null)
            {
                EnsureBaseScale(slot, label);
                label.rectTransform.localScale = slot.BaseScale;
            }
        }

        private static void EnsureBaseScale(StatSlot slot, TMP_Text label)
        {
            if (slot.HasBaseScale || label == null)
            {
                return;
            }

            slot.BaseScale = label.rectTransform.localScale;
            slot.HasBaseScale = true;
        }

        private IEnumerator FlashColor(TMP_Text label, StatSlot slot, Color settleColor)
        {
            if (label == null)
            {
                yield break;
            }

            EnsureBaseScale(slot, label);
            label.rectTransform.localScale = slot.BaseScale;

            var flashColor = Color.Lerp(settleColor, Color.white, 0.55f);
            var elapsed = 0f;
            const float flashDuration = 0.18f;
            while (elapsed < flashDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var flashT = Mathf.Clamp01(elapsed / flashDuration);
                label.color = Color.Lerp(flashColor, settleColor, flashT);
                yield return null;
            }

            label.color = settleColor;
            label.rectTransform.localScale = slot.BaseScale;
            slot.PunchRoutine = null;
        }

        private void KillHpTweens(bool keepDisplayedNumber = false)
        {
            _fillTween?.Kill();
            _slotTween?.Kill();
            _hpNumberTween?.Kill();
            _fillColorTween?.Kill();
            _hpTextColorTween?.Kill();
            _barShakeTween?.Kill();
            _fillTween = null;
            _slotTween = null;
            _hpNumberTween = null;
            _fillColorTween = null;
            _hpTextColorTween = null;
            _barShakeTween = null;

            if (!keepDisplayedNumber)
            {
                _displayedHp = _coreHp;
            }

            RestoreBloodBarBasePose();

            if (bloodFill != null)
            {
                bloodFill.color = FillNormalColor;
            }
        }

        private static bool TryReadAvatarHud(
            out int hp,
            out int maxHp,
            out int armor,
            out int gold)
        {
            hp = 0;
            maxHp = 0;
            armor = 0;
            gold = 0;

            var arch = NineGridArchitecture.Current;
            if (arch == null)
            {
                return false;
            }

            var player = arch.GetModel<PlayerModel>();
            gold = player.Coins != null ? Mathf.Max(0, player.Coins.Value) : 0;

            var board = arch.GetModel<BoardModel>();
            var avatarUid = board.AvatarUid != null ? board.AvatarUid.Value : 0;
            if (avatarUid <= 0 || !arch.GetModel<CardRegistry>().TryGet(avatarUid, out var avatar))
            {
                return false;
            }

            var stats = arch.GetSystem<IStatSystem>();
            hp = Mathf.Max(0, stats.GetEffectiveInt(avatar, StatId.Hp));
            maxHp = Mathf.Max(hp, stats.GetEffectiveInt(avatar, StatId.MaxHp));
            // ADR-0028：玩家信息 HUD 甲 = 有效护甲（基础 + 遗物/图腾等 Modifier），
            // 与玩家卡面显示的真实护甲（当前护甲）区分。遗物「基础护甲+N」经
            // Persistent Modifier 进有效护甲，StatId.Armor 基础值本身不变，
            // 故不能只读 GetBaseArmor，否则遗物加成在 HUD 上永不显现。
            armor = StatArmorUtility.GetEffectiveArmor(stats, avatar);
            return true;
        }

        private static Transform FindChild(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            var trimTarget = childName.Trim();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.name.Trim() == trimTarget)
                {
                    return t;
                }
            }

            return null;
        }

        private static TMP_Text FindTmp(Transform root, string childName)
        {
            var child = FindChild(root, childName);
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        private sealed class StatSlot
        {
            public int Value;
            public bool HasValue;
            public bool HasBaseScale;
            public Vector3 BaseScale = Vector3.one;
            public Coroutine PunchRoutine;

            public void Reset()
            {
                Value = 0;
                HasValue = false;
                HasBaseScale = false;
                BaseScale = Vector3.one;
                PunchRoutine = null;
            }
        }
    }
}
