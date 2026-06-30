using System;
using System.Collections;
using DG.Tweening;
using NineGrid.Core;
using NineGrid.Presentation.Visuals;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace NineGrid.Presentation.Projection
{
    /// <summary>
    /// 场面板 / 卡牌状态投影：攻击 / 生命跳变 + 护甲块动画；批末对齐入口。
    /// </summary>
    [DisallowMultipleComponent]
    [MovedFrom(true, "NineGrid.Presentation.Performance", null, "StatusPanelUpdatePerformance")]
    public sealed class InGameStatusProjection : MonoBehaviour
    {
        [Header("Digit Sprites (0-9)")]
        [SerializeField] private Sprite[] digitSprites = new Sprite[10];

        [Header("Playback")]
        [SerializeField] private bool deferPlayOneFrame = true;

        private Sprite[] resolvedDigitSprites;

        public bool IsPlaying { get; private set; }

        public float TotalDuration { get; private set; }

        private void Awake()
        {
            resolvedDigitSprites = ResolveDigitSprites();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (digitSprites == null || digitSprites.Length < 10 || digitSprites[0] == null)
            {
                Sprite[] loaded = TableNineDigitSpriteLibrary.LoadDefaultDigits();
                if (loaded != null)
                {
                    digitSprites = loaded;
                }
            }
        }
#endif

        public void Play(Transform cardActor, CoreGameEvent evt, CoreViewSnapshot snapshot, Action onComplete = null)
        {
            if (cardActor == null || evt == null)
            {
                onComplete?.Invoke();
                return;
            }

            TableNineCardStatusView view = EnsureCardView(cardActor);
            if (view == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (deferPlayOneFrame && isActiveAndEnabled)
            {
                StartCoroutine(PlayDeferred(view, evt, snapshot, onComplete));
                return;
            }

            PlayImmediate(view, evt, snapshot, onComplete);
        }

        public void AlignCard(Transform cardActor, BoardSlotView slot, bool instant = true)
        {
            if (cardActor == null || slot == null)
            {
                return;
            }

            TableNineCardStatusView view = EnsureCardView(cardActor);
            if (view == null)
            {
                return;
            }

            DOTween.Kill(cardActor);
            view.SnapFromSlot(slot);
        }

        public void AlignFromSnapshot(CoreViewSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            for (var i = 0; i < snapshot.BoardSlots.Count; i++)
            {
                BoardSlotView slot = snapshot.BoardSlots[i];
                if (slot.CardUid <= 0)
                {
                    continue;
                }
            }
        }

        private IEnumerator PlayDeferred(
            TableNineCardStatusView view,
            CoreGameEvent evt,
            CoreViewSnapshot snapshot,
            Action onComplete)
        {
            yield return null;
            PlayImmediate(view, evt, snapshot, onComplete);
        }

        private void PlayImmediate(
            TableNineCardStatusView view,
            CoreGameEvent evt,
            CoreViewSnapshot snapshot,
            Action onComplete)
        {
            IsPlaying = true;
            TotalDuration = view.ApplyEvent(evt, snapshot, animate: true);
            if (TotalDuration <= 0f)
            {
                IsPlaying = false;
                onComplete?.Invoke();
                return;
            }

            DOVirtual.DelayedCall(TotalDuration, () =>
            {
                IsPlaying = false;
                onComplete?.Invoke();
            }).SetTarget(this);
        }

        private TableNineCardStatusView EnsureCardView(Transform cardActor)
        {
            TableNineCardStatusView view = cardActor.GetComponent<TableNineCardStatusView>();
            if (view == null)
            {
                view = cardActor.GetComponentInChildren<TableNineCardStatusView>(true);
            }

            if (view == null)
            {
                view = cardActor.gameObject.AddComponent<TableNineCardStatusView>();
                view.EnsureBindings();
            }

            view.ConfigureDigitSprites(ResolveDigitSprites());
            return view;
        }

        public void ConfigurePreviewView(TableNineCardStatusView view)
        {
            if (view == null)
            {
                return;
            }

            view.ConfigureDigitSprites(ResolveDigitSprites());
        }

        private Sprite[] ResolveDigitSprites()
        {
            if (resolvedDigitSprites != null && resolvedDigitSprites.Length >= 10 && resolvedDigitSprites[0] != null)
            {
                return resolvedDigitSprites;
            }

            if (digitSprites != null && digitSprites.Length >= 10 && digitSprites[0] != null)
            {
                resolvedDigitSprites = digitSprites;
                return resolvedDigitSprites;
            }

            resolvedDigitSprites = TableNineDigitSpriteLibrary.LoadDefaultDigits();
            return resolvedDigitSprites;
        }

#if UNITY_EDITOR
        [Header("Preview")]
        [SerializeField] private Transform previewCardActor;

        [ContextMenu("Preview/Attack 1→7")]
        private void PreviewAttackJump()
        {
            PlayPreviewJump(isAttack: true, 1, 7);
        }

        [ContextMenu("Preview/Life 5→2")]
        private void PreviewLifeJump()
        {
            PlayPreviewJump(isAttack: false, 5, 2);
        }

        [ContextMenu("Preview/Armor 1→3")]
        private void PreviewArmorGain()
        {
            Transform actor = EnsurePreviewActor();
            TableNineCardStatusView view = EnsureCardView(actor);
            view.SnapFromSlot(new BoardSlotView(SlotId.Board(1), 1, "preview", CardKind.Monster, 5, 5, 5, 5, 1, 1, 3, 3, false));
            view.PlayArmorTo(3, animate: true);
        }

        private void PlayPreviewJump(bool isAttack, int from, int to)
        {
            Transform actor = EnsurePreviewActor();
            TableNineCardStatusView view = EnsureCardView(actor);
            var slot = new BoardSlotView(SlotId.Board(1), 1, "preview", CardKind.Monster, 5, 5, 5, 5, 0, 0, from, from, false);
            view.SnapFromSlot(slot);
            if (isAttack)
            {
                view.PlayAttackTo(to, animate: true);
            }
            else
            {
                view.PlayLifeTo(to, animate: true);
            }
        }

        private Transform EnsurePreviewActor()
        {
            if (previewCardActor != null)
            {
                return previewCardActor;
            }

            GameObject card = GameObject.Find("Card");
            return card != null ? card.transform : transform;
        }
#endif
    }
}
