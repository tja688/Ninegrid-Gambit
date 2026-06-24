using System;
using System.Collections.Generic;
using DG.Tweening;
using NineGrid.Core;
using UnityEngine;

namespace NineGrid.Presentation.Visuals
{
    /// <summary>
    /// 场地 Card 模版状态视图：攻击 / 生命直接跳变数字 + 护甲块显隐。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TableNineCardStatusView : MonoBehaviour
    {
        private static readonly Vector3 DefaultArmorBlockScale = new(0.727269411f, 2.9086678f, 1f);

        [Header("Digit Stats")]
        [SerializeField] private DirectDigitDisplay attackDisplay;
        [SerializeField] private DirectDigitDisplay lifeDisplay;

        [Header("Armor Blocks")]
        [SerializeField] private Transform armorRoot;
        [SerializeField] private List<SpriteRenderer> armorBlocks = new();

        [Header("Armor Motion")]
        [SerializeField, Min(0f)] private float armorPopDuration = 0.18f;
        [SerializeField] private Ease armorPopEase = Ease.OutBack;

        private int currentAttack;
        private int currentLife;
        private int currentArmor;
        private bool bindingsReady;
        private Sprite[] configuredDigitSprites;
        private Vector3[] armorBlockRestScales = Array.Empty<Vector3>();
        private float armorBlockSpacing;

        public int CurrentAttack => currentAttack;
        public int CurrentLife => currentLife;
        public int CurrentArmor => currentArmor;

        public DirectDigitDisplay AttackDisplay => attackDisplay;
        public DirectDigitDisplay LifeDisplay => lifeDisplay;

        public void EnsureBindings()
        {
            if (bindingsReady)
            {
                return;
            }

            if (attackDisplay == null)
            {
                attackDisplay = EnsureRollDisplay("Attack");
            }

            if (lifeDisplay == null)
            {
                lifeDisplay = EnsureRollDisplay("Life");
            }

            if (configuredDigitSprites != null)
            {
                attackDisplay?.ConfigureSprites(configuredDigitSprites);
                lifeDisplay?.ConfigureSprites(configuredDigitSprites);
            }

            if (armorRoot == null)
            {
                Transform armor = transform.Find("Armor");
                if (armor != null)
                {
                    armorRoot = armor;
                }
            }

            if (armorRoot != null && armorBlocks.Count == 0)
            {
                armorBlocks.Clear();
                SpriteRenderer[] renderers = armorRoot.GetComponentsInChildren<SpriteRenderer>(true);
                for (var i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i].transform == armorRoot)
                    {
                        continue;
                    }

                    armorBlocks.Add(renderers[i]);
                }
            }

            CaptureArmorLayout();

