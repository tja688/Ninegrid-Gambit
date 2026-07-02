using DamageNumbersPro;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace NineGrid.Presentation.Feedback
{
    public enum DamagePopupKind
    {
        Damage,
        Heal,
        Gold,
    }

    /// <summary>
    /// 伤害/治疗/金币飘字：由战斗 Flow 在 Impact 时刻调用。
    /// </summary>
    [DisallowMultipleComponent]
    [MovedFrom(true, "NineGrid.Presentation.Reactions", null, "DamageNumbersReaction")]
    public sealed class DamageNumberFeedback : MonoBehaviour
    {
        [Header("Templates (DNP_2D)")]
        [SerializeField] private DamageNumber damagePrefab;
        [SerializeField] private DamageNumber healPrefab;
        [SerializeField] private DamageNumber goldPrefab;

        [Header("Placement")]
        [SerializeField] private Vector3 worldOffset = new(0f, 0.75f, 0f);
        [SerializeField] private bool followTarget = true;

        public void Play(Transform target, float amount, DamagePopupKind kind)
        {
            if (target == null)
            {
                return;
            }

            Play(target.position + worldOffset, amount, kind, followTarget ? target : null);
        }

        public void Play(Vector3 worldPosition, float amount, DamagePopupKind kind, Transform follow = null)
        {
            DamageNumber template = ResolveTemplate(kind);
            if (template == null || amount <= 0f)
            {
                return;
            }

            DamageNumber spawned = template.Spawn(worldPosition, amount);
            if (spawned == null)
            {
                return;
            }

            if (follow != null)
            {
                spawned.SetFollowedTarget(follow);
            }
        }

        private DamageNumber ResolveTemplate(DamagePopupKind kind)
        {
            switch (kind)
            {
                case DamagePopupKind.Heal:
                    return healPrefab != null ? healPrefab : damagePrefab;
                case DamagePopupKind.Gold:
                    return goldPrefab != null ? goldPrefab : damagePrefab;
                default:
                    return damagePrefab;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Preview/Damage Popup")]
        private void PreviewDamagePopup()
        {
            Play(transform.position + worldOffset, 12f, DamagePopupKind.Damage, transform);
        }

        [ContextMenu("Preview/Heal Popup")]
        private void PreviewHealPopup()
        {
            Play(transform.position + worldOffset, 8f, DamagePopupKind.Heal, transform);
        }

        [ContextMenu("Preview/Gold Popup")]
        private void PreviewGoldPopup()
        {
            Play(transform.position + worldOffset, 5f, DamagePopupKind.Gold, transform);
        }
#endif
    }
}
