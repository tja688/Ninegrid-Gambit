using System;
using DG.Tweening;
using Febucci.TextAnimatorForUnity.TextMeshPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NineGrid.UI
{
    /// <summary>
    /// 轻量对话系统：从 DialoguePool 取预设，驱动 Portrait / DialogBox / DialogText 演出。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DialogueSystem : MonoBehaviour
    {
        [SerializeField] DialoguePool pool;
        [SerializeField] UiSystem ui;
        [SerializeField] TextAnimator_TMP dialogAnimator;

        [Header("Timing")]
        [SerializeField] float portraitFadeDuration = 0.35f;
        [SerializeField] float dialogBoxExpandDuration = 0.45f;
        [SerializeField] float dialogBoxTargetWidth = 6f;
        [SerializeField] float dialogBoxHeight = 1.2f;
        [SerializeField] Ease dialogBoxEase = Ease.OutCubic;

        Sequence _sequence;
        DialogueSequence _currentSequence;
        int _lineIndex;
        bool _isOpen;
        bool _isAnimating;
        string _currentSpeakerTag = string.Empty;

        public bool IsOpen => _isOpen;
        public bool IsAnimating => _isAnimating;
        public bool IsBusy => _isOpen || _isAnimating;
        public string CurrentSpeakerTag => _currentSpeakerTag;
        public event Action DialogueOpened;
        public event Action DialogueClosed;
        public event Action<DialogueLine> LineStarted;
        public event Action SequenceCompleted;

        void Awake()
        {
            if (ui == null)
            {
                ui = GetComponent<UiSystem>();
            }

            if (dialogAnimator == null && ui != null && ui.DialogTextObject != null)
            {
                dialogAnimator = ui.DialogTextObject.GetComponent<TextAnimator_TMP>();
            }
        }

        void Update()
        {
            if (!_isOpen || _isAnimating)
            {
                return;
            }

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            var submit =
                (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)) ||
                (mouse != null && mouse.leftButton.wasPressedThisFrame);

            if (submit)
            {
                Advance();
            }
        }

        void OnDestroy()
        {
            KillTween();
        }

        public bool Play(string sequenceId)
        {
            if (pool == null || !pool.TryGet(sequenceId, out var sequence))
            {
                Debug.LogWarning($"[Dialogue] 对话池中找不到 id={sequenceId}");
                return false;
            }

            return Play(sequence);
        }

        public bool Play(DialogueSequence sequence)
        {
            if (sequence == null || sequence.Lines == null || sequence.Lines.Count == 0)
            {
                Debug.LogWarning("[Dialogue] 空对话序列");
                return false;
            }

            KillTween();
            _currentSequence = sequence;
            _lineIndex = 0;
            PlayLine(sequence.Lines[0], isOpening: true);
            return true;
        }

        /// <summary>推进到下一句；若已结束则关闭。</summary>
        public void Advance()
        {
            if (_isAnimating || !_isOpen || _currentSequence == null)
            {
                return;
            }

            _lineIndex++;
            if (_lineIndex >= _currentSequence.Lines.Count)
            {
                Close();
                SequenceCompleted?.Invoke();
                return;
            }

            PlayLine(_currentSequence.Lines[_lineIndex], isOpening: false);
        }

        public void Close()
        {
            KillTween();
            _isAnimating = false;
            _isOpen = false;
            _currentSequence = null;
            _lineIndex = 0;
            _currentSpeakerTag = string.Empty;

            if (dialogAnimator != null)
            {
                dialogAnimator.SetText(string.Empty);
            }

            if (ui != null)
            {
                ui.SetDialogTextActive(false);
                ui.SetContinueArrowActive(false);
                ui.SetPortraitActive(false);
                ui.SetDialogBoxActive(false);

                var stillShowingNotice =
                    (ui.NoticeTextObject != null && ui.NoticeTextObject.activeSelf) ||
                    (ui.FactoryTextObject != null && ui.FactoryTextObject.activeSelf);
                if (!stillShowingNotice)
                {
                    ui.SetOverlayActive(false);
                }
            }

            ResetDialogBoxWidth(0f);
            DialogueClosed?.Invoke();
        }

        void PlayLine(DialogueLine line, bool isOpening)
        {
            if (ui == null || line == null)
            {
                return;
            }

            _currentSpeakerTag = line.SpeakerTag ?? string.Empty;
            LineStarted?.Invoke(line);

            if (isOpening || !_isOpen)
            {
                OpenWithPresentation(line);
            }
            else
            {
                ApplyPortrait(line.Portrait);
                PresentText(line.Text);
            }
        }

        void OpenWithPresentation(DialogueLine line)
        {
            _isAnimating = true;
            _isOpen = true;
            DialogueOpened?.Invoke();

            // 文本必须等对话框展开完成后再激活。
            ui.SetDialogTextActive(false);
            ui.SetContinueArrowActive(false);
            ui.SetNoticeTextActive(false);
            ui.SetFactoryTextActive(false);

            var portrait = ui.Portrait;
            var dialogBox = ui.DialogBox;

            if (portrait != null)
            {
                ApplyPortrait(line.Portrait);
                var color = portrait.color;
                color.a = 0f;
                portrait.color = color;
                ui.SetPortraitActive(true);
            }

            if (dialogBox != null)
            {
                ResetDialogBoxWidth(0f);
                ui.SetDialogBoxActive(true);
            }

            KillTween();
            _sequence = DOTween.Sequence().SetUpdate(true);

            if (portrait != null)
            {
                _sequence.Append(portrait.DOFade(1f, portraitFadeDuration));
            }

            if (dialogBox != null)
            {
                var target = new Vector2(dialogBoxTargetWidth, dialogBoxHeight);
                _sequence.Append(
                    DOTween.To(
                            () => dialogBox.size,
                            size => dialogBox.size = size,
                            target,
                            dialogBoxExpandDuration)
                        .SetEase(dialogBoxEase));
            }

            _sequence.OnComplete(() =>
            {
                _isAnimating = false;
                PresentText(line.Text);
            });
        }

        void PresentText(string text)
        {
            if (ui == null)
            {
                return;
            }

            ui.SetOverlayActive(true);
            ui.SetDialogTextActive(true);
            ui.SetContinueArrowActive(true);

            if (dialogAnimator == null)
            {
                dialogAnimator = ui.DialogTextObject != null
                    ? ui.DialogTextObject.GetComponent<TextAnimator_TMP>()
                    : null;
            }

            if (dialogAnimator != null)
            {
                // 先隐藏再整体显现，触发全局入场效果；无出场效果。
                dialogAnimator.SetText(text ?? string.Empty, hideText: true);
                dialogAnimator.SetVisibilityEntireText(true, canPlayEffects: true);
            }
        }

        void ApplyPortrait(Sprite sprite)
        {
            var portrait = ui != null ? ui.Portrait : null;
            if (portrait == null)
            {
                return;
            }

            if (sprite != null)
            {
                portrait.sprite = sprite;
            }
        }

        void ResetDialogBoxWidth(float width)
        {
            var dialogBox = ui != null ? ui.DialogBox : null;
            if (dialogBox == null)
            {
                return;
            }

            var size = dialogBox.size;
            size.x = width;
            if (size.y <= 0f)
            {
                size.y = dialogBoxHeight;
            }

            dialogBox.size = size;
        }

        void KillTween()
        {
            if (_sequence != null && _sequence.IsActive())
            {
                _sequence.Kill();
            }

            _sequence = null;

            if (ui != null && ui.Portrait != null)
            {
                ui.Portrait.DOKill();
            }

            if (ui != null && ui.DialogBox != null)
            {
                ui.DialogBox.DOKill();
            }
        }
    }
}