            bindingsReady = true;
        }

        private void CaptureArmorLayout()
        {
            armorBlockRestScales = new Vector3[armorBlocks.Count];
            for (var i = 0; i < armorBlocks.Count; i++)
            {
                SpriteRenderer block = armorBlocks[i];
                armorBlockRestScales[i] = block != null ? block.transform.localScale : DefaultArmorBlockScale;
            }

            if (armorBlocks.Count >= 2
                && armorBlocks[0] != null
                && armorBlocks[1] != null)
            {
                armorBlockSpacing = armorBlocks[1].transform.localPosition.x - armorBlocks[0].transform.localPosition.x;
            }
            else
            {
                armorBlockSpacing = 0.11363584f;
            }
        }

        public void ConfigureDigitSprites(Sprite[] digitSprites)
        {
            if (digitSprites == null || digitSprites.Length < 10)
            {
                return;
            }

            configuredDigitSprites = digitSprites;
            bindingsReady = false;
            EnsureBindings();
        }

        public float DebugIncrementAttack()
        {
            EnsureBindings();
            return PlayAttackTo(currentAttack + 1, animate: true);
        }

        public float DebugDecrementLife()
        {
            EnsureBindings();
            return PlayLifeTo(Mathf.Max(0, currentLife - 1), animate: true);
        }

        public float DebugIncrementArmor()
        {
            EnsureBindings();
            return PlayArmorTo(currentArmor + 1, animate: true);
        }

        public void DebugSnapDefaults()
        {
            EnsureBindings();
            currentAttack = 1;
            currentLife = 5;
            currentArmor = 0;
            attackDisplay?.SnapTo(currentAttack);
            lifeDisplay?.SnapTo(currentLife);
            SnapArmor(currentArmor);
        }

        public void SnapFromSlot(BoardSlotView slot)
        {
            if (slot == null)
            {
                return;
            }

            EnsureBindings();
            currentAttack = slot.Attack;
            currentLife = slot.Hp;
            currentArmor = slot.Armor;

            attackDisplay?.SnapTo(currentAttack);
            lifeDisplay?.SnapTo(currentLife);
            SnapArmor(currentArmor);
        }

        public float PlayAttackTo(int attack, bool animate)
        {
            EnsureBindings();
            currentAttack = Mathf.Max(0, attack);
            if (attackDisplay == null)
            {
                return 0f;
            }

            return animate ? attackDisplay.PlayTo(currentAttack) : SnapRoll(attackDisplay, currentAttack);
        }

        public float PlayLifeTo(int life, bool animate)
        {
            EnsureBindings();
            currentLife = Mathf.Max(0, life);
            if (lifeDisplay == null)
            {
                return 0f;
            }

            return animate ? lifeDisplay.PlayTo(currentLife) : SnapRoll(lifeDisplay, currentLife);
        }

        public float PlayArmorTo(int armor, bool animate)
        {
            EnsureBindings();
            int next = Mathf.Max(0, armor);
            EnsureArmorBlockCapacity(next);
            float duration = 0f;

            if (animate && next > currentArmor)
            {
                for (int i = currentArmor; i < next; i++)
                {
                    SpriteRenderer block = armorBlocks[i];
                    if (block == null)
                    {
                        continue;
                    }

                    Vector3 restScale = GetArmorBlockRestScale(i);
                    block.gameObject.SetActive(true);
                    Transform blockTransform = block.transform;
                    DOTween.Kill(blockTransform);
                    blockTransform.localScale = Vector3.zero;
                    duration = Mathf.Max(
                        duration,
                        blockTransform
                            .DOScale(restScale, armorPopDuration)
                            .SetEase(armorPopEase)
                            .SetTarget(blockTransform)
                            .Duration());
                }
            }
            else
            {
                SnapArmor(next);
            }

            currentArmor = next;
            for (int i = currentArmor; i < armorBlocks.Count; i++)
            {
                if (armorBlocks[i] != null)
                {
                    armorBlocks[i].gameObject.SetActive(false);
                }
            }

            return duration;
        }

        public float ApplyEvent(CoreGameEvent evt, CoreViewSnapshot snapshot, bool animate)
        {
            if (evt == null)
            {
                return 0f;
            }

            BoardSlotView slot = ResolveSlot(evt, snapshot);

            switch (evt.Type)
            {
                case CoreEventType.HpChanged:
                case CoreEventType.Healed:
                    return PlayLifeTo(evt.RemainingHp, animate);
                case CoreEventType.ArmorChanged:
                    return PlayArmorTo(evt.RemainingArmor, animate);
                case CoreEventType.BaseStatModified:
                    return PlayBaseStatModified(evt, slot, animate);
                default:
                    return 0f;
            }
        }

        private float PlayBaseStatModified(CoreGameEvent evt, BoardSlotView slot, bool animate)
        {
            if (slot == null)
            {
                return 0f;
            }

            switch ((StatId)evt.Amount)
            {
                case StatId.Attack:
                    return PlayAttackTo(slot.Attack, animate);
                case StatId.Armor:
                    return PlayArmorTo(slot.Armor, animate);
                case StatId.Hp:
                case StatId.MaxHp:
                    return PlayLifeTo(slot.Hp, animate);
                default:
                    return 0f;
            }
        }

        private void SnapArmor(int armor)
        {
            EnsureArmorBlockCapacity(armor);
            for (var i = 0; i < armorBlocks.Count; i++)
            {
                SpriteRenderer block = armorBlocks[i];
                if (block == null)
                {
                    continue;
                }

                bool active = i < armor;
                block.gameObject.SetActive(active);
                if (active)
                {
                    DOTween.Kill(block.transform);
                    block.transform.localScale = GetArmorBlockRestScale(i);
                }
            }
        }

        private void EnsureArmorBlockCapacity(int armorCount)
        {
            if (armorCount <= armorBlocks.Count || armorBlocks.Count == 0 || armorRoot == null)
            {
                return;
            }

            SpriteRenderer template = armorBlocks[0];
            if (template == null)
            {
                return;
            }

            Vector3 templatePosition = template.transform.localPosition;
            Vector3 restScale = GetArmorBlockRestScale(0);
            while (armorBlocks.Count < armorCount)
            {
                int index = armorBlocks.Count;
                GameObject clone = Instantiate(template.gameObject, armorRoot);
                clone.name = $"armor value ({index})";
                Transform cloneTransform = clone.transform;
                cloneTransform.localPosition = new Vector3(
                    templatePosition.x + armorBlockSpacing * index,
                    templatePosition.y,
                    templatePosition.z);
                cloneTransform.localScale = restScale;

                SpriteRenderer renderer = clone.GetComponent<SpriteRenderer>();
                armorBlocks.Add(renderer);

                var scales = new Vector3[armorBlockRestScales.Length + 1];
                Array.Copy(armorBlockRestScales, scales, armorBlockRestScales.Length);
                scales[index] = restScale;
                armorBlockRestScales = scales;
            }
        }

        private Vector3 GetArmorBlockRestScale(int index)
        {
            if (armorBlockRestScales != null && index >= 0 && index < armorBlockRestScales.Length)
            {
                return armorBlockRestScales[index];
            }

            if (index >= 0 && index < armorBlocks.Count && armorBlocks[index] != null)
            {
                return armorBlocks[index].transform.localScale;
            }

            return DefaultArmorBlockScale;
        }

        private static float SnapRoll(DirectDigitDisplay display, int value)
        {
            display.SnapTo(value);
            return 0f;
        }

        private static BoardSlotView ResolveSlot(CoreGameEvent evt, CoreViewSnapshot snapshot)
        {
            if (snapshot == null || evt == null || evt.CardUid <= 0)
            {
                return null;
            }

            for (var i = 0; i < snapshot.BoardSlots.Count; i++)
            {
                BoardSlotView slot = snapshot.BoardSlots[i];
                if (slot.CardUid == evt.CardUid)
                {
                    return slot;
                }
            }

            return null;
        }

        private DirectDigitDisplay EnsureRollDisplay(string childName)
        {
            Transform child = transform.Find(childName);
            if (child == null)
            {
                return null;
            }

            DirectDigitDisplay display = child.GetComponent<DirectDigitDisplay>();
            if (display == null)
            {
                DrumRollDigitDisplay legacy = child.GetComponent<DrumRollDigitDisplay>();
                if (legacy != null)
                {
                    Destroy(legacy);
                }

                display = child.gameObject.AddComponent<DirectDigitDisplay>();
            }

            return display;
        }
    }
}
