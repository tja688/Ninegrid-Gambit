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
        [SerializeField] float dialogBoxCollapseDuration = 0.35f;
        [SerializeField] float dialogBoxTargetWidth = 6f;
        [SerializeField] float dialogBoxHeight = 1.2f;
        [SerializeField] Ease dialogBoxEase = Ease.OutCubic;
        [SerializeField] Ease dialogBoxCollapseEase = Ease.InCubic;

        Sequence _sequence;
        DialogueSequence _currentSequence;
        int _lineIndex;
        bool _isOpen;
        bool _isAnimating;
        bool _isClosing;
        string _currentSpeakerName = string.Empty;

        public bool IsOpen => _isOpen;
        public bool IsAnimating => _isAnimating;
        public bool IsBusy => _isOpen || _isAnimating || _isClosing;
        public string CurrentSpeakerName => _currentSpeakerName;
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
            if (!_isOpen || _isAnimating || _isClosing)
            {
                return;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
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
            _isClosing = false;
            _currentSequence = sequence;
            _lineIndex = 0;
            PlayLine(sequence.Lines[0], isOpening: true);
            return true;
        }

        /// <summary>鼠标点击推进到下一句；若已结束则按退场顺序关闭。</summary>
        public void Advance()
        {
            if (_isAnimating || _isClosing || !_isOpen || _currentSequence == null)
            {
                return;
            }

            _lineIndex++;
            if (_lineIndex >= _currentSequence.Lines.Count)
            {
                CloseAnimated();
                return;
            }

            PlayLine(_currentSequence.Lines[_lineIndex], isOpening: false);
        }

        /// <summary>立即关闭（无退场动画）。</summary>
        public void CloseImmediate()
        {
            FinishClose(invokeCompleted: false);
        }

        void CloseAnimated()
        {
            if (_isClosing)
            {
                return;
            }

            _isClosing = true;
            _isAnimating = true;
            KillTween();

            // 1) 先退场 text（无出场/出场特效，直接隐藏）
            if (dialogAnimator != null)
            {
                dialogAnimator.SetText(string.Empty);
            }

            if (ui != null)
            {
                ui.SetDialogTextActive(false);
                ui.SetContinueArrowActive(false);
            }

            var dialogBox = ui != null ? ui.DialogBox : null;
            var portrait = ui != null ? ui.Portrait : null;

            _sequence = DOTween.Sequence().SetUpdate(true);

            // 2) 再收对话面板
            if (dialogBox != null)
            {
                _sequence.Append(
                    DOTween.To(
                            () => dialogBox.size,
                            size => dialogBox.size = size,
                            new Vector2(0f, dialogBoxHeight),
                            dialogBoxCollapseDuration)
                        .SetEase(dialogBoxCollapseEase));
                _sequence.AppendCallback(() =>
                {
                    if (ui != null)
                    {
                        ui.SetDialogBoxActive(false);
                    }
                });
            }

            // 3) 最后淡出立绘
            if (portrait != null)
            {
                _sequence.Append(portrait.DOFade(0f, portraitFadeDuration));
                _sequence.AppendCallback(() =>
                {
                    if (ui != null)
                    {
                        ui.SetPortraitActive(false);
                    }
                });
            }

            _sequence.OnComplete(() => FinishClose(invokeCompleted: true));
        }

        void FinishClose(bool invokeCompleted)
        {
            KillTween();
            _isAnimating = false;
            _isClosing = false;
            _isOpen = false;
            _currentSequence = null;
            _lineIndex = 0;
            _currentSpeakerName = string.Empty;

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
            ResetPortraitAlpha(1f);
            DialogueClosed?.Invoke();

            if (invokeCompleted)
            {
                SequenceCompleted?.Invoke();
            }
        }

        void PlayLine(DialogueLine line, bool isOpening)
        {
            if (ui == null || line == null)
            {
                return;
            }

            var speakerName = line.SpeakerName ?? string.Empty;
            var speakerChanged = !isOpening &&
                                 !string.Equals(_currentSpeakerName, speakerName, StringComparison.Ordinal);

            _currentSpeakerName = speakerName;
            LineStarted?.Invoke(line);

            if (isOpening || !_isOpen)
            {
                OpenWithPresentation(line);
                return;
            }

            // 同轮对话换角色：瞬间切头像，仅文字重新入场；面板与立绘保持。
            if (speakerChanged)
            {
                ApplyPortraitInstant(ResolvePortrait(speakerName));
            }

            PresentText(line.Text);
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
            var portraitSprite = ResolvePortrait(line.SpeakerName);

            if (portrait != null)
            {
                ApplyPortraitInstant(portraitSprite);
                ResetPortraitAlpha(0f);
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
            // 指向箭头由教学/提醒脚本显式控制，对话默认不弹出。
            ui.SetContinueArrowActive(false);

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

        Sprite ResolvePortrait(string speakerName)
        {
            if (pool == null)
            {
                return null;
            }

            var sprite = pool.ResolvePortrait(speakerName);
            if (sprite == null && !string.IsNullOrEmpty(speakerName))
            {
                Debug.LogWarning($"[Dialogue] 未找到说话人立绘: {speakerName}");
            }

            return sprite;
        }

        void ApplyPortraitInstant(Sprite sprite)
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

            // 瞬间切换时保持可见。
            var color = portrait.color;
            color.a = 1f;
            portrait.color = color;
        }

        void ResetPortraitAlpha(float alpha)
        {
            var portrait = ui != null ? ui.Portrait : null;
            if (portrait == null)
            {
                return;
            }

            var color = portrait.color;
            color.a = alpha;
            portrait.color = color;
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
