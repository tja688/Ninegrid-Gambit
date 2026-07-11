using System.Collections;
using NineGrid.Core;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using TMPro;
using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 局内 PlayerInfo Text：从内核 Avatar / Coins 刷血攻甲金与名称，数值变化时做轻量跳变。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInfoHudPresenter : MonoBehaviour
    {
        private const string DefaultInfoRootName = "InGameInfo Text";
        private const string DefaultPlayerInfoName = "PlayerInfoText";
        private const float FlashDuration = 0.18f;
        private const float MaxHpFlashHalfPeriod = 0.12f;
        private const int MaxHpFlashPulses = 2;

        private static readonly Color HpNormalColor = new Color32(0x00, 0xBE, 0x13, 0xFF);
        private static readonly Color HpMaxColor = new Color32(0x03, 0xFF, 0x1D, 0xFF);

        private static PlayerInfoHudPresenter _instance;

        [Tooltip("血量 TMP；留空则运行时在 PlayerInfo Text 下按名查找 HpText。")]
        [SerializeField] private TextMeshProUGUI hpText;

        [Tooltip("攻击 TMP；留空则运行时在 PlayerInfo Text 下按名查找 AttackText。")]
        [SerializeField] private TextMeshProUGUI attackText;

        [Tooltip("护甲 TMP；留空则运行时在 PlayerInfo Text 下按名查找 ArmorText。")]
        [SerializeField] private TextMeshProUGUI armorText;

        [Tooltip("金币 TMP；留空则运行时在 PlayerInfo Text 下按名查找 GoldText。")]
        [SerializeField] private TextMeshProUGUI goldText;

        [Tooltip("名称 TMP；留空则运行时在 PlayerInfo Text 下按名查找 NameText。")]
        [SerializeField] private TextMeshProUGUI nameText;

        private readonly StatSlot _hp = new();
        private readonly StatSlot _attack = new();
        private readonly StatSlot _armor = new();
        private readonly StatSlot _gold = new();
        private bool _hasSnapshot;
        private bool _wasAtMaxHp;
        private string _lastName = string.Empty;

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

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            EnsureBindings();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// 从内核刷新 PlayerInfoText；<paramref name="animate"/> 为 true 时对变化项做跳变。
        /// </summary>
        public void SyncFromCore(bool animate = true)
        {
            EnsureBindings();

            if (!TryReadAvatarHud(out var hp, out var maxHp, out var attack, out var armor, out var gold, out var displayName))
            {
                return;
            }

            var firstPaint = !_hasSnapshot;
            var shouldAnimate = animate && !firstPaint;
            _hasSnapshot = true;

            ApplyHp(hp, maxHp, shouldAnimate);
            ApplyIntStat(_attack, attackText, attack, attack.ToString(), shouldAnimate);
            ApplyIntStat(_armor, armorText, armor, armor.ToString(), shouldAnimate);
            ApplyGold(gold, shouldAnimate);

            if (nameText != null && displayName != _lastName)
            {
                _lastName = displayName;
                nameText.text = displayName;
            }
        }

        public void ClearSnapshot()
        {
            _hasSnapshot = false;
            _lastName = string.Empty;
            _hp.Reset();
            _attack.Reset();
            _armor.Reset();
            _gold.Reset();
            _wasAtMaxHp = false;
            // 清场只丢 HUD 缓存，不把金币显示 Snap 到 0（跨关 Opening 期间会闪零再暴涨）。
            // 保留 GoldGainFx 当前显示；Opening / SyncFromCore 再对齐 Core。
        }

        private void EnsureBindings()
        {
            if (hpText != null
                && attackText != null
                && armorText != null
                && goldText != null
                && nameText != null)
            {
                return;
            }

            var root = FindPlayerInfoRoot();
            if (root == null)
            {
                return;
            }

            hpText ??= FindTmp(root, "HpText");
            attackText ??= FindTmp(root, "AttackText");
            armorText ??= FindTmp(root, "ArmorText");
            goldText ??= FindTmp(root, "GoldText");
            nameText ??= FindTmp(root, "NameText");
        }

        private static Transform FindPlayerInfoRoot()
        {
            var overlay = GameObject.Find("TableNine Text Overlay UI");
            if (overlay != null)
            {
                var direct = overlay.transform.Find($"{DefaultInfoRootName}/{DefaultPlayerInfoName}");
                if (direct != null)
                {
                    return direct;
                }

                foreach (var t in overlay.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == DefaultPlayerInfoName)
                    {
                        return t;
                    }
                }
            }

            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t != null
                    && t.name == DefaultPlayerInfoName
                    && t.gameObject.scene.IsValid())
                {
                    return t;
                }
            }

            return null;
        }

        private static TextMeshProUGUI FindTmp(Transform root, string childName)
        {
            var child = root.Find(childName);
            return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
        }

        private static bool TryReadAvatarHud(
            out int hp,
            out int maxHp,
            out int attack,
            out int armor,
            out int gold,
            out string displayName)
        {
            hp = 0;
            maxHp = 0;
            attack = 0;
            armor = 0;
            gold = 0;
            displayName = string.Empty;

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
            attack = Mathf.Max(0, stats.GetEffectiveInt(avatar, StatId.Attack));
            armor = Mathf.Max(0, stats.GetEffectiveInt(avatar, StatId.Armor));
            displayName = ResolveAvatarDisplayName(avatar.DefId);
            return true;
        }

        private static string ResolveAvatarDisplayName(string defId)
        {
            if (string.IsNullOrEmpty(defId))
            {
                return "玩家";
            }

            CoreCardPresentationMapper.EnsureContentCatalogLoaded();
            var arch = NineGridArchitecture.Current;
            var content = arch?.GetSystem<IContentSystem>();
            if (content != null
                && content.HasCatalog
                && content.Catalog.Cards.TryGetValue(defId, out var card)
                && !string.IsNullOrWhiteSpace(card.DisplayName))
            {
                return card.DisplayName;
            }

            return "玩家";
        }

        private void ApplyHp(int hp, int maxHp, bool animate)
        {
            if (hpText == null)
            {
                return;
            }

            var atMax = maxHp > 0 && hp >= maxHp;
            var text = Mathf.Max(0, hp).ToString();
            var targetColor = atMax ? HpMaxColor : HpNormalColor;
            var hadValue = _hp.HasValue;
            var valueChanged = !hadValue || _hp.Value != hp || hpText.text != text;
            // 首次刷入不闪；仅从未满 → 满血时提醒两下。
            var reachedMax = animate && atMax && hadValue && !_wasAtMaxHp;
            _hp.Value = hp;
            _hp.HasValue = true;
            _wasAtMaxHp = atMax;

            if (!valueChanged && !reachedMax)
            {
                hpText.color = targetColor;
                return;
            }

            hpText.text = text;

            StopPunch(_hp, hpText);

            if (!animate)
            {
                hpText.color = targetColor;
                return;
            }

            if (reachedMax)
            {
                _hp.PunchRoutine = StartCoroutine(FlashMaxHp(hpText, _hp, targetColor));
                return;
            }

            hpText.color = targetColor;
            if (valueChanged)
            {
                _hp.PunchRoutine = StartCoroutine(FlashColor(hpText, _hp, targetColor));
            }
        }

        private void ApplyGold(int gold, bool animate)
        {
            // 增益演出由 GoldGainFx 缓冲驱动文本；本处只同步槽位，避免瞬间跳变抢戏。
            if (GoldGainFxManagerSingleton.TryGetInstance(out var goldFx)
                && goldFx.TryHandleGoldSync(gold, animate))
            {
                _gold.Value = gold;
                _gold.HasValue = true;
                return;
            }

            ApplyIntStat(_gold, goldText, gold, gold.ToString(), animate);
        }

        private void ApplyIntStat(
            StatSlot slot,
            TextMeshProUGUI label,
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

        private void StopPunch(StatSlot slot, TextMeshProUGUI label)
        {
            if (slot.PunchRoutine != null)
            {
                StopCoroutine(slot.PunchRoutine);
                slot.PunchRoutine = null;
            }

            // 左对齐 + 中心 pivot：缩放残留会把字形左缘往右推，停动画时必须还原。
            if (label != null)
            {
                EnsureBaseScale(slot, label);
                label.rectTransform.localScale = slot.BaseScale;
            }
        }

        private static void EnsureBaseScale(StatSlot slot, TextMeshProUGUI label)
        {
            if (slot.HasBaseScale || label == null)
            {
                return;
            }

            slot.BaseScale = label.rectTransform.localScale;
            slot.HasBaseScale = true;
        }

        private IEnumerator FlashColor(TextMeshProUGUI label, StatSlot slot, Color settleColor)
        {
            if (label == null)
            {
                yield break;
            }

            EnsureBaseScale(slot, label);
            label.rectTransform.localScale = slot.BaseScale;

            var flashColor = Color.Lerp(settleColor, Color.white, 0.55f);
            var elapsed = 0f;
            while (elapsed < FlashDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var flashT = Mathf.Clamp01(elapsed / FlashDuration);
                label.color = Color.Lerp(flashColor, settleColor, flashT);
                yield return null;
            }

            label.color = settleColor;
            label.rectTransform.localScale = slot.BaseScale;
            slot.PunchRoutine = null;
        }

        private IEnumerator FlashMaxHp(TextMeshProUGUI label, StatSlot slot, Color settleColor)
        {
            if (label == null)
            {
                yield break;
            }

            EnsureBaseScale(slot, label);
            label.rectTransform.localScale = slot.BaseScale;
            var white = Color.white;

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
                    label.color = Color.Lerp(settleColor, white, blend);
                    yield return null;
                }
            }

            label.color = settleColor;
            label.rectTransform.localScale = slot.BaseScale;
            slot.PunchRoutine = null;
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
